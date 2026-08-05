namespace Kitbash.Core.Tests.Git;

/// <summary>Listing branches and moving between them.</summary>
public class BranchTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryBranchIsListedAndTheCurrentOneIsMarked()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("branch", "side");

        var branches = await repository.Branches.ReadAsync(repository.Root, true, Stop);

        Assert.Equal(2, branches.Count);
        Assert.True(Assert.Single(branches, b => b.Name == "main").IsCurrent);
        Assert.False(Assert.Single(branches, b => b.Name == "side").IsCurrent);
        Assert.All(branches, b => Assert.Equal(40, b.Tip.Length));
        Assert.All(branches, b => Assert.Equal("start", b.Subject));
        Assert.All(branches, b => Assert.NotNull(b.LastCommit));
    }

    [Fact]
    public async Task ABranchIsMadeAndMovedToInOneGesture()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        var made = await repository.Branches.CreateAsync(repository.Root, "feature", null, true, Stop);

        Assert.True(made.Succeeded, made.Message);

        var branches = await repository.Branches.ReadAsync(repository.Root, true, Stop);

        Assert.True(Assert.Single(branches, b => b.Name == "feature").IsCurrent);
    }

    [Fact]
    public async Task ABranchIsMadeWithoutMovingToIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("first", ("one.txt", "one\n"));
        repository.Commit("second", ("two.txt", "two\n"));

        var made = await repository.Branches.CreateAsync(
            repository.Root, "from-the-start", "HEAD~1", false, Stop);

        Assert.True(made.Succeeded, made.Message);

        var branches = await repository.Branches.ReadAsync(repository.Root, true, Stop);
        var branch = Assert.Single(branches, b => b.Name == "from-the-start");

        Assert.False(branch.IsCurrent);
        Assert.Equal("first", branch.Subject);

        // Who wrote the tip is how a list says whose branch it is.
        Assert.Equal("Kitbash Tests", branch.Author);
    }

    [Fact]
    public async Task SwitchingAndSwitchingBackBothWork()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("branch", "side");

        Assert.True((await repository.Branches.SwitchAsync(repository.Root, "side", Stop)).Succeeded);
        Assert.True((await repository.Branches.SwitchAsync(repository.Root, "main", Stop)).Succeeded);

        var branches = await repository.Branches.ReadAsync(repository.Root, true, Stop);

        Assert.True(Assert.Single(branches, b => b.Name == "main").IsCurrent);
    }

    [Fact]
    public async Task MovingToACommitLeavesTheHeadDetached()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("first", ("one.txt", "one\n"));
        repository.Commit("second", ("two.txt", "two\n"));

        var moved = await repository.Branches.SwitchToCommitAsync(repository.Root, "HEAD~1", Stop);

        Assert.True(moved.Succeeded, moved.Message);

        var branches = await repository.Branches.ReadAsync(repository.Root, true, Stop);

        Assert.DoesNotContain(branches, b => b.IsCurrent);
    }

    [Fact]
    public async Task DeletingIsRefusedForWorkThatIsNowhereElseUntilItIsForced()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("switch", "--create", "side");
        repository.Commit("only on the side", ("side.txt", "side\n"));
        repository.Git("switch", "main");

        var refused = await repository.Branches.DeleteAsync(repository.Root, "side", false, Stop);

        Assert.False(refused.Succeeded);
        Assert.NotEqual("", refused.Message);

        var forced = await repository.Branches.DeleteAsync(repository.Root, "side", true, Stop);

        Assert.True(forced.Succeeded, forced.Message);
        Assert.DoesNotContain(
            await repository.Branches.ReadAsync(repository.Root, true, Stop),
            b => b.Name == "side");
    }

    [Fact]
    public async Task RenamingKeepsTheCommitItPointsAt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("branch", "before");

        var renamed = await repository.Branches.RenameAsync(repository.Root, "before", "after", Stop);

        Assert.True(renamed.Succeeded, renamed.Message);

        var branches = await repository.Branches.ReadAsync(repository.Root, true, Stop);

        Assert.DoesNotContain(branches, b => b.Name == "before");
        Assert.Contains(branches, b => b.Name == "after");
    }

    [Fact]
    public async Task ATrackingBranchCarriesItsUpstreamAndHowFarApartTheyAre()
    {
        using var remote = new TestRepository(bare: true);
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("remote", "add", "origin", remote.Root);
        repository.Git("push", "--set-upstream", "origin", "main");

        repository.Commit("ahead by one", ("two.txt", "two\n"));

        var branches = await repository.Branches.ReadAsync(repository.Root, true, Stop);
        var main = Assert.Single(branches, b => b.Name == "main");

        Assert.Equal("origin/main", main.Upstream);
        Assert.Equal(1, main.Ahead);
        Assert.Equal(0, main.Behind);
        Assert.False(main.UpstreamIsGone);

        Assert.Contains(branches, b => b is { IsRemote: true, Name: "origin/main" });
    }

    [Fact]
    public async Task RemoteBranchesAreLeftOutWhenTheyAreNotAskedFor()
    {
        using var remote = new TestRepository(bare: true);
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("remote", "add", "origin", remote.Root);
        repository.Git("push", "--set-upstream", "origin", "main");

        var branches = await repository.Branches.ReadAsync(repository.Root, false, Stop);

        Assert.DoesNotContain(branches, b => b.IsRemote);
    }

    [Fact]
    public async Task ABranchWithNothingLeftOnItReadsAsMerged()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("switch", "--create", "done");
        repository.Commit("finished", ("two.txt", "two\n"));
        repository.Git("switch", "main");
        repository.Git("merge", "--no-edit", "done");

        repository.Git("switch", "--create", "going");
        repository.Commit("part way", ("three.txt", "three\n"));
        repository.Git("switch", "main");

        var merged = await repository.Branches.ReadMergedAsync(repository.Root, "main", true, Stop);

        Assert.Contains("refs/heads/done", merged);
        Assert.DoesNotContain("refs/heads/still going", merged);

        // A branch is merged into itself, which is what says the base has nothing waiting.
        Assert.Contains("refs/heads/main", merged);
    }

    [Fact]
    public async Task NothingIsMergedIntoARevisionThatDoesNotResolve()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.Empty(await repository.Branches.ReadMergedAsync(repository.Root, "nowhere", true, Stop));
    }
}
