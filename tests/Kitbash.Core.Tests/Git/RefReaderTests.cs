namespace Kitbash.Core.Tests.Git;

/// <summary>
/// The default branch of a remote, and how far one revision is from another. Both are what
/// a client needs before it can say anything about a branch against the one it came from.
/// </summary>
public class RefReaderTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ACloneLearnsWhichBranchTheRemoteCallsDefault()
    {
        using var remote = new TestRepository(bare: true);
        using var seed = new TestRepository();
        Assert.SkipUnless(seed.HasGit, "no git on this machine");

        seed.Commit("start", ("one.txt", "one\n"));
        seed.Git("remote", "add", "origin", remote.Root);
        seed.Git("push", "--set-upstream", "origin", "main");

        // A bare repository's own head is what a clone reads, so it is set the way a
        // hosted repository sets it.
        remote.Git("symbolic-ref", "HEAD", "refs/heads/main");

        using var clone = new TestRepository();
        clone.Git("remote", "add", "origin", remote.Root);
        clone.Git("fetch", "origin");
        clone.Git("remote", "set-head", "origin", "--auto");

        Assert.Equal("main", await clone.Refs.ReadDefaultBranchAsync(clone.Root, "origin", Stop));
    }

    // Only a clone writes this ref. A repository built with remote add and fetch has none,
    // which is common enough that a caller has to have a fall back.
    [Fact]
    public async Task ARepositoryThatWasNeverClonedHasNoDefaultBranchRecorded()
    {
        using var remote = new TestRepository(bare: true);
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("remote", "add", "origin", remote.Root);
        repository.Git("push", "--set-upstream", "origin", "main");

        Assert.Null(await repository.Refs.ReadDefaultBranchAsync(repository.Root, "origin", Stop));

        // And the fall back a caller then makes is answerable.
        Assert.True(await repository.Refs.ExistsAsync(repository.Root, "origin/main", Stop));
        Assert.False(await repository.Refs.ExistsAsync(repository.Root, "origin/master", Stop));
    }

    [Fact]
    public async Task ARepositoryWithNoRemoteAnswersNothing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.Null(await repository.Refs.ReadDefaultBranchAsync(repository.Root, "origin", Stop));
        Assert.False(await repository.Refs.ExistsAsync(repository.Root, "origin/main", Stop));
    }

    [Fact]
    public async Task BothCountsAreAboutTheHeadAgainstTheBaseline()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("branch", "baseline");

        repository.Commit("mine one", ("two.txt", "two\n"));
        repository.Commit("mine two", ("three.txt", "three\n"));

        var apart = await repository.Refs.CompareAsync(repository.Root, "baseline", "HEAD", Stop);

        Assert.NotNull(apart);
        Assert.Equal(2, apart.Ahead);
        Assert.Equal(0, apart.Behind);
        Assert.False(apart.IsLevel);
        Assert.False(apart.HasParted);

        // The other way round is the mirror, which is the check that the two are not swapped.
        var back = await repository.Refs.CompareAsync(repository.Root, "HEAD", "baseline", Stop);

        Assert.NotNull(back);
        Assert.Equal(0, back.Ahead);
        Assert.Equal(2, back.Behind);
    }

    [Fact]
    public async Task TwoBranchesThatBothMovedHaveParted()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("switch", "--create", "side");
        repository.Commit("theirs", ("side.txt", "side\n"));
        repository.Git("switch", "main");
        repository.Commit("ours one", ("main.txt", "main\n"));
        repository.Commit("ours two", ("main2.txt", "main\n"));

        var apart = await repository.Refs.CompareAsync(repository.Root, "side", "HEAD", Stop);

        Assert.NotNull(apart);
        Assert.Equal(2, apart.Ahead);
        Assert.Equal(1, apart.Behind);
        Assert.True(apart.HasParted);
    }

    [Fact]
    public async Task LevelIsSaidToBeLevel()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("branch", "same");

        var apart = await repository.Refs.CompareAsync(repository.Root, "same", "HEAD", Stop);

        Assert.NotNull(apart);
        Assert.True(apart.IsLevel);
    }

    // A revision that does not resolve is not the same answer as being level, and a client
    // that treated it as level would say a branch is up to date with something absent.
    [Fact]
    public async Task ARevisionThatDoesNotResolveAnswersNothing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.Null(await repository.Refs.CompareAsync(repository.Root, "origin/main", "HEAD", Stop));
    }

    // Where a branch parted from the one it came off. A branch view is drawn from that
    // commit outwards, so it is the anchor rather than a detail.
    [Fact]
    public async Task TheCommitTwoBranchesPartedAtIsFound()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        var parted = repository.Git("rev-parse", "HEAD").Trim();

        repository.Git("switch", "--create", "side");
        repository.Commit("theirs", ("side.txt", "side\n"));
        repository.Git("switch", "main");
        repository.Commit("ours", ("main.txt", "main\n"));

        Assert.Equal(parted, await repository.Refs.ReadMergeBaseAsync(repository.Root, "main", "side", Stop));
    }

    // Two histories with nothing in common are not an error, and a caller drawing a branch
    // against a base it never came from has to be able to say so.
    [Fact]
    public async Task HistoriesWithNothingInCommonPartedNowhere()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("switch", "--orphan", "alone");
        repository.Commit("elsewhere", ("other.txt", "other\n"));

        Assert.Null(await repository.Refs.ReadMergeBaseAsync(repository.Root, "main", "alone", Stop));
        Assert.Null(await repository.Refs.ReadMergeBaseAsync(repository.Root, "main", "nothing", Stop));
    }

    [Fact]
    public async Task TheBranchTheHeadIsOnIsNamed()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.Equal("main", await repository.Refs.ReadHeadBranchAsync(repository.Root, Stop));

        repository.Git("switch", "--detach", "HEAD");

        Assert.Null(await repository.Refs.ReadHeadBranchAsync(repository.Root, Stop));
    }

    // A branch does not have to be called what it tracks, and a client that assumed it was
    // would ask the remote about a branch nobody has.
    [Fact]
    public async Task ABranchTracksWhateverItWasToldTo()
    {
        using var remote = new TestRepository(bare: true);
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("remote", "add", "origin", remote.Root);
        repository.Git("push", "--set-upstream", "origin", "main");

        repository.Git("switch", "--create", "feature");
        repository.Commit("mine", ("two.txt", "two\n"));
        repository.Git("push", "origin", "feature:something-else");
        repository.Git("branch", "--set-upstream-to=origin/something-else", "feature");

        var tracking = await repository.Refs.ReadUpstreamAsync(repository.Root, "feature", Stop);

        Assert.NotNull(tracking);
        Assert.Equal("origin", tracking.Remote);
        Assert.Equal("something-else", tracking.Branch);
        Assert.Equal("origin/something-else", tracking.Ref);
        Assert.False(tracking.IsGone);
    }

    [Fact]
    public async Task ABranchThatTracksNothingAnswersNothing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.Null(await repository.Refs.ReadUpstreamAsync(repository.Root, "main", Stop));
        Assert.Null(await repository.Refs.ReadUpstreamAsync(repository.Root, "nothing", Stop));
    }

    // The config still names it, so the branch tracks something that is not there. Every
    // count measured against it is missing rather than zero, which is what a client has to
    // tell apart from being level.
    [Fact]
    public async Task AnUpstreamTheRemoteNoLongerHasIsSaidToBeGone()
    {
        using var remote = new TestRepository(bare: true);
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("remote", "add", "origin", remote.Root);
        repository.Git("push", "--set-upstream", "origin", "main");

        repository.Git("switch", "--create", "feature");
        repository.Commit("mine", ("two.txt", "two\n"));
        repository.Git("push", "--set-upstream", "origin", "feature");

        repository.Git("push", "origin", "--delete", "feature");
        repository.Git("fetch", "--prune", "origin");

        var tracking = await repository.Refs.ReadUpstreamAsync(repository.Root, "feature", Stop);

        Assert.NotNull(tracking);
        Assert.Equal("origin", tracking.Remote);
        Assert.Equal("feature", tracking.Branch);
        Assert.True(tracking.IsGone);
    }
}
