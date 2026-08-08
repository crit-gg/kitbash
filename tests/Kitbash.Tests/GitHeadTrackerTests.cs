using Kitbash.Core.Git;
using Kitbash.Workspaces;

namespace Kitbash.Tests;

/// <summary>
/// What decides that the launcher reads the open workspace again. The status monitor
/// speaks for the working tree as well, and none of that changes a workspace's config.
/// </summary>
public sealed class GitHeadTrackerTests
{
    private static readonly GitPlaces Places = new("/repo/.git", "/repo/.git", null, "/repo");

    private static GitStatus Status(string branch, string commit, int modified = 0) =>
        new(Places, branch, commit, false, true, 0, 0, modified, 0, 0, null);

    /// <summary>
    /// The first read arrives with the load that asked for it, so reloading on it would
    /// read every workspace twice at launch and again on every switch.
    /// </summary>
    [Fact]
    public void TheFirstStatusIsNotAMove()
    {
        var head = new GitHeadTracker();

        Assert.False(head.Moved(Status("main", "aaa")));
    }

    [Fact]
    public void ABranchSwitchIsAMove()
    {
        var head = new GitHeadTracker();

        head.Moved(Status("main", "aaa"));

        Assert.True(head.Moved(Status("side", "bbb")));
    }

    /// <summary>An update that brought commits in, which leaves the branch where it was.</summary>
    [Fact]
    public void NewCommitsOnTheSameBranchAreAMove()
    {
        var head = new GitHeadTracker();

        head.Moved(Status("main", "aaa"));

        Assert.True(head.Moved(Status("main", "bbb")));
    }

    [Fact]
    public void EditingTheWorkingTreeIsNotAMove()
    {
        var head = new GitHeadTracker();

        head.Moved(Status("main", "aaa"));

        Assert.False(head.Moved(Status("main", "aaa", modified: 3)));
    }

    /// <summary>
    /// Letting go of a repository and taking up another, which is switching workspaces.
    /// Neither half is a move, since the switch reads the workspace itself.
    /// </summary>
    [Fact]
    public void LosingARepositoryAndFindingAnotherIsNotAMove()
    {
        var head = new GitHeadTracker();

        head.Moved(Status("main", "aaa"));

        Assert.False(head.Moved(null));
        Assert.False(head.Moved(Status("other", "ccc")));
    }

    /// <summary>
    /// A move is answered once. The reload it asks for is the whole of the response, so
    /// saying so again on the next read would reload for nothing.
    /// </summary>
    [Fact]
    public void AMoveIsOnlyReportedOnce()
    {
        var head = new GitHeadTracker();

        head.Moved(Status("main", "aaa"));

        Assert.True(head.Moved(Status("side", "bbb")));
        Assert.False(head.Moved(Status("side", "bbb")));
    }
}
