using System.Text.RegularExpressions;

namespace Workbench.Core.Godot;

/// <summary>
/// Reads the progress Godot prints while it imports, off its standard output.
/// </summary>
/// <remarks>
/// <para>
/// Measured on 4.7.1 against a 520 asset project. Every step prints one line:
/// </para>
/// <code>
/// [   0% ] reimport | Started (Re)Importing Assets (520 steps)
/// [  45% ] reimport | big_395.png
/// [ DONE ] reimport
/// </code>
/// <para>
/// The percentage and the brackets are plain, and the task name and the message are
/// wrapped in ANSI colour codes, so the codes come off before anything is read. The
/// bracket is the only anchor, which is why an unknown task still reports.
/// </para>
/// <para>
/// **It arrives while the work runs rather than at the end.** Measured with timestamps
/// on the same project: the reimport lines came out at 4.9s, 5.3s, 5.5s and 5.8s of a
/// 5.9s run, so a bar driven by them moves. That is the whole reason this exists.
/// </para>
/// <para>
/// **A line it cannot read is skipped and nothing fails.** Godot owns this format and
/// may change it, and an import that reports nothing is worth far more than one that
/// stops. The dialog falls back to an indeterminate bar on its own when no fraction ever
/// arrives.
/// </para>
/// </remarks>
public sealed partial class GodotProgressReader
{
    /// <summary>
    /// How many steps a phase needs before its progress is worth showing.
    /// </summary>
    /// <remarks>
    /// **Godot runs short phases either side of the import and they would sweep the bar
    /// again.** Measured on a 460 asset import: scanning and importing declare 460 steps
    /// each, while opening the project and loading the editor declare 5, and those two
    /// run first and last. Reporting them left the bar at 16 percent when the import had
    /// finished, which reads as going backwards.
    ///
    /// A count rather than a name, since a name is Godot's own identifier and this is
    /// really the question of whether a phase is the work or the startup around it.
    /// </remarks>
    private const int WorthShowing = 10;

    /// <summary>
    /// The task Godot runs to work out what has changed, before importing any of it.
    /// </summary>
    /// <remarks>
    /// The one place a task name is read, and only to tell two phases of the same work
    /// apart on screen. Anything else long enough to report reads as importing, so a
    /// rename costs a word rather than the progress.
    /// </remarks>
    private const string ScanTask = "_update_scan_actions";

    private string _task = string.Empty;
    private int _total;
    private int _done;
    private int _largest;

    /// <summary>What the last readable line said, or null when it said nothing useful.</summary>
    public GodotLaunchStep? Read(string line)
    {
        if (line is null)
        {
            return null;
        }

        var text = Colours().Replace(line, string.Empty);
        var step = Step().Match(text);

        if (!step.Success)
        {
            return null;
        }

        var task = step.Groups[2].Value;
        var message = step.Groups[3].Value.Trim();

        if (task != _task)
        {
            _task = task;
            _total = 0;
            _done = 0;
        }

        // A phase says how many steps it has before it takes any of them.
        if (Started().Match(message) is { Success: true } started)
        {
            _total = int.Parse(started.Groups[2].ValueSpan);
            _done = 0;
            _largest = Math.Max(_largest, _total);

            return _total > WorthShowing ? new GodotLaunchStep(StageOf(task), Count(), 0) : null;
        }

        // A short phase says nothing, so the bar holds what the work left it at rather
        // than starting over for the few steps of startup on either side.
        if (_total is > 0 and <= WorthShowing)
        {
            return null;
        }

        _done++;

        return new GodotLaunchStep(
            StageOf(task),
            Detail(message),
            int.Parse(step.Groups[1].ValueSpan) / 100d);
    }

    private static GodotLaunchStage StageOf(string task) =>
        task == ScanTask ? GodotLaunchStage.Scanning : GodotLaunchStage.Importing;

    /// <summary>
    /// The count, and the item beside it when the line names one.
    /// </summary>
    /// <remarks>
    /// **Godot's own narration is left out and its items are kept.** The line carries one
    /// or the other, and the two are told apart by the trailing dots: measured over a
    /// whole import, every message that is a sentence ends in them and no file name does.
    /// The narration says nothing a person waiting for their project needs, and the line
    /// it would go on is mono and holds values.
    /// </remarks>
    private string Detail(string message) =>
        message.EndsWith("...", StringComparison.Ordinal) || message.Length == 0
            ? Count()
            : $"{Count()}  {message}".TrimStart();

    /// <summary>
    /// The step to report once the import has exited, which is a full bar.
    /// </summary>
    /// <remarks>
    /// **Godot's last phase declares the whole step count and then takes one step.**
    /// Measured: after importing 460 assets it starts (Re)Importing Assets again for
    /// "Executing post reimport operations", which left the bar at nearly nothing with
    /// the work finished. The import is over by the time this is called, so this says so
    /// rather than leaving the last line Godot happened to print on screen.
    /// </remarks>
    public GodotLaunchStep Finished() =>
        new(GodotLaunchStage.Importing, _largest > 0 ? $"{_largest} / {_largest}" : string.Empty, 1);

    /// <summary>
    /// The count, which the design says is the part people read. Empty until a phase has
    /// said how many steps it has, since a number over nothing is worse than no number.
    /// </summary>
    private string Count() => _total > 0 ? $"{Math.Min(_done, _total)} / {_total}" : string.Empty;

    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex Colours();

    [GeneratedRegex(@"^\[\s*(\d+)%\s*\]\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|\s*(.*)$")]
    private static partial Regex Step();

    [GeneratedRegex(@"^Started\s+(.*?)\s*\((\d+)\s+steps?\)$")]
    private static partial Regex Started();
}
