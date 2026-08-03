using System.Text.RegularExpressions;

namespace Kitbash.Core.Godot;

/// <summary>
/// Reads the progress Godot prints while it imports, off its standard output.
/// </summary>
public sealed partial class GodotProgressReader
{
    /// <summary>
    /// How many steps a phase needs before its progress is worth showing.
    /// </summary>
    private const int WorthShowing = 10;

    /// <summary>
    /// The task Godot runs to work out what has changed, before importing any of it.
    /// </summary>
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
    private string Detail(string message) =>
        message.EndsWith("...", StringComparison.Ordinal) || message.Length == 0
            ? Count()
            : $"{Count()}  {message}".TrimStart();

    /// <summary>
    /// The step to report once the import has exited, which is a full bar.
    /// </summary>
    public GodotLaunchStep Finished() =>
        new(GodotLaunchStage.Importing, _largest > 0 ? $"{_largest} / {_largest}" : string.Empty, 1);

    /// <summary>The step count. Empty until a phase has declared its total.</summary>
    private string Count() => _total > 0 ? $"{Math.Min(_done, _total)} / {_total}" : string.Empty;

    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex Colours();

    [GeneratedRegex(@"^\[\s*(\d+)%\s*\]\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|\s*(.*)$")]
    private static partial Regex Step();

    [GeneratedRegex(@"^Started\s+(.*?)\s*\((\d+)\s+steps?\)$")]
    private static partial Regex Started();
}
