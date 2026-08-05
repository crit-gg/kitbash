namespace Kitbash.Core.Tests.Git;

/// <summary>
/// One file as a revision holds it. A client showing what a change did needs both sides
/// whole, which a diff of the lines between them cannot give.
/// </summary>
public class BlobTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AFileIsReadAtWhicheverRevisionIsAskedFor()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "first\n"));
        repository.Commit("second", ("one.txt", "second\n"));

        repository.Write("one.txt", "staged\n");
        repository.Git("add", "one.txt");
        repository.Write("one.txt", "on disk\n");

        Assert.Equal("second\n", await repository.Blobs.ReadTextAsync(repository.Root, "HEAD", "one.txt", Stop));
        Assert.Equal("first\n", await repository.Blobs.ReadTextAsync(repository.Root, "HEAD~1", "one.txt", Stop));

        // An empty revision is the index, which is what a commit would take.
        Assert.Equal("staged\n", await repository.Blobs.ReadTextAsync(repository.Root, "", "one.txt", Stop));
    }

    // A file one side never had is not a failure, it is what an addition looks like.
    [Fact]
    public async Task APathARevisionDoesNotHoldAnswersNothing()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "first\n"));

        Assert.Null(await repository.Blobs.ReadTextAsync(repository.Root, "HEAD", "nowhere.txt", Stop));
        Assert.Null(await repository.Blobs.ReadTextAsync(repository.Root, "nosuchrev", "one.txt", Stop));
    }

    // The path goes in as one argument, so nothing about it can be read as an option and
    // nothing needs quoting.
    [Fact]
    public async Task APathHoldingASpaceIsReadWhole()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("art/hurt flash.tres", "[gd_resource]\n"));

        Assert.Equal(
            "[gd_resource]\n",
            await repository.Blobs.ReadTextAsync(repository.Root, "HEAD", "art/hurt flash.tres", Stop));
    }
}
