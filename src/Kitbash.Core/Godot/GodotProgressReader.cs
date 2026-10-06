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

    /// <summary>The task Godot runs to import what the scan found.</summary>
    private const string ImportTask = "reimport";

    private string _task = string.Empty;
    private int _total;
    private int _done;
    private int _largest;
    private string? _lastItem;

    /// <summary>True once Godot has said its import ran to the end.</summary>
    public bool ImportFinished { get; private set; }

    /// <summary>What the last readable line said, or null when it said nothing useful.</summary>
    public GodotLaunchStep? Read(string line)
    {
        if (line is null)
        {
            return null;
        }

        var text = Colours().Replace(line, string.Empty);

        if (Done().Match(text) is { Success: true } done && done.Groups[1].Value == ImportTask)
        {
            ImportFinished = true;

            return null;
        }

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
            _lastItem = null;
        }

        // A phase says how many steps it has before it takes any of them.
        if (Started().Match(message) is { Success: true } started)
        {
            _total = int.Parse(started.Groups[2].ValueSpan);
            _done = 0;
            _lastItem = null;
            _largest = Math.Max(_largest, _total);

            return null;
        }

        // A short phase says nothing, so the bar holds what the work left it at rather
        // than starting over for the few steps of startup on either side.
        if (_total is > 0 and <= WorthShowing)
        {
            return null;
        }

        // Godot narrates in sentences ending in dots and names an item otherwise. Only an
        // item is a step of the work, so a phase that narrates and names nothing is silent.
        if (message.Length == 0 || message.EndsWith("...", StringComparison.Ordinal))
        {
            return null;
        }

        // Godot prints the same line again each second it waits on one item.
        var item = $"{step.Groups[1].Value}|{message}";

        if (item == _lastItem)
        {
            return null;
        }

        _lastItem = item;
        _done++;

        var fraction = _total > 0
            ? Math.Min(_done, _total) / (double)_total
            : int.Parse(step.Groups[1].ValueSpan) / 100d;

        return new GodotLaunchStep(StageOf(task), $"{Count()}  {message}".TrimStart(), fraction);
    }

    private static GodotLaunchStage StageOf(string task) =>
        task == ScanTask ? GodotLaunchStage.Scanning : GodotLaunchStage.Importing;

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

    [GeneratedRegex(@"^\[\s*DONE\s*\]\s*([A-Za-z_][A-Za-z0-9_]*)\s*$")]
    private static partial Regex Done();

    [GeneratedRegex(@"^Started\s+(.*?)\s*\((\d+)\s+steps?\)$")]
    private static partial Regex Started();
}
