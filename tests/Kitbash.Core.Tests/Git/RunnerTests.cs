using System.Text;
using Kitbash.Core.Git;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Git;

/// <summary>
/// The shared runner, and the process input it needed. Handing a program its input is what
/// lets a patch and a commit message be any size, so the cases here are the large ones and
/// the ones nobody would ask for on purpose.
/// </summary>
public class RunnerTests
{
    private static CancellationToken Stop => TestContext.Current.CancellationToken;

    // git hash-object reads its input and answers with the name git would store it under,
    // so what it says is proof of exactly which bytes arrived.
    private static string Hash(string text)
    {
        using var sha = System.Security.Cryptography.SHA1.Create();

        var bytes = Encoding.UTF8.GetBytes(text);
        var header = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");

        return Convert.ToHexStringLower(sha.ComputeHash([.. header, .. bytes]));
    }

    [Fact]
    public async Task WhatIsWrittenToAProgramIsWhatItReads()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        const string Text = "the quick brown fox\nsecond line\n";

        var result = await repository.Runner.RunAsync(
            repository.Root,
            GitCommand.Of("hash-object", "--stdin").Reading(Text),
            Stop);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(Hash(Text), result.Output.Trim());
    }

    // A program answers as it goes, so a large input fills its output pipe part way through.
    // Written without reading at the same time, this is where the two sides wait on each
    // other forever.
    [Fact]
    public async Task ALargeInputDoesNotWedge()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        var text = string.Concat(Enumerable.Range(0, 200_000).Select(i => $"line {i}\n"));

        var result = await repository.Runner.RunAsync(
            repository.Root,
            GitCommand.Of("hash-object", "--stdin").Reading(text),
            Stop);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(Hash(text), result.Output.Trim());
    }

    [Fact]
    public async Task TextOutsideAsciiSurvivesBothWays()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        const string Text = "un café です \U0001F600\n";

        var result = await repository.Runner.RunAsync(
            repository.Root,
            GitCommand.Of("hash-object", "--stdin").Reading(Text),
            Stop);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(Hash(Text), result.Output.Trim());
    }

    [Fact]
    public async Task AFolderThatIsNotThereIsAnswerRatherThanAThrow()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        var result = await repository.Runner.RunAsync(
            Path.Combine(repository.Root, "no such folder"), GitCommand.Of("status"), Stop);

        Assert.Equal(GitRunOutcome.Unavailable, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AFolderThatIsNotARepositoryFailsAndSaysWhy()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        var outside = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(outside);

        try
        {
            var result = await repository.Runner.RunAsync(outside, GitCommand.Of("status"), Stop);

            Assert.Equal(GitRunOutcome.Ran, result.Outcome);
            Assert.False(result.Succeeded);
            Assert.NotEqual("", result.Message);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task GivingUpOnALimitIsSaidToBeThatRatherThanAFailure()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        // A hash of nothing, with input never written, so git waits on a pipe that stays
        // open until the limit closes it.
        var result = await repository.Runner.RunAsync(
            repository.Root,
            GitCommand.Of("hash-object", "--stdin").Within(TimeSpan.FromMilliseconds(300)),
            Stop);

        Assert.Equal(GitRunOutcome.TimedOut, result.Outcome);
    }

    // Reading with no input asked for must leave the program's own input alone, or every
    // caller that never wanted it starts waiting.
    [Fact]
    public async Task ARunThatAsksForNoInputStillFinishes()
    {
        using var repository = new TestRepository();
        Assert.SkipUnless(repository.HasGit, "no git on this machine");

        repository.Commit("start", ("one.txt", "one\n"));

        var result = await repository.Runner.RunAsync(
            repository.Root, GitCommand.Of("rev-parse", "HEAD"), Stop);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(40, result.Output.Trim().Length);
    }

    [Fact]
    public async Task AProgramThatIsNotThereIsReportedRatherThanThrowing()
    {
        using var repository = new TestRepository();

        var runner = new ProcessRunner(new NoBundleEnvironment());

        await Assert.ThrowsAsync<ProcessStartException>(() => runner.ReadAsync(
            ProcessRequest.CommandIn(repository.Root, "kitbash-no-such-program", "--version"),
            Stop));
    }

    private sealed class NoBundleEnvironment : IBundleEnvironment
    {
        public IReadOnlyDictionary<string, string> Outside { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
