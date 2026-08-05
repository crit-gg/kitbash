using Kitbash.Core.Git;

namespace Kitbash.Core.Tests.Git;

/// <summary>Making commits, and the message surviving whatever a person typed into a box.</summary>
public class CommitTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WhatTheIndexHoldsBecomesACommit()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Write("one.txt", "one\n");
        repository.Git("add", "--all", "--", ".");

        var made = await repository.Committer.CommitAsync(repository.Root, "the first one", null, Stop);

        Assert.True(made.Succeeded, made.Message);

        var commit = Assert.Single(await repository.History.ReadAsync(
            repository.Root, new GitHistoryQuery(), Stop));

        Assert.Equal("the first one", commit.Subject);
    }

    // A message goes in through git's input, so nothing in it needs quoting and nothing in
    // it is treated as an option.
    [Theory]
    [InlineData("a message with \"quotes\" in it")]
    [InlineData("a message with 'single quotes'")]
    [InlineData("--not-an-option")]
    [InlineData("a message with a $variable and a `backtick`")]
    [InlineData("a message with a % and a ^ and an &")]
    public async Task AMessageIsTakenExactlyAsItWasWritten(string message)
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Write("one.txt", "one\n");
        repository.Git("add", "--all", "--", ".");

        var made = await repository.Committer.CommitAsync(repository.Root, message, null, Stop);

        Assert.True(made.Succeeded, made.Message);

        var commit = Assert.Single(await repository.History.ReadAsync(
            repository.Root, new GitHistoryQuery(), Stop));

        Assert.Equal(message, commit.Subject);
    }

    // Git strips lines starting with a hash out of a message by default, and a line like
    // that is ordinary text when it was typed rather than taken from an editor template.
    [Fact]
    public async Task ALineStartingWithAHashIsKept()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Write("one.txt", "one\n");
        repository.Git("add", "--all", "--", ".");

        const string Message = "the subject\n\n# not a comment\nordinary line";

        var made = await repository.Committer.CommitAsync(repository.Root, Message, null, Stop);

        Assert.True(made.Succeeded, made.Message);

        var commit = Assert.Single(await repository.History.ReadAsync(
            repository.Root, new GitHistoryQuery(), Stop));

        Assert.Contains("# not a comment", commit.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAmendReplacesTheLastCommitRatherThanAddingOne()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("the wrong words", ("one.txt", "one\n"));

        var made = await repository.Committer.CommitAsync(
            repository.Root, "the right words", new GitCommitOptions { Amend = true }, Stop);

        Assert.True(made.Succeeded, made.Message);

        var commit = Assert.Single(await repository.History.ReadAsync(
            repository.Root, new GitHistoryQuery(), Stop));

        Assert.Equal("the right words", commit.Subject);
    }

    [Fact]
    public async Task CommittingWithNothingStagedIsRefused()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        var made = await repository.Committer.CommitAsync(repository.Root, "nothing to say", null, Stop);

        Assert.False(made.Succeeded);
        Assert.NotEqual("", made.Message);
    }

    [Fact]
    public async Task TheIdentityIsWhatGitWouldRecord()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        var identity = await repository.Committer.ReadIdentityAsync(repository.Root, Stop);

        Assert.NotNull(identity);
        Assert.Equal("Kitbash Tests", identity.Name);
        Assert.Equal("tests@example.invalid", identity.Email);
        Assert.Equal("Kitbash Tests <tests@example.invalid>", identity.ToString());
    }

    // A person who has never set a name cannot commit, and a client has to say so before
    // offering the button rather than after git refuses.
    [Fact]
    public async Task NoIdentityIsAnAnswerRatherThanAGuess()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        // Set to nothing rather than removed. Removing it would let the machine's own
        // setting answer, and this has to be the same test on every machine.
        repository.Git("config", "user.email", "");

        Assert.Null(await repository.Committer.ReadIdentityAsync(repository.Root, Stop));
    }
}
