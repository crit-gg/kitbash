using Kitbash.Core.Git;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// The two halves of a patch link that Core answers: which workspaces the name it gave
/// means, and the download. Real files and real git under a temporary home, with the
/// network faked.
/// </summary>
public sealed class PatchLinkTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "kitbash-patch-link-" + Path.GetRandomFileName());
    private readonly FakeWeb _web = new();
    private readonly ServiceProvider _provider;

    public PatchLinkTests()
    {
        Directory.CreateDirectory(_home);

        var services = new ServiceCollection();
        services.TryAddSingleton<IUserDirectories>(new TestDirectories(_home));
        services.TryAddSingleton<IWebContent>(_web);

        _provider = services.AddKitbashWorkspaceCreation().BuildServiceProvider();
    }

    private IGitPatchDownloader Downloader => _provider.GetRequiredService<IGitPatchDownloader>();

    private IWorkspaceRegistry Registry => _provider.GetRequiredService<IWorkspaceRegistry>();

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    /// <summary>A folder holding a Godot project, optionally named by its team config.</summary>
    private string Project(string folder, string godotName, string? teamName = null)
    {
        var root = Path.Combine(_home, "projects", folder);

        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "project.godot"),
            $"config_version=5\n\n[application]\n\nconfig/name=\"{godotName}\"\n");

        if (teamName is not null)
        {
            var team = Path.Combine(root, ".kitbash", "config", "kitbash.toml");

            Directory.CreateDirectory(Path.GetDirectoryName(team)!);
            File.WriteAllText(team, $"[workspace]\nname = \"{teamName}\"\n");
        }

        return root;
    }

    private IWorkspaceLinkNames Names => _provider.GetRequiredService<IWorkspaceLinkNames>();

    private async Task<IReadOnlyList<string>> Matching(string name) =>
        [.. (await Names.MatchAsync(name, TestContext.Current.CancellationToken)).Select(workspace => workspace.Root)];

    private Task<string?> NameOf(string root) => Names.ForAsync(root, TestContext.Current.CancellationToken);

    /// <summary>Runs git in a folder, for building the repositories these cases need.</summary>
    private static string Git(string root, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true };

        foreach (var argument in new[] { "-c", "user.name=Tests", "-c", "user.email=tests@example.invalid", "-c", "commit.gpgsign=false" }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed");

        return output.Trim();
    }

    /// <summary>A plain folder, no Godot project and no team name, holding one commit.</summary>
    private string Repository(string folder)
    {
        var root = Path.Combine(_home, "projects", folder);

        Directory.CreateDirectory(root);
        Git(root, "init", "-q", "-b", "main");
        File.WriteAllText(Path.Combine(root, folder + ".txt"), folder + "\n");
        Git(root, "add", "--all");
        Git(root, "commit", "-q", "-m", "First");

        return root;
    }

    private static bool HasGit => Environment.GetEnvironmentVariable("PATH")?
        .Split(Path.PathSeparator)
        .Any(folder => File.Exists(Path.Combine(folder, "git")) || File.Exists(Path.Combine(folder, "git.exe"))) == true;

    /// <summary>A person's own rename is theirs alone, so a link written by a teammate still finds it.</summary>
    [Fact]
    public async Task ALinksNameFindsAWorkspaceWhateverThePersonCalledIt()
    {
        var root = Registry.Add(Project("door-game", "Door Game")).Root;

        Registry.Rename(root, "My copy");

        Assert.Equal("Door Game", await NameOf(root));
        Assert.Equal([root], await Matching("door game"));
        Assert.Empty(await Matching("My copy"));
    }

    [Fact]
    public async Task TheTeamConfigNameComesBeforeTheProjects()
    {
        var root = Registry.Add(Project("game", "Prototype", teamName: "Slopworks")).Root;

        Assert.Equal("Slopworks", await NameOf(root));
        Assert.Equal([root], await Matching("Slopworks"));
        Assert.Empty(await Matching("Prototype"));
    }

    /// <summary>Nothing names it, so the first commit does, and every clone has the same one.</summary>
    [Fact]
    public async Task AnUnnamedRepositoryIsNamedByItsFirstCommit()
    {
        if (!HasGit)
        {
            return;
        }

        var root = Registry.Add(Repository("plain")).Root;
        var clone = Path.Combine(_home, "projects", "someone-elses-folder");

        Git(_home, "clone", "-q", root, clone);
        Registry.Add(clone);

        var name = await NameOf(root);

        Assert.Equal(Git(root, "rev-list", "--max-parents=0", "HEAD"), name);
        Assert.Equal(name, await NameOf(clone));
        Assert.Equal([root, clone], (await Matching(name!)).Order(StringComparer.Ordinal));
        Assert.Empty(await Matching("plain"));
    }

    /// <summary>After unrelated histories are merged there are two first commits, and either answers.</summary>
    [Fact]
    public async Task EitherRootOfAMergedHistoryMatches()
    {
        if (!HasGit)
        {
            return;
        }

        var root = Registry.Add(Repository("joined")).Root;
        var other = Repository("other");

        Git(root, "pull", "-q", "--no-rebase", "--allow-unrelated-histories", "--no-edit", other, "main");

        var roots = Git(root, "rev-list", "--max-parents=0", "HEAD").Split('\n');

        Assert.Equal(2, roots.Length);
        Assert.Equal(roots.Order(StringComparer.Ordinal).First(), await NameOf(root));

        foreach (var commit in roots)
        {
            Assert.Equal([root], await Matching(commit));
        }
    }

    [Fact]
    public async Task AFolderWithNoNameAndNoRepositoryHasNoName()
    {
        var root = Path.Combine(_home, "projects", "plain");

        Directory.CreateDirectory(root);
        Registry.Add(root);

        Assert.Null(await NameOf(root));
        Assert.Empty(await Matching("plain"));
    }

    [Fact]
    public async Task TwoClonesOfOneProjectBothAnswer()
    {
        Registry.Add(Project("game", "Door Game"));
        Registry.Add(Project("game-copy", "Door Game"));

        Assert.Equal(2, (await Matching("Door Game")).Count);
    }

    [Fact]
    public async Task APatchDownloadsIntoTheCacheUnderItsOwnName()
    {
        _web.Body = "From 0000 Mon Sep 17 00:00:00 2001\n"u8.ToArray();

        var file = await Downloader.DownloadAsync(
            WebAddress.Parse("https://example.invalid/team/fix-door.patch"), TestContext.Current.CancellationToken);

        Assert.Equal("fix-door.patch", Path.GetFileName(file));
        Assert.StartsWith(Path.Combine(_home, "cache"), file, StringComparison.Ordinal);
        Assert.Equal(_web.Body, File.ReadAllBytes(file));

        Downloader.Discard(file);

        Assert.False(Directory.Exists(Path.GetDirectoryName(file)));
    }

    [Fact]
    public async Task PlainHttpIsNeverAsked()
    {
        var failure = await Assert.ThrowsAsync<GitPatchDownloadException>(() =>
            Downloader.DownloadAsync(WebAddress.Parse("http://example.invalid/fix.patch"), TestContext.Current.CancellationToken));

        Assert.Equal(GitPatchDownloadFailure.NotSecure, failure.Failure);
        Assert.Equal(0, _web.Asked);
    }

    /// <summary>Stopped as it passes the limit, whatever the server claimed, and nothing is left.</summary>
    [Fact]
    public async Task APatchPastTheLimitIsStoppedAndLeavesNothing()
    {
        _web.Body = new byte[Downloader.Limit + 1];

        var failure = await Assert.ThrowsAsync<GitPatchDownloadException>(() =>
            Downloader.DownloadAsync(WebAddress.Parse("https://example.invalid/huge.patch"), TestContext.Current.CancellationToken));

        Assert.Equal(GitPatchDownloadFailure.TooLarge, failure.Failure);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_home, "cache"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AServerThatRefusesIsAFailedDownload()
    {
        _web.Refuse = true;

        var failure = await Assert.ThrowsAsync<GitPatchDownloadException>(() =>
            Downloader.DownloadAsync(WebAddress.Parse("https://example.invalid/fix.patch"), TestContext.Current.CancellationToken));

        Assert.Equal(GitPatchDownloadFailure.Failed, failure.Failure);
    }

    /// <summary>An address ending in something that is not a plain file name is given one.</summary>
    [Fact]
    public async Task AnOddLastSegmentIsNotUsedAsAFileName()
    {
        _web.Body = "x"u8.ToArray();

        var file = await Downloader.DownloadAsync(
            WebAddress.Parse("https://example.invalid/raw/..%2F..%2Fescape"), TestContext.Current.CancellationToken);

        Assert.Equal("downloaded.patch", Path.GetFileName(file));
    }

    private sealed class FakeWeb : IWebContent
    {
        public byte[] Body { get; set; } = [];

        public bool Refuse { get; set; }

        public int Asked { get; private set; }

        public Task<string> ReadTextAsync(WebAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<long?> MeasureAsync(WebAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        /// <summary>Writes in blocks and reports after each, the way the real one does.</summary>
        public async Task DownloadAsync(
            WebAddress address, string path, IProgress<long>? progress, CancellationToken cancellationToken)
        {
            Asked++;

            if (Refuse)
            {
                throw new HttpRequestException("404 Not Found");
            }

            await using var target = File.Create(path);
            var written = 0;

            while (written < Body.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var block = Math.Min(1024 * 1024, Body.Length - written);

                await target.WriteAsync(Body.AsMemory(written, block), cancellationToken);
                written += block;
                progress?.Report(written);
            }
        }
    }

    private sealed class TestDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "run");
    }
}
