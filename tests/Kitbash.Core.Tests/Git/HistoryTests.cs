using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>Reading commits, including the ones whose message would break a naive format.</summary>
public class HistoryTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CommitsComeBackNewestFirstWithTheirWholeMessage()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("first", ("one.txt", "one\n"));
        repository.Commit("second", ("two.txt", "two\n"));

        var commits = await repository.History.ReadAsync(repository.Root, new GitHistoryQuery(), Stop);

        Assert.Equal(2, commits.Count);
        Assert.Equal("second", commits[0].Subject);
        Assert.Equal("first", commits[1].Subject);

        Assert.Equal("Kitbash Tests", commits[0].AuthorName);
        Assert.Equal("tests@example.invalid", commits[0].AuthorEmail);
        Assert.Equal(40, commits[0].Hash.Length);
        Assert.Equal([commits[1].Hash], commits[0].Parents);
        Assert.Empty(commits[1].Parents);
        Assert.NotEqual(default, commits[0].AuthoredWhen);
    }

    // A subject and a body are two fields, and a body holding a blank line is where a reader
    // that splits the message on the first gap goes wrong.
    [Fact]
    public async Task ABodyWithBlankLinesInItSurvives()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Write("one.txt", "one\n");
        repository.Git("add", "--all", "--", ".");
        repository.Git("commit", "--message", "the subject\n\nfirst paragraph\n\nsecond paragraph");

        var commit = Assert.Single(await repository.History.ReadAsync(
            repository.Root, new GitHistoryQuery(), Stop));

        Assert.Equal("the subject", commit.Subject);
        Assert.Equal("first paragraph\n\nsecond paragraph", commit.Body);
        Assert.Equal("the subject\n\nfirst paragraph\n\nsecond paragraph", commit.Message);
    }

    [Fact]
    public async Task AMergeIsMarkedAndCarriesBothParents()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));
        repository.Git("switch", "--create", "side");
        repository.Commit("on the side", ("side.txt", "side\n"));
        repository.Git("switch", "main");
        repository.Commit("on main", ("main.txt", "main\n"));
        repository.Git("merge", "--no-ff", "--no-edit", "side");

        var commits = await repository.History.ReadAsync(repository.Root, new GitHistoryQuery(), Stop);

        Assert.True(commits[0].IsMerge);
        Assert.Equal(2, commits[0].Parents.Count);
    }

    [Fact]
    public async Task TheHeadIsDecoratedWithTheBranchPointingAtIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        var commit = Assert.Single(await repository.History.ReadAsync(
            repository.Root, new GitHistoryQuery(), Stop));

        Assert.Contains("HEAD -> main", commit.Refs);
    }

    [Fact]
    public async Task ALimitAndASkipPageThroughTheHistory()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        for (var i = 1; i <= 5; i++)
        {
            repository.Commit("commit " + i, ("one.txt", "version " + i + "\n"));
        }

        var page = await repository.History.ReadAsync(
            repository.Root, new GitHistoryQuery(Limit: 2, Skip: 1), Stop);

        Assert.Equal(2, page.Count);
        Assert.Equal("commit 4", page[0].Subject);
        Assert.Equal("commit 3", page[1].Subject);
    }

    [Fact]
    public async Task APathNarrowsTheWalkToTheCommitsThatTouchedIt()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("touched one", ("one.txt", "one\n"));
        repository.Commit("touched two", ("two.txt", "two\n"));
        repository.Commit("touched one again", ("one.txt", "one again\n"));

        var commits = await repository.History.ReadAsync(
            repository.Root, new GitHistoryQuery() { Path = "one.txt" }, Stop);

        Assert.Equal(2, commits.Count);
        Assert.DoesNotContain(commits, c => c.Subject == "touched two");
    }

    [Fact]
    public async Task ARepositoryWithNoCommitsAnswersWithNothingRatherThanThrowing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        Assert.Empty(await repository.History.ReadAsync(repository.Root, new GitHistoryQuery(), Stop));
        Assert.Null(await repository.History.ReadOneAsync(repository.Root, "HEAD", Stop));
    }

    [Fact]
    public async Task OneCommitIsReadByAnyRevision()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("first", ("one.txt", "one\n"));
        repository.Commit("second", ("two.txt", "two\n"));

        var commit = await repository.History.ReadOneAsync(repository.Root, "HEAD~1", Stop);

        Assert.NotNull(commit);
        Assert.Equal("first", commit.Subject);
    }
}
