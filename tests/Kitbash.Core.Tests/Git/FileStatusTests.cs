using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// The list a file tree is drawn from. The two states are separate because one file can be
/// staged and modified at once, and a client has to draw both.
/// </summary>
public class FileStatusTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StagedAndUnstakedChangesToOneFileAreBothReported()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        repository.Write("one.txt", "staged\n");
        repository.Git("add", "--all", "--", ".");
        repository.Write("one.txt", "staged, then changed again\n");

        var file = Assert.Single(await repository.Files.ReadAsync(repository.Root, false, Stop));

        Assert.Equal("one.txt", file.Path);
        Assert.Equal(GitChangeKind.Modified, file.Staged);
        Assert.Equal(GitChangeKind.Modified, file.Unstaged);
    }

    [Fact]
    public async Task AnUntrackedFileIsNamedRatherThanTheFolderHoldingIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("art/textures/new.png", "not really a png\n");

        var file = Assert.Single(await repository.Files.ReadAsync(repository.Root, false, Stop));

        Assert.Equal("art/textures/new.png", file.Path);
        Assert.True(file.IsUntracked);
        Assert.Equal(GitChangeKind.None, file.Staged);
    }

    [Fact]
    public async Task ARenameCarriesBothPathsAndItsScore()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("before.txt", TestRepository.Lines(30)));
        repository.Git("mv", "before.txt", "after.txt");

        var file = Assert.Single(await repository.Files.ReadAsync(repository.Root, false, Stop));

        Assert.Equal("after.txt", file.Path);
        Assert.Equal("before.txt", file.OldPath);
        Assert.Equal(GitChangeKind.Renamed, file.Staged);
        Assert.Equal(100, file.Similarity);
    }

    [Fact]
    public async Task ADeletionIsReported()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"), ("two.txt", "two\n"));
        repository.Delete("two.txt");

        var file = Assert.Single(await repository.Files.ReadAsync(repository.Root, false, Stop));

        Assert.Equal("two.txt", file.Path);
        Assert.Equal(GitChangeKind.Deleted, file.Unstaged);
    }

    [Fact]
    public async Task AConflictedPathIsMarkedAsOne()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "the original\n"));
        repository.Git("switch", "--create", "side");
        repository.Commit("theirs", ("one.txt", "their version\n"));
        repository.Git("switch", "main");
        repository.Commit("ours", ("one.txt", "our version\n"));

        _ = repository.Try("merge", "--no-edit", "side");

        var file = Assert.Single(await repository.Files.ReadAsync(repository.Root, false, Stop));

        Assert.Equal("one.txt", file.Path);
        Assert.True(file.IsUnmerged);
    }

    [Fact]
    public async Task IgnoredFilesAreLeftOutUntilTheyAreAskedFor()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", (".gitignore", "*.tmp\n"));
        repository.Write("scratch.tmp", "ignore me\n");

        Assert.Empty(await repository.Files.ReadAsync(repository.Root, false, Stop));

        var withIgnored = await repository.Files.ReadAsync(repository.Root, true, Stop);

        Assert.True(Assert.Single(withIgnored, f => f.Path == "scratch.tmp").IsIgnored);
    }

    [Fact]
    public async Task APathWithASpaceIsOnePath()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Write("a name with spaces.txt", "two\n");

        var file = Assert.Single(await repository.Files.ReadAsync(repository.Root, false, Stop));

        Assert.Equal("a name with spaces.txt", file.Path);
    }

    [Fact]
    public async Task ACleanRepositoryHasNothingToList()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        Assert.Empty(await repository.Files.ReadAsync(repository.Root, false, Stop));
    }
}
