using System.Text;
using System.Text.RegularExpressions;
using Kitbash.Core.IO;

namespace Kitbash.Core.Git;

/// <summary>
/// Applies a patch with <c>git stash</c>, <c>git am</c> and <c>git stash pop</c>, and puts
/// everything back with <c>git am --abort</c>, <c>git reset --merge</c> and a second pop.
/// </summary>
public sealed partial class GitPatchApplier : IGitPatchApplier
{
    /// <summary>Starts the message of every stash this sets aside, which is how one is found again.</summary>
    private const string Marker = "Kitbash: before applying";

    private readonly IGitRunner _git;
    private readonly IGitCommitter _committer;
    private readonly IGitConflictReader _conflicts;
    private readonly GitPatchReader _patches;
    private readonly IFileSystem _fileSystem;

    public GitPatchApplier(
        IGitRunner git,
        IGitCommitter committer,
        IGitConflictReader conflicts,
        GitPatchReader patches,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(committer);
        ArgumentNullException.ThrowIfNull(conflicts);
        ArgumentNullException.ThrowIfNull(patches);
        ArgumentNullException.ThrowIfNull(fileSystem);

        _git = git;
        _committer = committer;
        _conflicts = conflicts;
        _patches = patches;
        _fileSystem = fileSystem;
    }

    public GitPatchPreview Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var subject = Subject(text);
        var files = _patches.Read(text);

        // A diff made without --binary names the file and carries nothing to write.
        var missing = files
            .Where(file => file.IsBinary && file.Header.Any(line => line.StartsWith("Binary files ", StringComparison.Ordinal)))
            .Select(file => file.Path)
            .ToList();

        return new GitPatchPreview(subject ?? string.Empty, subject is not null, files, missing);
    }

    public async Task<GitPatchResult> ApplyAsync(string root, string patchFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(patchFile);

        if (!_git.IsAvailable)
        {
            return new GitPatchResult(GitPatchOutcome.GitMissing);
        }

        var gitDirectory = await RunAsync(root, "rev-parse", "--absolute-git-dir").ConfigureAwait(false);

        if (!gitDirectory.Succeeded)
        {
            return new GitPatchResult(GitPatchOutcome.NotARepository, gitDirectory.Message);
        }

        var places = gitDirectory.Output.Trim();
        var branch = await RunAsync(root, "symbolic-ref", "--quiet", "--short", "HEAD").ConfigureAwait(false);

        if (!branch.Succeeded)
        {
            return new GitPatchResult(GitPatchOutcome.NotOnBranch);
        }

        var head = await RunAsync(root, "rev-parse", "--quiet", "--verify", "HEAD").ConfigureAwait(false);

        if (!head.Succeeded)
        {
            return new GitPatchResult(GitPatchOutcome.NoCommits);
        }

        if (await IsBusyAsync(root, places).ConfigureAwait(false))
        {
            return new GitPatchResult(GitPatchOutcome.Busy);
        }

        if (await FindSetAsideAsync(root, CancellationToken.None).ConfigureAwait(false) is not null)
        {
            return new GitPatchResult(GitPatchOutcome.SetAsideWaiting);
        }

        if (await _committer.ReadIdentityAsync(root).ConfigureAwait(false) is null)
        {
            return new GitPatchResult(GitPatchOutcome.NoIdentity);
        }

        GitPatchPreview preview;

        try
        {
            preview = Read(_fileSystem.ReadAllText(patchFile));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GitPatchResult(GitPatchOutcome.NotAPatch, exception.Message);
        }

        if (!preview.IsMail || preview.Files.Count == 0)
        {
            return new GitPatchResult(GitPatchOutcome.NotAPatch);
        }

        if (preview.MissingBinary.Count > 0)
        {
            return new GitPatchResult(GitPatchOutcome.MissingBinary, Files: preview.MissingBinary);
        }

        var paths = preview.Files.Select(file => file.Path).ToList();

        // A patch that will not go forward but will come back out is one that is already in.
        var forward = await RunAsync(root, "apply", "--check", patchFile).ConfigureAwait(false);

        if (!forward.Succeeded
            && (await RunAsync(root, "apply", "--check", "--reverse", patchFile).ConfigureAwait(false)).Succeeded)
        {
            return new GitPatchResult(GitPatchOutcome.AlreadyApplied, Files: paths);
        }

        var edited = await RunAsync(root, "diff", "--name-only", "-z", "HEAD").ConfigureAwait(false);
        var inTheWay = paths.Intersect(edited.Records, StringComparer.Ordinal).ToList();

        return await WriteAsync(
            root, places, head.Output.Trim(), branch.Output.Trim(), patchFile, paths, inTheWay).ConfigureAwait(false);
    }

    public async Task<string?> ReadSetAsideAsync(string root, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        return (await FindSetAsideAsync(root, cancellation).ConfigureAwait(false))?.Label;
    }

    public async Task<GitRestoreOutcome> RestoreAsync(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (await FindSetAsideAsync(root, CancellationToken.None).ConfigureAwait(false) is not { } stash)
        {
            return GitRestoreOutcome.Nothing;
        }

        var gitDirectory = await RunAsync(root, "rev-parse", "--absolute-git-dir").ConfigureAwait(false);

        // A run that stopped inside git am left it under way, and abandoning it is what
        // that run would have done next.
        if (gitDirectory.Succeeded && IsApplyingMail(gitDirectory.Output.Trim())
            && !(await RunAsync(root, "am", "--abort").ConfigureAwait(false)).Succeeded)
        {
            return GitRestoreOutcome.Kept;
        }

        // Only onto a clean tree, so every change a failed pop leaves came from the stash
        // and putting the tree back cannot touch anything the person did since.
        var status = await RunAsync(root, "status", "--porcelain", "--untracked-files=no").ConfigureAwait(false);

        if (!status.Succeeded || status.Output.Trim().Length > 0)
        {
            return GitRestoreOutcome.NotClean;
        }

        if (await PopAsync(root, stash.Id).ConfigureAwait(false))
        {
            return GitRestoreOutcome.Restored;
        }

        await RunAsync(root, "reset", "--merge", "HEAD").ConfigureAwait(false);

        return GitRestoreOutcome.Kept;
    }

    /// <summary>The half that writes. Every failure from here on puts everything back.</summary>
    private async Task<GitPatchResult> WriteAsync(
        string root,
        string places,
        string start,
        string branch,
        string patchFile,
        IReadOnlyList<string> paths,
        IReadOnlyList<string> inTheWay)
    {
        var label = $"{Marker} {Path.GetFileName(patchFile)}";
        var before = await StashTipAsync(root).ConfigureAwait(false);

        // Untracked files stay where they are. A patch adding one that is already there is
        // refused by git am before it writes anything.
        var push = await RunAsync(root, "stash", "push", "--message", label).ConfigureAwait(false);

        if (!push.Succeeded)
        {
            return new GitPatchResult(GitPatchOutcome.Refused, push.Message, paths);
        }

        // Git makes no stash when there is nothing to set aside, so the tip is compared.
        var tip = await StashTipAsync(root).ConfigureAwait(false);
        var ours = tip is not null && tip != before ? tip : null;

        var am = await RunAsync(root, "am", patchFile).ConfigureAwait(false);

        if (!am.Succeeded)
        {
            return await RollBackAsync(root, places, start, ours, label).ConfigureAwait(false)
                ?? new GitPatchResult(Classify(am), am.Message, paths);
        }

        if (ours is not null && !await PopAsync(root, ours).ConfigureAwait(false))
        {
            return await RollBackAsync(root, places, start, ours, label).ConfigureAwait(false)
                ?? new GitPatchResult(GitPatchOutcome.InTheWay, string.Empty, inTheWay.Count > 0 ? inTheWay : paths);
        }

        return new GitPatchResult(GitPatchOutcome.Applied, am.Message, paths, branch);
    }

    /// <summary>
    /// Puts the repository back where it started. Null when it got there, and Stranded when a
    /// step refused, in which case nothing further is tried and the stash is left alone.
    /// </summary>
    private async Task<GitPatchResult?> RollBackAsync(
        string root, string places, string start, string? ours, string label)
    {
        if (IsApplyingMail(places))
        {
            var abort = await RunAsync(root, "am", "--abort").ConfigureAwait(false);

            if (!abort.Succeeded)
            {
                return Stranded(abort, ours, label);
            }
        }

        var head = await RunAsync(root, "rev-parse", "HEAD").ConfigureAwait(false);

        // --merge keeps any file something else wrote meanwhile, which --hard would discard.
        if (head.Output.Trim() != start)
        {
            var reset = await RunAsync(root, "reset", "--merge", start).ConfigureAwait(false);

            if (!reset.Succeeded)
            {
                return Stranded(reset, ours, label);
            }
        }

        // The stash goes back onto the commit it was made on, which git can always do.
        if (ours is not null && !await PopAsync(root, ours).ConfigureAwait(false))
        {
            return Stranded(GitResult.Skipped, ours, label);
        }

        return null;
    }

    private static GitPatchResult Stranded(GitResult said, string? ours, string label) =>
        new(GitPatchOutcome.Stranded, said.Message, SetAside: ours is null ? string.Empty : label);

    /// <summary>
    /// Pops one stash by its object name, so another entry pushed meanwhile is never the
    /// one taken. A pop that fails keeps its entry, which is git's own rule.
    /// </summary>
    private async Task<bool> PopAsync(string root, string id)
    {
        var list = await RunAsync(root, "stash", "list", "--format=%H").ConfigureAwait(false);
        var index = list.Lines.ToList().IndexOf(id);

        if (index < 0)
        {
            return false;
        }

        var pop = await RunAsync(root, "stash", "pop", "--index", $"stash@{{{index}}}").ConfigureAwait(false);

        return pop.Succeeded;
    }

    private async Task<string?> StashTipAsync(string root)
    {
        var tip = await RunAsync(root, "rev-parse", "--quiet", "--verify", "refs/stash").ConfigureAwait(false);

        return tip.Succeeded ? tip.Output.Trim() : null;
    }

    private async Task<(string Id, string Label)?> FindSetAsideAsync(string root, CancellationToken cancellation)
    {
        var list = await _git
            .RunAsync(root, GitCommand.Of("stash", "list", "--format=%H%x09%gs"), cancellation)
            .ConfigureAwait(false);

        foreach (var line in list.Succeeded ? list.Lines : [])
        {
            var parts = line.Split('\t', 2);

            // Git writes "On <branch>: " ahead of the message.
            if (parts.Length == 2 && parts[1].IndexOf(Marker, StringComparison.Ordinal) is var at and >= 0)
            {
                return (parts[0], parts[1][at..]);
            }
        }

        return null;
    }

    private async Task<bool> IsBusyAsync(string root, string places)
    {
        if (await _conflicts.ReadStateAsync(root).ConfigureAwait(false) != GitMergeState.None)
        {
            return true;
        }

        // Git keeps a stopped rebase or patch in these two folders, and writes no head for
        // either at some stops.
        if (IsApplyingMail(places) || _fileSystem.DirectoryExists(Path.Combine(places, "rebase-merge")))
        {
            return true;
        }

        var unmerged = await RunAsync(root, "ls-files", "--unmerged").ConfigureAwait(false);

        return unmerged.Output.Trim().Length > 0;
    }

    private bool IsApplyingMail(string places) =>
        _fileSystem.DirectoryExists(Path.Combine(places, "rebase-apply"));

    /// <summary>What git am's refusal means, read from its words since its exit code is always one.</summary>
    private static GitPatchOutcome Classify(GitResult am)
    {
        var said = am.Error + am.Output;

        if (said.Contains("Patch format detection failed", StringComparison.Ordinal))
        {
            return GitPatchOutcome.NotAPatch;
        }

        return said.Contains("patch does not apply", StringComparison.Ordinal)
            || said.Contains("already exists in", StringComparison.Ordinal)
            || said.Contains("does not exist in index", StringComparison.Ordinal)
            ? GitPatchOutcome.DoesNotFit
            : GitPatchOutcome.Refused;
    }

    private Task<GitResult> RunAsync(string root, params string[] arguments) =>
        _git.RunAsync(root, new GitCommand(arguments));

    /// <summary>
    /// The subject <c>git am</c> will commit with, or null when this is not a mail. Folded
    /// lines are joined, encoded words decoded, and leading bracket tags such as
    /// <c>[PATCH 2/3]</c> dropped, which is what <c>git mailinfo</c> does.
    /// </summary>
    private static string? Subject(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).SkipWhile(line => line.Length == 0).ToList();

        if (lines.Count == 0 || !lines[0].StartsWith("From ", StringComparison.Ordinal))
        {
            return null;
        }

        string? subject = null;
        var inSubject = false;

        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0)
            {
                break;
            }

            if (line[0] is ' ' or '\t')
            {
                if (inSubject)
                {
                    subject += line;
                }

                continue;
            }

            inSubject = line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase);

            if (inSubject)
            {
                subject = line["Subject:".Length..];
            }
        }

        return subject is null ? null : WithoutTags(Decoded(subject));
    }

    private static string WithoutTags(string subject)
    {
        var text = subject.Trim();

        while (true)
        {
            if (text.StartsWith('[') && text.IndexOf(']') is var close and > 0)
            {
                text = text[(close + 1)..].TrimStart();
            }
            else if (text.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
            {
                text = text[3..].TrimStart();
            }
            else
            {
                return text;
            }
        }
    }

    /// <summary>
    /// RFC 2047 encoded words, which format-patch writes for a subject outside ASCII. Space
    /// between two of them is not part of the text, which is that RFC's own rule.
    /// </summary>
    private static string Decoded(string value)
    {
        var joined = BetweenWords().Replace(value, "?==?");

        return EncodedWord().Replace(joined, match =>
        {
            try
            {
                var encoding = Encoding.GetEncoding(match.Groups["charset"].Value);
                var body = match.Groups["text"].Value;

                var bytes = match.Groups["kind"].Value is "B" or "b"
                    ? Convert.FromBase64String(body)
                    : QuotedBytes(body);

                return encoding.GetString(bytes);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return match.Value;
            }
        });
    }

    private static byte[] QuotedBytes(string body)
    {
        var bytes = new List<byte>(body.Length);

        for (var at = 0; at < body.Length; at++)
        {
            if (body[at] == '_')
            {
                bytes.Add((byte)' ');
            }
            else if (body[at] == '=' && at + 2 < body.Length)
            {
                bytes.Add(Convert.ToByte(body.Substring(at + 1, 2), 16));
                at += 2;
            }
            else
            {
                bytes.Add((byte)body[at]);
            }
        }

        return [.. bytes];
    }

    [GeneratedRegex(@"\?=\s+=\?")]
    private static partial Regex BetweenWords();

    [GeneratedRegex(@"=\?(?<charset>[^?]+)\?(?<kind>[QqBb])\?(?<text>[^?]*)\?=")]
    private static partial Regex EncodedWord();
}
