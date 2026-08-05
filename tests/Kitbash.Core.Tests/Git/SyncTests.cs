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
}
