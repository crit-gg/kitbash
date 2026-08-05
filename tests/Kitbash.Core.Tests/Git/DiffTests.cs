using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>Which paths differ, over each of the things that can be compared.</summary>
public class DiffTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StagedAndUnstagedAreTwoDifferentAnswers()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"), ("two.txt", "two\n"));

        repository.Write("one.txt", "one changed\n");
        repository.Git("add", "--all", "--", ".");
        repository.Write("two.txt", "two changed\n");

        var staged = await repository.Diffs.ReadChangesAsync(repository.Root, GitDiffScope.Staged, Stop);
        var unstaged = await repository.Diffs.ReadChangesAsync(repository.Root, GitDiffScope.Unstaged, Stop);
        var both = await repository.Diffs.ReadChangesAsync(repository.Root, GitDiffScope.Uncommitted, Stop);

        Assert.Equal("one.txt", Assert.Single(staged).Path);
        Assert.Equal("two.txt", Assert.Single(unstaged).Path);
        Assert.Equal(2, both.Count);
    }

    [Fact]
    public async Task EveryKindOfChangeIsNamedByItsOwnLetter()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit(
            "start",
            ("kept.txt", "kept\n"),
            ("changed.txt", "before\n"),
            ("removed.txt", "removed\n"),
            ("moved.txt", TestRepository.Lines(30)));

        repository.Write("changed.txt", "after\n");
        repository.Delete("removed.txt");
        repository.Write("added.txt", "added\n");
        repository.Git("mv", "moved.txt", "elsewhere.txt");
        repository.Git("add", "--all", "--", ".");

        var changes = await repository.Diffs.ReadChangesAsync(repository.Root, GitDiffScope.Staged, Stop);

        Assert.Equal(GitChangeKind.Modified, Assert.Single(changes, c => c.Path == "changed.txt").Kind);
        Assert.Equal(GitChangeKind.Deleted, Assert.Single(changes, c => c.Path == "removed.txt").Kind);
        Assert.Equal(GitChangeKind.Added, Assert.Single(changes, c => c.Path == "added.txt").Kind);
        Assert.DoesNotContain(changes, c => c.Path == "kept.txt");

        var moved = Assert.Single(changes, c => c.Path == "elsewhere.txt");

        Assert.Equal(GitChangeKind.Renamed, moved.Kind);
        Assert.Equal("moved.txt", moved.OldPath);
        Assert.Equal(100, moved.Similarity);
    }

    // Splitting on the null byte is the whole reason for the -z form, so a path with a
    // newline in it is the case that proves it was used.
    [Fact]
    public async Task APathWithANewlineInItIsStillOnePath()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no newline in a file name");

        repository.Commit("start", ("plain.txt", "one\n"));
        repository.Write("odd\nname.txt", "two\n");
        repository.Git("add", "--all", "--", ".");

        var changes = await repository.Diffs.ReadChangesAsync(repository.Root, GitDiffScope.Staged, Stop);

        Assert.Contains(changes, c => c.Path == "odd\nname.txt");
        Assert.Single(changes);
    }

    [Fact]
    public async Task ARangeComparesTwoRevisions()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("first", ("one.txt", "one\n"));
        repository.Commit("second", ("two.txt", "two\n"));
        repository.Commit("third", ("three.txt", "three\n"));

        var changes = await repository.Diffs.ReadChangesAsync(
            repository.Root, GitDiffScope.Between("HEAD~2", "HEAD"), Stop);

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c.Path == "two.txt");
        Assert.Contains(changes, c => c.Path == "three.txt");
    }

    [Fact]
    public async Task OneCommitIsComparedWithWhatCameBeforeIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("first", ("one.txt", "one\n"));
        repository.Commit("second", ("two.txt", "two\n"));

        var changes = await repository.Diffs.ReadChangesAsync(
            repository.Root, GitDiffScope.Commit("HEAD"), Stop);

        Assert.Equal("two.txt", Assert.Single(changes).Path);
    }

    // The first commit has nothing before it, and a client that shows a commit has to show
    // that one too.
    [Fact]
    public async Task TheFirstCommitReadsAsEverythingInItBeingAdded()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("first", ("one.txt", "one\n"), ("two.txt", "two\n"));

        var changes = await repository.Diffs.ReadChangesAsync(
            repository.Root, GitDiffScope.Commit("HEAD"), Stop);

        Assert.Equal(2, changes.Count);
        Assert.All(changes, c => Assert.Equal(GitChangeKind.Added, c.Kind));
    }

    // Without asking for the first parent alone, git writes a merge once per parent and the
    // same file arrives twice.
    [Fact]
    public async Task AMergeCommitReadsAsWhatItBroughtIn()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("switch", "--create", "side");
        repository.Commit("on the side", ("side.txt", "side\n"));
        repository.Git("switch", "main");
        repository.Commit("on main", ("main.txt", "main\n"));
        repository.Git("merge", "--no-ff", "--no-edit", "side");

        var changes = await repository.Diffs.ReadChangesAsync(
            repository.Root, GitDiffScope.Commit("HEAD"), Stop);

        Assert.Equal("side.txt", Assert.Single(changes).Path);
    }

    [Fact]
    public async Task AFileThatDoesNotDifferHasNoPatch()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.Null(await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", null, Stop));
    }

    [Fact]
    public async Task TheContextAroundAChangeIsAsWideAsAsked()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", TestRepository.Lines(40)));
        repository.Write("one.txt", TestRepository.Lines(40, (20, "CHANGED")));

        var narrow = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", new GitDiffOptions(Context: 1), Stop);

        var wide = await repository.Diffs.ReadPatchAsync(
            repository.Root, GitDiffScope.Unstaged, "one.txt", new GitDiffOptions(Context: 8), Stop);

        Assert.NotNull(narrow);
        Assert.NotNull(wide);
        // One line taken out and one put in, with the asked for context either side.
        Assert.Equal(4, narrow.Hunks[0].Lines.Count);
        Assert.Equal(18, wide.Hunks[0].Lines.Count);
    }
}
