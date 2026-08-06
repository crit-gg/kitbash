using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// Pushing and pulling, against a bare repository on this machine. No network is involved,
/// which is deliberate: what is being tested is reading git's own words, and git says the
/// same things over a local remote as over a real one.
/// </summary>
public class SyncTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    /// <summary>A bare repository, and a clone of it with one commit already pushed.</summary>
    private static (TestRepository Remote, TestRepository Local) Pair()
    {
        var remote = new TestRepository(bare: true);
        var local = new TestRepository();

        local.Commit("start", ("one.txt", "one\n"));
        local.Git("remote", "add", "origin", remote.Root);
        local.Git("push", "--set-upstream", "origin", "main");

        return (remote, local);
    }

    /// <summary>A second working copy of the same remote, standing in for a teammate.</summary>
    private static TestRepository Second(TestRepository remote)
    {
        var other = new TestRepository();

        other.Git("remote", "add", "origin", remote.Root);
        other.Git("fetch", "origin");
        other.Git("switch", "main");

        return other;
    }

    [Fact]
    public async Task APushSendsTheCommitsAndSayingItAgainSaysThereIsNothingToSend()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        local.Commit("something new", ("two.txt", "two\n"));

        var sent = await local.Sync.PushAsync(local.Root, null, Stop);

        Assert.Equal(GitSyncOutcome.Done, sent.Outcome);

        var again = await local.Sync.PushAsync(local.Root, null, Stop);

        Assert.Equal(GitSyncOutcome.AlreadyLevel, again.Outcome);
    }

    // This is the one the workflow rests on: a push from behind comes back refused, and it
    // has to be told apart from every other reason a push can fail.
    [Fact]
    public async Task APushFromBehindIsRejectedRatherThanFailing()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        using var other = Second(remote);
        other.Commit("someone else got there first", ("theirs.txt", "theirs\n"));
        other.Git("push", "origin", "main");

        local.Commit("mine", ("mine.txt", "mine\n"));

        var sent = await local.Sync.PushAsync(local.Root, null, Stop);

        Assert.Equal(GitSyncOutcome.Rejected, sent.Outcome);
        Assert.NotEqual("", sent.Message);
    }

    [Fact]
    public async Task ABranchThatTracksNothingIsSaidToHaveNoUpstream()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        local.Git("switch", "--create", "on-its-own");
        local.Commit("only here", ("new.txt", "new\n"));

        var sent = await local.Sync.PushAsync(local.Root, null, Stop);

        Assert.Equal(GitSyncOutcome.NoUpstream, sent.Outcome);
    }

    [Fact]
    public async Task ARepositoryWithNoRemoteSaysSo()
    {
        using var local = new TestRepository();
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        local.Commit("start", ("one.txt", "one\n"));

        var sent = await local.Sync.PushAsync(local.Root, null, Stop);

        Assert.Equal(GitSyncOutcome.NoRemote, sent.Outcome);
    }

    [Fact]
    public async Task SettingTheUpstreamOnTheFirstPushMakesTheBranchTrackIt()
    {
        using var remote = new TestRepository(bare: true);
        using var local = new TestRepository();
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        local.Commit("start", ("one.txt", "one\n"));
        local.Git("remote", "add", "origin", remote.Root);

        var sent = await local.Sync.PushAsync(
            local.Root,
            new GitPushRequest { Remote = "origin", Branch = "main", SetUpstream = true },
            Stop);

        Assert.Equal(GitSyncOutcome.Done, sent.Outcome);

        var main = Assert.Single(
            await local.Branches.ReadAsync(local.Root, false, Stop), b => b.Name == "main");

        Assert.Equal("origin/main", main.Upstream);
    }

    [Fact]
    public async Task APullTakesTheNewCommitsWhenItIsAStraightLine()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        using var other = Second(remote);
        other.Commit("from somewhere else", ("theirs.txt", "theirs\n"));
        other.Git("push", "origin", "main");

        var taken = await local.Sync.PullAsync(local.Root, null, Stop);

        Assert.Equal(GitSyncOutcome.Done, taken.Outcome);
        Assert.Equal("theirs\n", local.Read("theirs.txt"));
    }

    [Fact]
    public async Task APullWithNothingToTakeSaysSo()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        var taken = await local.Sync.PullAsync(local.Root, null, Stop);

        Assert.Equal(GitSyncOutcome.AlreadyLevel, taken.Outcome);
    }

    // Both sides moved, so the branch pointer cannot simply be moved forward. That is a
    // different thing from the remote refusing, and the repair for it is a merge.
    [Fact]
    public async Task TwoHistoriesThatBothMovedAreSaidToHaveDiverged()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        using var other = Second(remote);
        other.Commit("theirs", ("theirs.txt", "theirs\n"));
        other.Git("push", "origin", "main");

        local.Commit("mine", ("mine.txt", "mine\n"));

        var taken = await local.Sync.PullAsync(local.Root, null, Stop);

        Assert.Equal(GitSyncOutcome.Diverged, taken.Outcome);

        // Allowed a real merge, the same two histories join.
        var merged = await local.Sync.PullAsync(
            local.Root, new GitPullRequest { FastForwardOnly = false }, Stop);

        Assert.Equal(GitSyncOutcome.Done, merged.Outcome);
        Assert.Equal("theirs\n", local.Read("theirs.txt"));
    }

    [Fact]
    public async Task AMergeThatCannotBeSettledIsSaidToBeConflicted()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        using var other = Second(remote);
        other.Commit("theirs", ("one.txt", "their version\n"));
        other.Git("push", "origin", "main");

        local.Commit("mine", ("one.txt", "my version\n"));

        var taken = await local.Sync.PullAsync(
            local.Root, new GitPullRequest { FastForwardOnly = false }, Stop);

        Assert.Equal(GitSyncOutcome.Conflicted, taken.Outcome);
    }

    [Fact]
    public async Task AFetchBringsTheRemoteRefForwardWithoutMovingTheBranch()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        using var other = Second(remote);
        other.Commit("theirs", ("theirs.txt", "theirs\n"));
        other.Git("push", "origin", "main");

        var fetched = await local.Sync.FetchAsync(local.Root, null, Stop);

        Assert.True(fetched.Succeeded, fetched.Message);

        var main = Assert.Single(
            await local.Branches.ReadAsync(local.Root, false, Stop), b => b.Name == "main");

        Assert.Equal(1, main.Behind);
        Assert.False(File.Exists(Path.Combine(local.Root, "theirs.txt")));
    }

    [Fact]
    public async Task ARepositoryWithNoRemoteSaysSoRatherThanFailing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.Empty(await repository.Sync.ReadRemotesAsync(repository.Root, Stop));
    }

    [Fact]
    public async Task EveryRemoteIsNamed()
    {
        using var first = new TestRepository(bare: true);
        using var second = new TestRepository(bare: true);
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("remote", "add", "origin", first.Root);
        repository.Git("remote", "add", "backup", second.Root);

        var remotes = await repository.Sync.ReadRemotesAsync(repository.Root, Stop);

        Assert.Equal(["backup", "origin"], remotes);
    }

    // The whole point of asking this way: the answer arrives without the objects behind it,
    // so a client can ask often and a repository of any size can afford it.
    [Fact]
    public async Task TheRemoteTipIsReadWithoutTakingTheCommitItNames()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        using var other = Second(remote);
        other.Commit("someone else got there first", ("theirs.txt", "theirs\n"));
        other.Git("push", "origin", "main");

        var tips = await local.Sync.ReadRemoteTipsAsync(local.Root, "origin", ["main"], null, Stop);

        Assert.True(tips.Reached);

        var tip = Assert.IsType<string>(tips.Tip("main"));

        Assert.Equal(40, tip.Length);

        // The commit is named and is not here, which is what says nothing came down with it.
        Assert.False(await local.Refs.ExistsAsync(local.Root, tip, Stop));
    }

    [Fact]
    public async Task SeveralBranchesComeBackFromOneAsk()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        local.Git("switch", "-c", "feature");
        local.Commit("on the branch", ("two.txt", "two\n"));
        local.Git("push", "origin", "feature");

        var tips = await local.Sync
            .ReadRemoteTipsAsync(local.Root, "origin", ["main", "feature", "nothing"], null, Stop);

        Assert.NotNull(tips.Tip("main"));
        Assert.NotNull(tips.Tip("feature"));

        // A branch the remote does not have is absent rather than an error, since a branch
        // nobody has pushed yet is the ordinary case.
        Assert.Null(tips.Tip("nothing"));
    }

    // Reached and holding nothing has to be told apart from not answering at all, since one
    // means there is no such branch and the other means we do not know.
    [Fact]
    public async Task ARemoteThatCannotBeAskedIsNotTheSameAsOneWithNothingOnIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("remote", "add", "origin", Path.Combine(repository.Root, "nowhere"));

        var tips = await repository.Sync
            .ReadRemoteTipsAsync(repository.Root, "origin", ["main"], null, Stop);

        Assert.False(tips.Reached);
        Assert.Null(tips.Tip("main"));
    }

    [Fact]
    public async Task OneBranchIsFetchedAndTheOthersAreLeftWhereTheyWere()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        using var other = Second(remote);

        other.Commit("on main", ("theirs.txt", "theirs\n"));
        other.Git("push", "origin", "main");

        other.Git("switch", "-c", "elsewhere");
        other.Commit("on another branch", ("other.txt", "other\n"));
        other.Git("push", "origin", "elsewhere");

        var fetched = await local.Sync.FetchBranchAsync(local.Root, "origin", "main", Stop);

        Assert.True(fetched.Succeeded);
        Assert.True(await local.Refs.ExistsAsync(local.Root, "refs/remotes/origin/main", Stop));
        Assert.False(await local.Refs.ExistsAsync(local.Root, "refs/remotes/origin/elsewhere", Stop));
    }

    // A branch somebody rewrote upstream still updates here, which is what the plus on git's
    // own default refspec is for. Without it the tracking ref would stay where it was.
    [Fact]
    public async Task ABranchRewrittenUpstreamStillUpdatesHere()
    {
        var (remote, local) = Pair();
        using var _ = remote;
        using var __ = local;
        Assert.SkipUnless(local.HasGit, "no git on this machine");

        using var other = Second(remote);
        other.Commit("first", ("theirs.txt", "theirs\n"));
        other.Git("push", "origin", "main");

        await local.Sync.FetchBranchAsync(local.Root, "origin", "main", Stop);

        var before = local.Git("rev-parse", "refs/remotes/origin/main").Trim();

        other.Git("reset", "--hard", "HEAD~1");
        other.Commit("rewritten", ("different.txt", "different\n"));
        other.Git("push", "--force", "origin", "main");

        var fetched = await local.Sync.FetchBranchAsync(local.Root, "origin", "main", Stop);

        Assert.True(fetched.Succeeded);

        Assert.NotEqual(before, local.Git("rev-parse", "refs/remotes/origin/main").Trim());
    }
}
