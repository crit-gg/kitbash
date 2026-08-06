using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// Telling one repository about a merge driver. Both halves live under the git directory, so
/// nothing a person would commit moves and every clone has to be told again.
/// </summary>
public class MergeDriverTests
{
    /// <summary>
    /// Takes their side whole. Git runs a driver through a shell, so this is the same command
    /// on either machine, and it is short enough to read in a failure message.
    /// </summary>
    private static GitMergeDriver Driver(string command = "cat \"%B\" > \"%A\"") =>
        new("probe", "A driver for the tests", command, ["*.tscn"]);

    private static string Attributes(TestRepository repository) =>
        File.ReadAllText(Path.Combine(repository.Root, ".git", "info", "attributes"));

    [Fact]
    public async Task WiringWritesTheConfigAndTheAttributes()
    {
        using var repository = new TestRepository();

        if (!repository.HasGit)
        {
            return;
        }

        var outcome = await repository.Drivers.EnsureAsync(repository.Root, Driver(), TestContext.Current.CancellationToken);

        Assert.Equal(GitMergeDriverOutcome.Wired, outcome);

        Assert.Equal(
            "cat \"%B\" > \"%A\"",
            repository.Git("config", "--local", "--get", "merge.probe.driver").Trim());

        Assert.Equal(
            "A driver for the tests",
            repository.Git("config", "--local", "--get", "merge.probe.name").Trim());

        Assert.Contains("*.tscn merge=probe", Attributes(repository), StringComparison.Ordinal);
    }

    // The attributes go beside the config rather than into the working tree, so opening a
    // repository never puts a change in front of a person that they did not make.
    [Fact]
    public async Task NothingIsWrittenIntoTheWorkingTree()
    {
        using var repository = new TestRepository();

        if (!repository.HasGit)
        {
            return;
        }

        repository.Commit("first", ("readme.md", "hello\n"));

        await repository.Drivers.EnsureAsync(repository.Root, Driver(), TestContext.Current.CancellationToken);

        Assert.Equal("", repository.Git("status", "--porcelain").Trim());
    }

    [Fact]
    public async Task WiringItAgainWritesNothing()
    {
        using var repository = new TestRepository();

        if (!repository.HasGit)
        {
            return;
        }

        await repository.Drivers.EnsureAsync(repository.Root, Driver(), TestContext.Current.CancellationToken);

        var again = await repository.Drivers.EnsureAsync(repository.Root, Driver(), TestContext.Current.CancellationToken);

        Assert.Equal(GitMergeDriverOutcome.Ready, again);

        // One line, not two. A driver written on every open would grow the file forever.
        Assert.Single(
            Attributes(repository).Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task ChangingTheCommandRewritesTheConfig()
    {
        using var repository = new TestRepository();

        if (!repository.HasGit)
        {
            return;
        }

        await repository.Drivers.EnsureAsync(repository.Root, Driver(), TestContext.Current.CancellationToken);

        var moved = await repository.Drivers.EnsureAsync(repository.Root, Driver("cat \"%O\" > \"%A\""), TestContext.Current.CancellationToken);

        Assert.Equal(GitMergeDriverOutcome.Wired, moved);

        Assert.Equal(
            "cat \"%O\" > \"%A\"",
            repository.Git("config", "--local", "--get", "merge.probe.driver").Trim());
    }

    // A team that already told git what to do with these files meant it, and the attributes
    // written here would win over their own, so the pattern is left as they set it.
    [Fact]
    public async Task APatternSomebodyElseClaimsIsLeftAlone()
    {
        using var repository = new TestRepository();

        if (!repository.HasGit)
        {
            return;
        }

        repository.Commit("first", (".gitattributes", "*.tscn merge=binary\n"));

        var outcome = await repository.Drivers.EnsureAsync(repository.Root, Driver(), TestContext.Current.CancellationToken);

        Assert.Equal(GitMergeDriverOutcome.Claimed, outcome);

        Assert.False(File.Exists(Path.Combine(repository.Root, ".git", "info", "attributes")));

        Assert.Contains(
            "binary", repository.Git("check-attr", "merge", "--", "player.tscn"), StringComparison.Ordinal);
    }

    // The whole point. Everything else here is the wiring, and this is git running it.
    [Fact]
    public async Task TheDriverRunsWhenTwoBranchesTouchOneScene()
    {
        using var repository = new TestRepository();

        if (!repository.HasGit)
        {
            return;
        }

        repository.Commit("first", ("player.tscn", "base\n"));

        repository.Git("switch", "--create", "theirs");
        repository.Commit("theirs", ("player.tscn", "their side\n"));

        repository.Git("switch", "main");
        repository.Commit("ours", ("player.tscn", "our side\n"));

        await repository.Drivers.EnsureAsync(repository.Root, Driver(), TestContext.Current.CancellationToken);

        var merge = repository.Try("merge", "--no-edit", "theirs");

        Assert.True(merge.Succeeded, merge.Message);

        // Git's own text merge would have left conflict markers here. The driver took a side
        // and said it had settled it, so the merge finished.
        Assert.Equal("their side\n", repository.Read("player.tscn"));
    }

    [Fact]
    public async Task ADriverThatFailsLeavesTheFileConflicted()
    {
        using var repository = new TestRepository();

        if (!repository.HasGit)
        {
            return;
        }

        repository.Commit("first", ("player.tscn", "base\n"));

        repository.Git("switch", "--create", "theirs");
        repository.Commit("theirs", ("player.tscn", "their side\n"));

        repository.Git("switch", "main");
        repository.Commit("ours", ("player.tscn", "our side\n"));

        // Writes a file and then says it could not settle it, which is what a driver does
        // when it produces something readable that a person still has to look at.
        await repository.Drivers.EnsureAsync(
            repository.Root,
            Driver("cat \"%B\" > \"%A\"; exit 1"),
            TestContext.Current.CancellationToken);

        var merge = repository.Try("merge", "--no-edit", "theirs");

        Assert.False(merge.Succeeded);

        Assert.Contains("player.tscn", repository.Git("diff", "--name-only", "--diff-filter=U"), StringComparison.Ordinal);

        // What the driver wrote is what is in the working tree, markers or not.
        Assert.Equal("their side\n", repository.Read("player.tscn"));
    }
}
