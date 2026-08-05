using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// Merging, and asking beforehand what it would cost. Git says which of these happened in
/// prose rather than in an exit code, so the reading of that prose is what is measured here.
/// </summary>
public class MergeTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AMergeThatOnlyMovesThePointerIsNotACommit()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("switch", "--create", "work");
        repository.Commit("mine", ("two.txt", "two\n"));
        repository.Git("switch", "main");

        var merged = await repository.Merger.MergeAsync(
            repository.Root, "work", GitMergeRequest.FastForward, Stop);

        Assert.Equal(GitMergeOutcome.FastForwarded, merged.Outcome);
        Assert.True(merged.Succeeded);

        // A fast forward records nothing, so main is the same commit work is.
        Assert.Equal(
            repository.Git("rev-parse", "work").Trim(),
            repository.Git("rev-parse", "main").Trim());
    }

    [Fact]
    public async Task MergingWhatIsAlreadyHereDoesNothing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("branch", "work");

        var merged = await repository.Merger.MergeAsync(repository.Root, "work", null, Stop);

        Assert.Equal(GitMergeOutcome.AlreadyLevel, merged.Outcome);
        Assert.True(merged.Succeeded);
    }

    [Fact]
    public async Task TwoHistoriesThatBothMovedRefuseAFastForwardAndTakeARealMerge()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("switch", "--create", "work");
        repository.Commit("theirs", ("two.txt", "two\n"));
        repository.Git("switch", "main");
        repository.Commit("ours", ("three.txt", "three\n"));

        var refused = await repository.Merger.MergeAsync(
            repository.Root, "work", GitMergeRequest.FastForward, Stop);

        Assert.Equal(GitMergeOutcome.NotFastForward, refused.Outcome);
        Assert.False(refused.Succeeded);

        var merged = await repository.Merger.MergeAsync(repository.Root, "work", null, Stop);

        Assert.Equal(GitMergeOutcome.Merged, merged.Outcome);
        Assert.Equal("two\n", repository.Read("two.txt"));
    }

    [Fact]
    public async Task AMergeThatCollidesLeavesTheConflictToSettle()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("a.txt", "one\n"));
        repository.Git("switch", "--create", "work");
        repository.Commit("theirs", ("a.txt", "theirs\n"));
        repository.Git("switch", "main");
        repository.Commit("ours", ("a.txt", "ours\n"));

        var merged = await repository.Merger.MergeAsync(repository.Root, "work", null, Stop);

        Assert.Equal(GitMergeOutcome.Conflicted, merged.Outcome);
        Assert.Equal(GitMergeState.Merging, await repository.Conflicts.ReadStateAsync(repository.Root, Stop));

        var conflicts = await repository.Conflicts.ReadAsync(repository.Root, Stop);

        Assert.Equal("a.txt", Assert.Single(conflicts).Path);
    }

    [Fact]
    public async Task WorkOnDiskThatAMergeWouldOverwriteStopsItBeforeItStarts()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("a.txt", "one\n"));
        repository.Git("switch", "--create", "work");
        repository.Commit("theirs", ("a.txt", "theirs\n"));
        repository.Git("switch", "main");

        repository.Write("a.txt", "typing\n");

        var merged = await repository.Merger.MergeAsync(repository.Root, "work", null, Stop);

        Assert.Equal(GitMergeOutcome.Blocked, merged.Outcome);

        // Nothing started, so what was being typed is still there.
        Assert.Equal("typing\n", repository.Read("a.txt"));
        Assert.Equal(GitMergeState.None, await repository.Conflicts.ReadStateAsync(repository.Root, Stop));
    }

    [Fact]
    public async Task ARevisionNothingResolvesIsToldApartFromAFailure()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        var merged = await repository.Merger.MergeAsync(repository.Root, "nowhere", null, Stop);

        Assert.Equal(GitMergeOutcome.NoSuchRevision, merged.Outcome);
    }

    [Fact]
    public async Task APreviewNamesTheFilesThatWouldNeedSettling()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("a.txt", "one\n"), ("b.txt", "keep\n"));
        repository.Git("switch", "--create", "work");
        repository.Commit("theirs", ("a.txt", "theirs\n"), ("c.txt", "new\n"));
        repository.Git("switch", "main");
        repository.Commit("ours", ("a.txt", "ours\n"));

        var preview = await repository.Merger.PreviewAsync(repository.Root, "work", "HEAD", Stop);

        Assert.Equal(GitMergeVerdict.Conflicts, preview.Verdict);
        Assert.Equal("a.txt", Assert.Single(preview.Paths));

        // Nothing was touched by asking, so the merge is still ahead of the person.
        Assert.Equal("ours\n", repository.Read("a.txt"));
        Assert.Equal(GitMergeState.None, await repository.Conflicts.ReadStateAsync(repository.Root, Stop));
    }

    [Fact]
    public async Task APreviewOfAMergeWithNothingInTheWayIsClean()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("a.txt", "one\n"));
        repository.Git("switch", "--create", "work");
        repository.Commit("theirs", ("c.txt", "new\n"));
        repository.Git("switch", "main");
        repository.Commit("ours", ("b.txt", "mine\n"));

        var preview = await repository.Merger.PreviewAsync(repository.Root, "work", "HEAD", Stop);

        Assert.Equal(GitMergeVerdict.Clean, preview.Verdict);
        Assert.Empty(preview.Paths);
    }

    // Two histories with nothing in common have no merge base, and merge-tree treats that as
    // an error rather than an answer.
    [Fact]
    public async Task APreviewOfHistoriesWithNothingInCommonAnswersNothing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("switch", "--orphan", "elsewhere");
        repository.Commit("apart", ("two.txt", "two\n"));
        repository.Git("switch", "main");

        var preview = await repository.Merger.PreviewAsync(repository.Root, "elsewhere", "HEAD", Stop);

        Assert.Equal(GitMergeVerdict.Unknown, preview.Verdict);
        Assert.False(preview.IsClean);
    }

    [Fact]
    public async Task ABranchIsItsOwnAncestorAndABranchThatMovedAheadIsNot()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("branch", "baseline");
        repository.Commit("mine", ("two.txt", "two\n"));

        Assert.True(await repository.Merger.IsAncestorAsync(repository.Root, "baseline", "HEAD", Stop));
        Assert.True(await repository.Merger.IsAncestorAsync(repository.Root, "HEAD", "HEAD", Stop));
        Assert.False(await repository.Merger.IsAncestorAsync(repository.Root, "HEAD", "baseline", Stop));

        // A revision that does not resolve is false rather than a throw, since a caller
        // asking this is deciding whether to offer a button.
        Assert.False(await repository.Merger.IsAncestorAsync(repository.Root, "nowhere", "HEAD", Stop));
    }
}
