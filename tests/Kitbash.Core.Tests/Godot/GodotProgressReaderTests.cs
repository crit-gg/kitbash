using Kitbash.Core.Godot;

namespace Kitbash.Core.Tests.Godot;

/// <summary>Reading the progress a headless Godot 4.7.1 import prints, in the form it prints it.</summary>
public class GodotProgressReaderTests
{
    private static readonly string[] Files = ["t1.csv", "t2.csv", "a1.svg", "a2.svg", "a3.svg", "a4.svg", "a5.svg", "a6.svg", "a7.svg", "a8.svg", "a9.svg", "a10.svg"];

    [Fact]
    public void TheCountFollowsTheFilesRatherThanThePrepareLoop()
    {
        var reader = new GodotProgressReader();

        var steps = Read(reader, Reimport(Files));

        Assert.Equal(Files.Length, steps.Count);
        Assert.Equal($"1 / {Files.Length}  t1.csv", steps[0].Detail);
        Assert.Equal($"{Files.Length} / {Files.Length}  a10.svg", steps[^1].Detail);
        Assert.Equal(1d / Files.Length, steps[0].Fraction);
        Assert.Equal(1d, steps[^1].Fraction);
    }

    [Fact]
    public void EachStageSweepsOnceAndNeverMovesBack()
    {
        var reader = new GodotProgressReader();
        var lines = Phase("first_scan_filesystem", "Project initialization", 5, ["Scanning file structure..."])
            .Concat(Phase("_update_scan_actions", "Scanning actions...", Files.Length, Files))
            .Concat(Reimport(Files))
            .Concat(Phase("reimport", "(Re)Importing Assets", Files.Length, ["Executing post-reimport operations..."]))
            .Concat(Phase("loading_editor_layout", "Loading editor", 5, ["Loading editor layout..."]));

        var steps = Read(reader, lines);

        Assert.Equal(
            [GodotLaunchStage.Scanning, GodotLaunchStage.Importing],
            steps.Select(step => step.Stage).Distinct());

        foreach (var stage in steps.GroupBy(step => step.Stage))
        {
            var fractions = stage.Select(step => step.Fraction!.Value).ToList();
            Assert.Equal(fractions.Order(), fractions);
            Assert.Equal(1d, fractions[^1]);
        }
    }

    [Fact]
    public void ALineRepeatedWhileOneFileImportsIsNotAnotherStep()
    {
        var reader = new GodotProgressReader();
        var lines = new[]
        {
            Line("reimport", "Started (Re)Importing Assets (20 steps)", 0, 20),
            Line("reimport", "big.png", 0, 20),
            Line("reimport", "big.png", 0, 20),
            Line("reimport", "big.png", 0, 20),
            Line("reimport", "next.png", 1, 20),
        };

        var steps = Read(reader, lines);

        Assert.Equal(["1 / 20  big.png", "2 / 20  next.png"], steps.Select(step => step.Detail));
    }

    [Fact]
    public void APhaseSaysNothingUntilItNamesAnItem()
    {
        var reader = new GodotProgressReader();

        Assert.Null(reader.Read(Line("reimport", "Started (Re)Importing Assets (20 steps)", 0, 20)));
        Assert.Null(reader.Read(Line("reimport", "Preparing files to reimport...", 0, 20)));
        Assert.Null(reader.Read(Line("reimport", "Executing pre-reimport operations...", 0, 20)));
        Assert.NotNull(reader.Read(Line("reimport", "icon.svg", 0, 20)));
    }

    [Fact]
    public void TheFinishIsAFullBarOverTheLargestPhase()
    {
        var reader = new GodotProgressReader();
        Read(reader, Reimport(Files));

        var finished = reader.Finished();

        Assert.Equal(1d, finished.Fraction);
        Assert.Equal($"{Files.Length} / {Files.Length}", finished.Detail);
    }

    [Fact]
    public void TheImportIsFinishedOnceGodotSaysItsImportIsDone()
    {
        var reader = new GodotProgressReader();
        Read(reader, Reimport(Files));

        Assert.False(reader.ImportFinished);

        reader.Read(Done("reimport"));

        Assert.True(reader.ImportFinished);
    }

    [Theory]
    [InlineData("first_scan_filesystem")]
    [InlineData("_update_scan_actions")]
    [InlineData("loading_editor_layout")]
    public void AnotherTaskBeingDoneIsNotTheImport(string task)
    {
        var reader = new GodotProgressReader();

        reader.Read(Done(task));

        Assert.False(reader.ImportFinished);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Godot Engine v4.7.1.stable.mono.official.a13da4feb - https://godotengine.org")]
    [InlineData("\u001b[92m[ DONE ]\u001b[39m \u001b[1mreimport\u001b[22m")]
    [InlineData("\u001b[0m")]
    public void ALineThatIsNotProgressSaysNothing(string line) =>
        Assert.Null(new GodotProgressReader().Read(line));

    private static List<GodotLaunchStep> Read(GodotProgressReader reader, IEnumerable<string> lines) =>
        lines.Select(reader.Read).OfType<GodotLaunchStep>().ToList();

    // The order reimport_files prints in: a prepare line per file, one narrated step, then each file.
    private static IEnumerable<string> Reimport(string[] files)
    {
        yield return Line("reimport", $"Started (Re)Importing Assets ({files.Length} steps)", 0, files.Length);

        for (var i = 0; i < files.Length; i++)
        {
            yield return Line("reimport", "Preparing files to reimport...", i, files.Length);
        }

        yield return Line("reimport", "Executing pre-reimport operations...", 0, files.Length);

        for (var i = 0; i < files.Length; i++)
        {
            yield return Line("reimport", files[i], i, files.Length);
        }

        yield return Line("reimport", "Finalizing Asset Import...", files.Length, files.Length);
    }

    private static IEnumerable<string> Phase(string task, string label, int total, IReadOnlyList<string> states)
    {
        yield return Line(task, $"Started {label} ({total} steps)", 0, total);

        for (var i = 0; i < states.Count; i++)
        {
            yield return Line(task, states[i], i, total);
        }
    }

    // EditorNode::progress_end_task in cmdline mode, as print_line_rich writes it.
    private static string Done(string task) =>
        $"\u001b[92m[ DONE ]\u001b[39m \u001b[1m{task}\u001b[22m\u001b[0m";

    // EditorNode::progress_task_step in cmdline mode, after print_line_rich has turned its BBCode into ANSI.
    private static string Line(string task, string state, int step, int total) =>
        $"[{(int)(step / (float)(total + 1) * 100),4}% ] \u001b[90m\u001b[1m{task}\u001b[22m | {state}\u001b[39m\u001b[0m";
}
