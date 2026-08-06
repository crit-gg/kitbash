using Kitbash.Core.Platform;
using Kitbash.Tools;

namespace Kitbash.Tests;

/// <summary>
/// A real script, started by the real runner, reporting to a real progress. This is the
/// claim the whole thing rests on, so it is measured rather than faked.
/// </summary>
public sealed class ToolScriptRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-script-{Guid.NewGuid():N}");

    public ToolScriptRunnerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Both forms arrive, a plain line becomes a log line, and the code comes back.</summary>
    [Fact]
    public async Task AScriptReportsInBothFormsAndSaysHowItEnded()
    {
        var tool = Script(
            """
            echo "@kitbash stage Reading files"
            echo "@kitbash progress 40"
            echo '{"kitbash":1,"detail":"12 / 30","progress":80}'
            echo "copied foo.png"
            echo "that did not work" >&2
            exit 3
            """);

        var (outcome, steps) = await RunAsync(tool, workspace: null, []);

        Assert.Equal(3, outcome.ExitCode);
        Assert.False(outcome.Worked);

        Assert.Equal("Reading files", steps.Select(step => step.Stage).OfType<string>().Single());
        Assert.Equal("12 / 30", steps.Select(step => step.Detail).OfType<string>().Single());
        Assert.Equal([40, 80], steps.Select(step => step.Progress).OfType<double>());

        Assert.Contains("copied foo.png", steps.Select(step => step.Log));

        var complaint = Assert.Single(steps, step => step.IsError);

        Assert.Equal("that did not work", complaint.Log);
    }

    /// <summary>The workspace goes first, then whatever the form answered, in order.</summary>
    [Fact]
    public async Task TheScriptIsHandedTheWorkspaceAndThenTheForm()
    {
        var tool = Script("""echo "@kitbash detail $*" """);

        var (outcome, steps) = await RunAsync(tool, "/home/a/ws", ["--source", "/art", "--dry-run"]);

        Assert.True(outcome.Worked);
        Assert.Equal(
            "--workspace /home/a/ws --source /art --dry-run",
            steps.Select(step => step.Detail).OfType<string>().Single());
    }

    /// <summary>A path with a space is one argument, since nothing here is ever a command line.</summary>
    [Fact]
    public async Task AnAnswerHoldingASpaceStaysOneArgument()
    {
        var tool = Script("""echo "@kitbash detail [$1][$2]" """);

        var (_, steps) = await RunAsync(tool, workspace: null, ["--source", "/my art/sheets"]);

        Assert.Equal(
            "[--source][/my art/sheets]",
            steps.Select(step => step.Detail).OfType<string>().Single());
    }

    /// <summary>The script runs in its own folder, which is where its own files are.</summary>
    [Fact]
    public async Task AScriptRunsInTheFolderItWasInstalledIn()
    {
        File.WriteAllText(Path.Combine(_root, "beside.txt"), "here");

        var tool = Script(
            """
            if [ -f beside.txt ]; then echo "@kitbash stage found it"; fi
            """);

        var (outcome, steps) = await RunAsync(tool, workspace: null, []);

        Assert.True(outcome.Worked);
        Assert.Equal("found it", steps.Select(step => step.Stage).OfType<string>().Single());
    }

    /// <summary>Cancelling kills the script rather than leaving it running unwatched.</summary>
    [Fact]
    public async Task CancellingStopsTheScript()
    {
        var tool = Script(
            """
            echo "@kitbash stage waiting"
            sleep 30
            """);

        using var cancellation = new CancellationTokenSource();

        var started = new TaskCompletionSource();
        var progress = new Progress<ToolProgressStep>(step =>
        {
            if (step.Stage is not null)
            {
                started.TrySetResult();
            }
        });

        var running = Runner().RunAsync(tool, null, [], progress, cancellation.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => running);
    }

    [Fact]
    public async Task AScriptThatIsNotThereSaysSoRatherThanFailingQuietly()
    {
        var tool = FakeScriptTool.Built(_root, "missing.sh");

        await Assert.ThrowsAsync<ProcessStartException>(
            () => Runner().RunAsync(tool, null, [], new Progress<ToolProgressStep>(), TestContext.Current.CancellationToken));
    }

    private async Task<(ToolRunOutcome Outcome, IReadOnlyList<ToolProgressStep> Steps)> RunAsync(
        InstalledTool tool, string? workspace, IReadOnlyList<string> arguments)
    {
        List<ToolProgressStep> steps = [];

        // Reported from whichever thread read the pipe, so the list is guarded rather than
        // being written from two places at once.
        var progress = new Progress<ToolProgressStep>(step =>
        {
            lock (steps)
            {
                steps.Add(step);
            }
        });

        var outcome = await Runner().RunAsync(
            tool, workspace, arguments, progress, TestContext.Current.CancellationToken);

        // Progress hands its reports to whatever context built it, and a test has none, so
        // the last of them can still be in flight when the process has exited.
        await Task.Delay(50, TestContext.Current.CancellationToken);

        lock (steps)
        {
            return (outcome, [.. steps]);
        }
    }

    private static IToolScriptRunner Runner() =>
        new ToolScriptRunner(new ProcessRunner(new NoBundle()), new ToolProgressReader());

    /// <summary>Writes the script and makes it runnable, which is what an install does.</summary>
    private InstalledTool Script(string body)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "A shell script does not run on Windows.");

        var file = Path.Combine(_root, "run.sh");

        File.WriteAllText(file, $"#!/bin/sh\n{body}\n");

        // The skip above has already returned by here, which the platform analyzer cannot
        // see, so the test is guarded twice.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                file,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return FakeScriptTool.Built(_root, "run.sh");
    }

    /// <summary>Not in a bundle, so there is nothing to take back out of the environment.</summary>
    private sealed class NoBundle : IBundleEnvironment
    {
        public IReadOnlyDictionary<string, string> Outside { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
