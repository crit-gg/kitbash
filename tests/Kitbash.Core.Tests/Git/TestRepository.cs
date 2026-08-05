using Kitbash.Core.Git;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// A real repository in a temporary folder, with the real git and the real services over it.
/// Nothing here is faked, because what is being tested is agreement with git itself.
/// </summary>
public sealed class TestRepository : IDisposable
{
    private sealed class NoBundle : IBundleEnvironment
    {
        public IReadOnlyDictionary<string, string> Outside { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class FoundOnPath : IExternalTools
    {
        public ExternalTool Git { get; } = new("git", Find("git"), null, false);

        public ExternalTool Dotnet { get; } = new("dotnet", Find("dotnet"), null, false);

        private static string? Find(string name)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";

            foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(folder, name);

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    private readonly IProcessRunner _processes = new ProcessRunner(new NoBundle());

    public TestRepository(bool bare = false)
    {
        Root = Path.Combine(
            Path.GetTempPath(), "kitbash-git-tests", Path.GetRandomFileName());

        Directory.CreateDirectory(Root);

        Tools = new FoundOnPath();
        Runner = new GitRunner(_processes, Tools, new FileSystem(), new GitEnvironment());
        Status = new GitStatusReader(_processes, Tools, new FileSystem());

        Patches = new GitPatchReader();
        Diffs = new GitDiffReader(Runner, Patches);
        Stager = new GitStager(Runner, new GitPatchWriter());
        History = new GitHistoryReader(Runner);
        Committer = new GitCommitter(Runner);
        Branches = new GitBranches(Runner);
        Sync = new GitSync(Runner);
        Conflicts = new GitConflictReader(Runner);
        Files = new GitFileStatusReader(Runner);
        Refs = new GitRefReader(Runner);
        Merger = new GitMerger(Runner, Refs);
        Blobs = new GitBlobReader(Runner);

        Git(bare ? ["init", "--bare", "-b", "main"] : ["init", "-b", "main"]);

        // Set in the repository rather than relied on from the machine, so a person's own
        // settings cannot change what these tests measure.
        Git(["config", "user.name", "Kitbash Tests"]);
        Git(["config", "user.email", "tests@example.invalid"]);
        Git(["config", "commit.gpgsign", "false"]);
        Git(["config", "tag.gpgsign", "false"]);
        Git(["config", "core.autocrlf", "false"]);
        Git(["config", "merge.conflictStyle", "merge"]);
        Git(["config", "diff.algorithm", "myers"]);
    }

    public string Root { get; }

    public IProcessRunner Processes => _processes;

    public IExternalTools Tools { get; }

    public GitRunner Runner { get; }

    public IGitStatusReader Status { get; }

    public GitPatchReader Patches { get; }

    public IGitDiffReader Diffs { get; }

    public IGitStager Stager { get; }

    public IGitHistoryReader History { get; }

    public IGitCommitter Committer { get; }

    public IGitBranches Branches { get; }

    public IGitSync Sync { get; }

    public IGitConflictReader Conflicts { get; }

    public IGitFileStatusReader Files { get; }

    public IGitRefReader Refs { get; }

    public IGitMerger Merger { get; }

    public IGitBlobReader Blobs { get; }

    /// <summary>Whether there is a git to run at all, which every test skips without.</summary>
    public bool HasGit => Runner.IsAvailable;

    /// <summary>Runs git and gives back what it wrote, failing the test when it refuses.</summary>
    public string Git(params string[] arguments)
    {
        var result = Runner.RunAsync(Root, new GitCommand(arguments)).GetAwaiter().GetResult();

        Assert.True(
            result.Succeeded,
            $"git {string.Join(' ', arguments)} failed with {result.ExitCode}: {result.Message}");

        return result.Output;
    }

    /// <summary>The same, without failing the test when git refuses.</summary>
    public GitResult Try(params string[] arguments) =>
        Runner.RunAsync(Root, new GitCommand(arguments)).GetAwaiter().GetResult();

    public void Write(string path, string contents)
    {
        var full = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        // Written with the newline git writes into a patch, so a hunk read back names the
        // same lines whichever machine this runs on.
        File.WriteAllText(full, contents.ReplaceLineEndings("\n"));
    }

    public string Read(string path) =>
        File.ReadAllText(Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar)));

    public void Delete(string path) =>
        File.Delete(Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Writes a file, stages it and commits, which is the usual way to set a scene.</summary>
    public void Commit(string message, params (string Path, string Contents)[] files)
    {
        foreach (var (path, contents) in files)
        {
            Write(path, contents);
        }

        Git(["add", "--all", "--", "."]);
        Git(["commit", "--message", message, "--allow-empty"]);
    }

    /// <summary>Numbered lines, which makes a hunk in the middle of a file easy to name.</summary>
    public static string Lines(int count, params (int Line, string Text)[] replacements)
    {
        var lines = Enumerable.Range(1, count).Select(i => "line " + i).ToArray();

        foreach (var (line, text) in replacements)
        {
            lines[line - 1] = text;
        }

        return string.Join('\n', lines) + "\n";
    }

    public void Dispose()
    {
        try
        {
            // Git leaves object files read only on some setups, which stops a plain delete.
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            // A temporary folder left behind is not worth failing a test over.
        }
    }
}
