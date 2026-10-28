using Kitbash.Core.Git;
using Kitbash.Core.IO;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// A patch from git format-patch, applied around a person's own work. What every failure is
/// checked for is that the repository afterwards is the one before, to the byte.
/// </summary>
public sealed class GitPatchApplierTests : IDisposable
{
    private readonly TestRepository _repo = new();
    private readonly string _patch = Path.Combine(Path.GetTempPath(), "kitbash-patch-" + Path.GetRandomFileName() + ".patch");

    public GitPatchApplierTests()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Commit("Base", ("a.txt", TestRepository.Lines(9)), ("b.txt", "b\n"), ("d.txt", "d\n"));

        // The developer's change, made and then taken back off so only the patch carries it.
        _repo.Git("config", "user.name", "Dana Developer");
        _repo.Commit("Fix the door\n\nIt was stuck.", ("a.txt", TestRepository.Lines(9, (2, "line 2 fixed"))), ("c.txt", "new\n"));
        File.WriteAllText(_patch, _repo.Git("format-patch", "-1", "--stdout"));
        _repo.Git("reset", "--hard", "HEAD~1");
        _repo.Git("config", "user.name", "Sam Person");
    }

    private GitPatchApplier Applier => new(_repo.Runner, _repo.Committer, _repo.Conflicts, _repo.Patches, new FileSystem());

    public void Dispose()
    {
        _repo.Dispose();
        File.Delete(_patch);
    }

    /// <summary>Everything a failure must leave as it was.</summary>
    private string Snapshot() => string.Join(
        "\n---\n",
        _repo.Git("rev-parse", "HEAD"),
        _repo.Git("ls-files", "--stage"),
        _repo.Git("diff"),
        _repo.Git("stash", "list", "--format=%H %gs"),
        _repo.Git("status", "--porcelain", "--untracked-files=all"),
        string.Join(",", Directory.GetFiles(_repo.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal)));

    [Fact]
    public async Task AppliesAroundEditsAndKeepsWhatWasStaged()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Write("d.txt", "older work\n");
        _repo.Git("stash", "push", "--message", "the person's own");
        _repo.Write("a.txt", TestRepository.Lines(9, (8, "line 8 mine")));
        _repo.Write("b.txt", "b\nstaged\n");
        _repo.Git("add", "b.txt");

        var result = await Applier.ApplyAsync(_repo.Root, _patch);

        Assert.Equal(GitPatchOutcome.Applied, result.Outcome);
        Assert.Equal("main", result.Branch);
        Assert.Equal(["a.txt", "c.txt"], result.Paths);

        Assert.Equal("Dana Developer|Sam Person|Fix the door", _repo.Git("log", "-1", "--format=%an|%cn|%s").Trim());
        Assert.Equal("a.txt\nc.txt\n", _repo.Git("show", "--name-only", "--format=", "HEAD"));

        Assert.Equal(TestRepository.Lines(9, (2, "line 2 fixed"), (8, "line 8 mine")), _repo.Read("a.txt"));
        Assert.Equal(" M a.txt\nM  b.txt\n", _repo.Git("status", "--porcelain", "--untracked-files=no"));
        Assert.Equal("the person's own", _repo.Git("stash", "list", "--format=%s").Trim().Split(": ")[^1]);
    }

    [Fact]
    public async Task NothingOfTheirsMeansNoStash()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        var result = await Applier.ApplyAsync(_repo.Root, _patch);

        Assert.Equal(GitPatchOutcome.Applied, result.Outcome);
        Assert.Equal(string.Empty, _repo.Git("stash", "list"));
    }

    [Fact]
    public async Task AnEditOnThePatchedLineIsInTheWayAndChangesNothing()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Write("a.txt", TestRepository.Lines(9, (2, "line 2 mine")));
        _repo.Write("b.txt", "b\nstaged\n");
        _repo.Git("add", "b.txt");
        var before = Snapshot();

        var result = await Applier.ApplyAsync(_repo.Root, _patch);

        Assert.Equal(GitPatchOutcome.InTheWay, result.Outcome);
        Assert.Equal(["a.txt"], result.Paths);
        Assert.Equal(before, Snapshot());
    }

    /// <summary>A hook is the person's own and runs. Refusing still changes nothing.</summary>
    [Fact]
    public async Task ARefusedCommitChangesNothing()
    {
        if (!_repo.HasGit || OperatingSystem.IsWindows())
        {
            return;
        }

        var hook = Path.Combine(_repo.Root, ".git", "hooks", "applypatch-msg");

        File.WriteAllText(hook, "#!/bin/sh\necho refused by the hook >&2\nexit 1\n");
        File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _repo.Write("a.txt", TestRepository.Lines(9, (8, "line 8 mine")));
        var before = Snapshot();

        var result = await Applier.ApplyAsync(_repo.Root, _patch);

        Assert.Equal(GitPatchOutcome.Refused, result.Outcome);
        Assert.Contains("refused by the hook", result.Message);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task AnUntrackedFileWhereThePatchAddsOneChangesNothing()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Write("c.txt", "mine\n");
        _repo.Write("a.txt", TestRepository.Lines(9, (8, "line 8 mine")));
        var before = Snapshot();

        var result = await Applier.ApplyAsync(_repo.Root, _patch);

        Assert.Equal(GitPatchOutcome.DoesNotFit, result.Outcome);
        Assert.Equal(before, Snapshot());
        Assert.Equal("mine\n", _repo.Read("c.txt"));
    }

    [Fact]
    public async Task APlainDiffIsNotAPatchAndChangesNothing()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Write("a.txt", TestRepository.Lines(9, (2, "line 2 fixed")));
        File.WriteAllText(_patch, _repo.Git("diff"));
        _repo.Git("checkout", "--", "a.txt");
        var before = Snapshot();

        var result = await Applier.ApplyAsync(_repo.Root, _patch);

        Assert.Equal(GitPatchOutcome.NotAPatch, result.Outcome);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task APatchAlreadyInIsSaidSoAndChangesNothing()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        await Applier.ApplyAsync(_repo.Root, _patch);
        var before = Snapshot();

        var result = await Applier.ApplyAsync(_repo.Root, _patch);

        Assert.Equal(GitPatchOutcome.AlreadyApplied, result.Outcome);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task ADetachedHeadIsRefused()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Git("switch", "--detach", "HEAD");

        Assert.Equal(GitPatchOutcome.NotOnBranch, (await Applier.ApplyAsync(_repo.Root, _patch)).Outcome);
    }

    [Fact]
    public async Task AConflictUnderWayIsRefused()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Git("switch", "--create", "other");
        _repo.Commit("Theirs", ("b.txt", "theirs\n"));
        _repo.Git("switch", "main");
        _repo.Commit("Ours", ("b.txt", "ours\n"));
        _repo.Try("merge", "other");

        Assert.Equal(GitPatchOutcome.Busy, (await Applier.ApplyAsync(_repo.Root, _patch)).Outcome);
    }

    /// <summary>
    /// A run stopped between setting work aside and putting it back leaves a stash. The next
    /// run refuses until it is back, and putting it back restores everything.
    /// </summary>
    [Fact]
    public async Task WorkLeftSetAsideIsFoundAndPutBack()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Write("a.txt", TestRepository.Lines(9, (8, "line 8 mine")));
        _repo.Write("b.txt", "b\nstaged\n");
        _repo.Git("add", "b.txt");
        var before = Snapshot().Split("\n---\n")[1..3];
        _repo.Git("stash", "push", "--message", "Kitbash: before applying fix.patch");

        Assert.Equal("Kitbash: before applying fix.patch", await Applier.ReadSetAsideAsync(_repo.Root, TestContext.Current.CancellationToken));
        Assert.Equal(GitPatchOutcome.SetAsideWaiting, (await Applier.ApplyAsync(_repo.Root, _patch)).Outcome);

        Assert.Equal(GitRestoreOutcome.Restored, await Applier.RestoreAsync(_repo.Root));
        Assert.Equal(before, Snapshot().Split("\n---\n")[1..3]);
        Assert.Null(await Applier.ReadSetAsideAsync(_repo.Root, TestContext.Current.CancellationToken));
    }

    /// <summary>Anything done since is never put at risk, so a tree with changes is left alone.</summary>
    [Fact]
    public async Task WorkSetAsideIsNotPutBackOverNewChanges()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Write("a.txt", TestRepository.Lines(9, (8, "line 8 mine")));
        _repo.Git("stash", "push", "--message", "Kitbash: before applying fix.patch");
        _repo.Write("d.txt", "since\n");
        var before = Snapshot();

        Assert.Equal(GitRestoreOutcome.NotClean, await Applier.RestoreAsync(_repo.Root));
        Assert.Equal(before, Snapshot());
    }

    /// <summary>Put back onto the applied patch where the two overlap, the stash is kept.</summary>
    [Fact]
    public async Task WorkThatWillNotGoBackStaysInTheStash()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        _repo.Write("a.txt", TestRepository.Lines(9, (2, "line 2 mine")));
        _repo.Git("stash", "push", "--message", "Kitbash: before applying fix.patch");
        _repo.Git("am", _patch);

        Assert.Equal(GitRestoreOutcome.Kept, await Applier.RestoreAsync(_repo.Root));
        Assert.Equal(string.Empty, _repo.Git("status", "--porcelain", "--untracked-files=no"));
        Assert.Contains("line 2 mine", _repo.Git("stash", "show", "-p", "stash@{0}"));
    }

    [Fact]
    public void ThePreviewReadsTheSubjectAndTheFiles()
    {
        if (!_repo.HasGit)
        {
            return;
        }

        var preview = Applier.Read(File.ReadAllText(_patch));

        Assert.True(preview.CanApply);
        Assert.Equal("Fix the door", preview.Subject);
        Assert.Equal(["a.txt", "c.txt"], preview.Files.Select(file => file.Path));
    }

    [Fact]
    public void AnEncodedFoldedSubjectIsReadTheWayGitAmReadsIt()
    {
        var preview = Applier.Read(
            "From 0000000000000000000000000000000000000000 Mon Sep 17 00:00:00 2001\n"
            + "From: Dana <d@example.invalid>\n"
            + "Subject: [PATCH 2/3] =?UTF-8?q?R=C3=A9pare?=\n =?UTF-8?q?_la_porte?=\n"
            + "\n"
            + "---\n"
            + "diff --git a/a.txt b/a.txt\n");

        Assert.Equal("Répare la porte", preview.Subject);
    }

    [Fact]
    public void ABinaryFileWithoutItsContentsIsNamed()
    {
        var preview = Applier.Read(
            "From 0000000000000000000000000000000000000000 Mon Sep 17 00:00:00 2001\n"
            + "Subject: [PATCH] Art\n"
            + "\n"
            + "diff --git a/door.png b/door.png\n"
            + "new file mode 100644\n"
            + "index 0000000..34513ed\n"
            + "Binary files /dev/null and b/door.png differ\n");

        Assert.False(preview.CanApply);
        Assert.Equal(["door.png"], preview.MissingBinary);
    }
}
