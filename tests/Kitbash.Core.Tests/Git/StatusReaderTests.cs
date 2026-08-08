namespace Kitbash.Core.Tests.Git;

/// <summary>
/// The status reader the launcher's strip already used. Nothing here is new work. It is
/// what the process runner gaining an input had to leave untouched, written down so a later
/// change to that runner cannot quietly move it.
/// </summary>
public class StatusReaderTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ACleanRepositoryReadsAsCleanOnItsBranch()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        var status = await repository.Status.ReadAsync(repository.Root, Stop);

        Assert.NotNull(status);
        Assert.Equal("main", status.Branch);
        Assert.False(status.IsDetached);
        Assert.False(status.HasUpstream);
        Assert.True(status.IsClean);
    }

    [Fact]
    public async Task StagedAndModifiedAreCountedApart()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"), ("two.txt", "two\n"));

        repository.Write("one.txt", "staged\n");
        repository.Git("add", "--all", "--", ".");
        repository.Write("two.txt", "modified\n");

        var status = await repository.Status.ReadAsync(repository.Root, Stop);

        Assert.NotNull(status);
        Assert.Equal(1, status.Staged);
        Assert.Equal(1, status.Modified);
        Assert.Equal(0, status.Conflicted);
        Assert.False(status.IsClean);
    }

    [Fact]
    public async Task AConflictIsCounted()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "the original\n"));
        repository.Git("switch", "--create", "side");
        repository.Commit("theirs", ("one.txt", "their version\n"));
        repository.Git("switch", "main");
        repository.Commit("ours", ("one.txt", "our version\n"));

        _ = repository.Try("merge", "--no-edit", "side");

        var status = await repository.Status.ReadAsync(repository.Root, Stop);

        Assert.NotNull(status);
        Assert.Equal(1, status.Conflicted);
    }

    [Fact]
    public async Task AnUpstreamAndHowFarApartTheyAreAreBothRead()
    {
        using var remote = new TestRepository(bare: true);
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("remote", "add", "origin", remote.Root);
        repository.Git("push", "--set-upstream", "origin", "main");
        repository.Commit("ahead by one", ("two.txt", "two\n"));

        var status = await repository.Status.ReadAsync(repository.Root, Stop);

        Assert.NotNull(status);
        Assert.True(status.HasUpstream);
        Assert.Equal(1, status.Ahead);
        Assert.Equal(0, status.Behind);
    }

    [Fact]
    public async Task ADetachedHeadReadsAsTheShortCommit()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("first", ("one.txt", "one\n"));
        repository.Commit("second", ("two.txt", "two\n"));
        repository.Git("switch", "--detach", "HEAD~1");

        var status = await repository.Status.ReadAsync(repository.Root, Stop);

        Assert.NotNull(status);
        Assert.True(status.IsDetached);
        Assert.Equal(7, status.Branch.Length);
    }

    /// <summary>
    /// The head is what says a branch moved, so a reader that only named the branch could
    /// not tell a pull or a merge from nothing happening at all.
    /// </summary>
    [Fact]
    public async Task TheHeadCommitIsReadAndMovesWithTheBranch()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("first", ("one.txt", "one\n"));

        var first = await repository.Status.ReadAsync(repository.Root, Stop);

        Assert.NotNull(first);
        Assert.Equal(40, first.Commit.Length);

        repository.Commit("second", ("two.txt", "two\n"));

        var second = await repository.Status.ReadAsync(repository.Root, Stop);

        Assert.NotNull(second);
        Assert.Equal(second.Branch, first.Branch);
        Assert.NotEqual(second.Commit, first.Commit);
    }

    /// <summary>Git writes "(initial)" for a head that is on no commit yet.</summary>
    [Fact]
    public async Task ARepositoryWithNoCommitsNamesNoCommit()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        var status = await repository.Status.ReadAsync(repository.Root, Stop);

        Assert.NotNull(status);
        Assert.Equal("", status.Commit);
        Assert.False(status.IsDetached);
    }

    [Fact]
    public async Task AFolderThatIsNotARepositoryAnswersWithNothing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        var outside = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(outside);

        try
        {
            Assert.Null(await repository.Status.ReadAsync(outside, Stop));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task GitIsAskedWhereItKeepsTheRepositoryRatherThanBeingGuessedAt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        var status = await repository.Status.ReadAsync(repository.Root, Stop);

        Assert.NotNull(status);
        Assert.True(Directory.Exists(status.Places.GitDirectory));
        Assert.Contains(status.Places.GitDirectory, status.Places.Watchable);

        // Every path git reports is relative to this, so it is what turns one into a file
        // that can be opened.
        Assert.True(Directory.Exists(status.Places.WorkTree));
        Assert.True(File.Exists(Path.Combine(status.Places.WorkTree, "one.txt")));
    }

    // A workspace can sit below the repository root, so the folder git was run in is not
    // the folder its paths are relative to.
    [Fact]
    public async Task TheWorkTreeIsTheTopRatherThanTheFolderGitRanIn()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("nested/deep/one.txt", "one\n"));

        var inside = Path.Combine(repository.Root, "nested", "deep");
        var status = await repository.Status.ReadAsync(inside, Stop);

        Assert.NotNull(status);
        Assert.True(File.Exists(Path.Combine(status.Places.WorkTree, "nested", "deep", "one.txt")));
    }
}
