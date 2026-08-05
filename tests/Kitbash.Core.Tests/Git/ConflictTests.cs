using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// The three versions a conflict leaves in the index. This is what a semantic merge is fed
/// with, so reading all three exactly is the point rather than reading the marked up file.
/// </summary>
public class ConflictTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    /// <summary>A repository stopped part way through a merge, with one file in conflict.</summary>
    private static TestRepository Conflicted()
    {
        var repository = new TestRepository();

        repository.Commit("start", ("one.txt", "the original\n"));

        repository.Git("switch", "--create", "side");
        repository.Commit("theirs", ("one.txt", "their version\n"));

        repository.Git("switch", "main");
        repository.Commit("ours", ("one.txt", "our version\n"));

        // Refuses, which is the point.
        _ = repository.Try("merge", "--no-edit", "side");

        return repository;
    }

    [Fact]
    public async Task AllThreeVersionsComeBackWithTheirOwnContent()
    {
        using var repository = Conflicted();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        var conflict = Assert.Single(await repository.Conflicts.ReadAsync(repository.Root, Stop));

        Assert.Equal("one.txt", conflict.Path);
        Assert.NotNull(conflict.Base);
        Assert.NotNull(conflict.Ours);
        Assert.NotNull(conflict.Theirs);
        Assert.False(conflict.IsDeletion);
        Assert.False(conflict.IsBothAdded);

        Assert.Equal(
            "the original\n",
            await repository.Conflicts.ReadVersionAsync(
                repository.Root, conflict, GitConflictSide.Base, Stop));

        Assert.Equal(
            "our version\n",
            await repository.Conflicts.ReadVersionAsync(
                repository.Root, conflict, GitConflictSide.Ours, Stop));

        Assert.Equal(
            "their version\n",
            await repository.Conflicts.ReadVersionAsync(
                repository.Root, conflict, GitConflictSide.Theirs, Stop));
    }

    [Fact]
    public async Task AMergeInProgressIsSaidToBeOne()
    {
        using var repository = Conflicted();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        Assert.Equal(GitMergeState.Merging, await repository.Conflicts.ReadStateAsync(repository.Root, Stop));
    }

    [Fact]
    public async Task ARepositoryInTheMiddleOfNothingSaysSo()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.Equal(GitMergeState.None, await repository.Conflicts.ReadStateAsync(repository.Root, Stop));
        Assert.Empty(await repository.Conflicts.ReadAsync(repository.Root, Stop));
    }

    // A rebase writes CHERRY_PICK_HEAD as well, so asking about that one first would call
    // every stopped rebase a cherry pick.
    [Fact]
    public async Task ARebaseThatStoppedIsSaidToBeARebase()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "the original\n"));
        repository.Git("switch", "--create", "side");
        repository.Commit("theirs", ("one.txt", "their version\n"));
        repository.Git("switch", "main");
        repository.Commit("ours", ("one.txt", "our version\n"));
        repository.Git("switch", "side");

        _ = repository.Try("rebase", "main");

        Assert.Equal(GitMergeState.Rebasing, await repository.Conflicts.ReadStateAsync(repository.Root, Stop));
    }

    [Fact]
    public async Task TakingOneSideSettlesThePathInTheWorkingTreeAndTheIndex()
    {
        using var repository = Conflicted();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        var taken = await repository.Conflicts.TakeAsync(
            repository.Root, ["one.txt"], GitConflictSide.Theirs, Stop);

        Assert.True(taken.Succeeded, taken.Message);
        Assert.Equal("their version\n", repository.Read("one.txt"));
        Assert.Empty(await repository.Conflicts.ReadAsync(repository.Root, Stop));
    }

    [Fact]
    public async Task WritingAnAnswerAndSayingItIsSettledClearsTheConflict()
    {
        using var repository = Conflicted();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Write("one.txt", "the version somebody wrote by hand\n");

        var resolved = await repository.Conflicts.ResolveAsync(repository.Root, ["one.txt"], Stop);

        Assert.True(resolved.Succeeded, resolved.Message);
        Assert.Empty(await repository.Conflicts.ReadAsync(repository.Root, Stop));

        var committed = await repository.Committer.CommitAsync(repository.Root, "settled", null, Stop);

        Assert.True(committed.Succeeded, committed.Message);
    }

    [Fact]
    public async Task AbandoningAMergePutsTheWorkingTreeBack()
    {
        using var repository = Conflicted();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        var stopped = await repository.Conflicts.AbortAsync(repository.Root, Stop);

        Assert.True(stopped.Succeeded, stopped.Message);
        Assert.Equal("our version\n", repository.Read("one.txt"));
        Assert.Equal(GitMergeState.None, await repository.Conflicts.ReadStateAsync(repository.Root, Stop));
    }

    // One side deleted the file and the other changed it. There is no version to take on
    // the deleting side, so a merge tool that assumes three of everything breaks here.
    [Fact]
    public async Task ADeleteAgainstAnEditHasOnlyTwoVersions()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "the original\n"));

        repository.Git("switch", "--create", "side");
        repository.Delete("one.txt");
        repository.Git("add", "--all", "--", ".");
        repository.Git("commit", "--message", "gone");

        repository.Git("switch", "main");
        repository.Commit("changed", ("one.txt", "our version\n"));

        _ = repository.Try("merge", "--no-edit", "side");

        var conflict = Assert.Single(await repository.Conflicts.ReadAsync(repository.Root, Stop));

        Assert.NotNull(conflict.Base);
        Assert.NotNull(conflict.Ours);
        Assert.Null(conflict.Theirs);
        Assert.True(conflict.IsDeletion);

        Assert.Null(await repository.Conflicts.ReadVersionAsync(
            repository.Root, conflict, GitConflictSide.Theirs, Stop));
    }

    // Both branches added a file of the same name, so there is nothing they started from.
    [Fact]
    public async Task TwoSidesAddingTheSameNameHaveNoBase()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("other.txt", "other\n"));

        repository.Git("switch", "--create", "side");
        repository.Commit("theirs", ("both.txt", "their version\n"));

        repository.Git("switch", "main");
        repository.Commit("ours", ("both.txt", "our version\n"));

        _ = repository.Try("merge", "--no-edit", "side");

        var conflict = Assert.Single(await repository.Conflicts.ReadAsync(repository.Root, Stop));

        Assert.Null(conflict.Base);
        Assert.True(conflict.IsBothAdded);
    }

    [Fact]
    public async Task EveryConflictedPathIsListedOnce()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"), ("two.txt", "two\n"));

        repository.Git("switch", "--create", "side");
        repository.Commit("theirs", ("one.txt", "their one\n"), ("two.txt", "their two\n"));

        repository.Git("switch", "main");
        repository.Commit("ours", ("one.txt", "our one\n"), ("two.txt", "our two\n"));

        _ = repository.Try("merge", "--no-edit", "side");

        var conflicts = await repository.Conflicts.ReadAsync(repository.Root, Stop);

        Assert.Equal(2, conflicts.Count);
        Assert.Equal(["one.txt", "two.txt"], conflicts.Select(c => c.Path).Order());
    }
}
