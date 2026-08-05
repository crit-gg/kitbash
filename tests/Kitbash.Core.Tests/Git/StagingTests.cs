using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// Staging, and the part of it that has to be built rather than asked for: one hunk of a
/// file and not the rest. Every assertion here is made by asking git what it now holds.
/// </summary>
public class StagingTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    /// <summary>What the index holds against the head, read straight from git.</summary>
    private static string Staged(TestRepository repository) =>
        repository.Git("diff", "--no-color", "--cached", "--patch");

    /// <summary>What the working tree holds against the index.</summary>
    private static string Unstaged(TestRepository repository) =>
        repository.Git("diff", "--no-color", "--patch");

    [Fact]
    public async Task AWholePathIsStagedAndTakenBackOut()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("one.txt", "one\ntwo\n");

        Assert.True((await repository.Stager.StageAsync(repository.Root, ["one.txt"], Stop)).Succeeded);
        Assert.Contains("+two", Staged(repository), StringComparison.Ordinal);

        Assert.True((await repository.Stager.UnstageAsync(repository.Root, ["one.txt"], Stop)).Succeeded);
        Assert.Equal("", Staged(repository));
        Assert.Contains("+two", Unstaged(repository), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneHunkGoesInAndTheOthersStayOut()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));

        repository.Write("one.txt", TestRepository.Lines(
            40, (2, "TOP"), (20, "MIDDLE"), (38, "END")));

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop);

        Assert.NotNull(patch);
        Assert.Equal(3, patch.Hunks.Count);

        var staged = await repository.Stager.StageHunksAsync(repository.Root, patch, [1], Stop);

        Assert.True(staged.Succeeded, staged.Message);

        // The index took the middle change and nothing else.
        var inIndex = Staged(repository);

        Assert.Contains("+MIDDLE", inIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("+TOP", inIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("+END", inIndex, StringComparison.Ordinal);

        // The working tree still holds all three, so the other two are still to stage.
        var left = Unstaged(repository);

        Assert.Contains("+TOP", left, StringComparison.Ordinal);
        Assert.Contains("+END", left, StringComparison.Ordinal);
        Assert.DoesNotContain("+MIDDLE", left, StringComparison.Ordinal);

        // And the file on disk was not touched by any of it.
        Assert.Contains("MIDDLE", repository.Read("one.txt"), StringComparison.Ordinal);
        Assert.Contains("TOP", repository.Read("one.txt"), StringComparison.Ordinal);
    }

    // Staging the later hunks and not the first is the case that catches a wrong offset,
    // since every hunk after a skipped one starts somewhere else than the patch says.
    [Fact]
    public async Task SkippingTheFirstHunkStillPutsTheLaterOnesInTheRightPlace()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        // The first hunk grows the file, so everything after it moves.
        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));

        var lines = TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")).Split('\n').ToList();
        lines.Insert(1, "INSERTED ONE");
        lines.Insert(2, "INSERTED TWO");

        repository.Write("one.txt", string.Join('\n', lines));

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop);

        Assert.NotNull(patch);
        Assert.Equal(3, patch.Hunks.Count);

        var staged = await repository.Stager.StageHunksAsync(repository.Root, patch, [1, 2], Stop);

        Assert.True(staged.Succeeded, staged.Message);

        var inIndex = Staged(repository);

        Assert.Contains("+MIDDLE", inIndex, StringComparison.Ordinal);
        Assert.Contains("+END", inIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERTED", inIndex, StringComparison.Ordinal);

        // Git rewrote the index, so the file it now holds is the old one with two lines
        // changed. Reading it back is the proof the offsets landed where they should.
        var text = repository.Git("show", ":one.txt");

        Assert.Equal(TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")), text);
    }

    [Fact]
    public async Task AHunkComesBackOutOfTheIndexOnItsOwn()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));

        repository.Write("one.txt", TestRepository.Lines(
            40, (2, "TOP"), (20, "MIDDLE"), (38, "END")));

        repository.Git("add", "--all", "--", ".");

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Staged, "one.txt", null, Stop);

        Assert.NotNull(patch);
        Assert.Equal(3, patch.Hunks.Count);

        var unstaged = await repository.Stager.UnstageHunksAsync(repository.Root, patch, [0], Stop);

        Assert.True(unstaged.Succeeded, unstaged.Message);

        var inIndex = Staged(repository);

        Assert.DoesNotContain("+TOP", inIndex, StringComparison.Ordinal);
        Assert.Contains("+MIDDLE", inIndex, StringComparison.Ordinal);
        Assert.Contains("+END", inIndex, StringComparison.Ordinal);

        Assert.Equal(
            TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")),
            repository.Git("show", ":one.txt"));
    }

    // Taking the last hunk out is the mirror of skipping the first one going in, and it is
    // the case that fails if the drift is applied to the wrong side of the header.
    [Fact]
    public async Task TakingAnEarlyHunkOutLeavesTheLaterOnesWhereTheyBelong()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));

        var lines = TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")).Split('\n').ToList();
        lines.Insert(1, "INSERTED ONE");
        lines.Insert(2, "INSERTED TWO");

        var whole = string.Join('\n', lines);

        repository.Write("one.txt", whole);
        repository.Git("add", "--all", "--", ".");

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Staged, "one.txt", null, Stop);

        Assert.NotNull(patch);

        var unstaged = await repository.Stager.UnstageHunksAsync(repository.Root, patch, [0], Stop);

        Assert.True(unstaged.Succeeded, unstaged.Message);

        Assert.Equal(
            TestRepository.Lines(40, (20, "MIDDLE"), (38, "END")),
            repository.Git("show", ":one.txt"));
    }

    [Fact]
    public async Task AnUntrackedFileIsReadWithoutStagingIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("new.txt", "alpha\nbeta\n");

        var patch = await repository.Diffs.ReadUntrackedPatchAsync(repository.Root, "new.txt", Stop);

        Assert.NotNull(patch);
        Assert.Equal("new.txt", patch.Path);
        Assert.Equal(GitChangeKind.Added, patch.Kind);
        Assert.Equal(2, patch.AddedLines);

        // Nothing was staged to look at it.
        Assert.Equal("", Staged(repository));
    }

    [Fact]
    public async Task AHunkOfANewFileIsStagedOnceGitIsToldTheFileIsComing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("new.txt", TestRepository.Lines(40, (2, "TOP"), (38, "END")));

        Assert.True((await repository.Stager.BeginTrackingAsync(repository.Root, ["new.txt"], Stop)).Succeeded);

        var patch = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "new.txt", null, Stop);

        Assert.NotNull(patch);

        var staged = await repository.Stager.StageHunksAsync(repository.Root, patch, [0], Stop);

        Assert.True(staged.Succeeded, staged.Message);
        Assert.Contains("+TOP", Staged(repository), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscardingPutsTheIndexVersionBack()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("one.txt", "one\nunwanted\n");

        Assert.True((await repository.Stager.DiscardAsync(repository.Root, ["one.txt"], Stop)).Succeeded);
        Assert.Equal("one\n", repository.Read("one.txt"));
    }

    [Fact]
    public async Task DeletingAnUntrackedFileRemovesIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("scratch.txt", "throw me away\n");

        Assert.True((await repository.Stager.DeleteUntrackedAsync(repository.Root, ["scratch.txt"], Stop)).Succeeded);
        Assert.False(File.Exists(Path.Combine(repository.Root, "scratch.txt")));
        Assert.True(File.Exists(Path.Combine(repository.Root, "one.txt")));
    }

    // A repository with no commits has no HEAD to resolve, which is what restore --staged
    // needs. Unstaging the very first file staged in a fresh repository goes through here.
    [Fact]
    public async Task AFileIsUnstagedInARepositoryWithNoCommitsYet()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Write("one.txt", "one\n");
        repository.Git("add", "--", "one.txt");

        var unstaged = await repository.Stager.UnstageAsync(repository.Root, ["one.txt"], Stop);

        Assert.True(unstaged.Succeeded, unstaged.Message);

        var file = Assert.Single(await repository.Files.ReadAsync(repository.Root, false, Stop));

        Assert.True(file.IsUntracked);
        Assert.True(File.Exists(Path.Combine(repository.Root, "one.txt")));
    }

    // The same for a file git has never had a version of, where there is nothing in the
    // head to put back.
    [Fact]
    public async Task ANewFileIsUnstagedWithoutBeingDeleted()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("two.txt", "two\n");
        repository.Git("add", "--", "two.txt");

        var unstaged = await repository.Stager.UnstageAsync(repository.Root, ["two.txt"], Stop);

        Assert.True(unstaged.Succeeded, unstaged.Message);
        Assert.Equal("two\n", repository.Read("two.txt"));
        Assert.Equal("", Staged(repository));
    }

    [Fact]
    public async Task StagingNothingIsNotAnError()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.True((await repository.Stager.StageAsync(repository.Root, [], Stop)).Succeeded);
    }
}
