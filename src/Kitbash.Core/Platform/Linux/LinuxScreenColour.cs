using System.Globalization;
using System.Text.RegularExpressions;

namespace Kitbash.Core.Platform.Linux;

/// <summary>
/// Picking through the desktop portal, which is the only way that works on both Wayland and
/// X11 and on any desktop that implements it. The portal draws the picker and hands back one
/// colour.
/// </summary>
public sealed partial class LinuxScreenColour : IScreenColour
{
    /// <summary>
    /// The portal's own names. Screenshot version 2 is where PickColor arrives, and a
    /// request answers on the Response signal rather than to the call.
    /// </summary>
    private const string Bus = "org.freedesktop.portal.Desktop";
    private const string Path = "/org/freedesktop/portal/desktop";
    private const string Screenshot = "org.freedesktop.portal.Screenshot";
    private const string Tool = "gdbus";

    private readonly IProcessRunner _runner;
    private readonly IExecutableFinder _finder;

    /// <summary>Names one request, so a reply meant for another is not read as ours.</summary>
    private int _asked;

    public LinuxScreenColour(IProcessRunner runner, IExecutableFinder finder)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(finder);

        _runner = runner;
        _finder = finder;
    }

    /// <summary>
    /// Whether the tool that talks to the portal is installed. Whether the portal itself
    /// answers is not asked here, since asking costs a round trip on every draw.
    /// </summary>
    public bool CanPick => _finder.Find(Tool) is not null;

    public async Task<ScreenColour?> PickAsync(CancellationToken cancellation = default)
    {
        if (!CanPick)
        {
            return null;
        }

        var token = string.Create(
            CultureInfo.InvariantCulture, $"kitbash_{Environment.ProcessId}_{Interlocked.Increment(ref _asked)}");

        using var picked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

        ScreenColour? colour = null;

        // The reply is a signal to the connection that asked, and each run of the tool is its
        // own connection, so the answer is read off a monitor rather than off the call.
        var listening = _runner.ReadLinesAsync(
            ProcessRequest.Command(Tool, "monitor", "--session", "--dest", Bus),
            line =>
            {
                if (!line.Contains(token, StringComparison.Ordinal)
                    || !line.Contains("Response", StringComparison.Ordinal))
                {
                    return;
                }

                colour = Read(line);
                picked.Cancel();
            },
            picked.Token);

        try
        {
            var asked = await _runner.ReadAsync(
                ProcessRequest.Command(
                    Tool,
                    "call",
                    "--session",
                    "--dest", Bus,
                    "--object-path", Path,
                    "--method", Screenshot + ".PickColor",
                    string.Empty,
                    "{'handle_token': <'" + token + "'>}"),
                cancellation);

            if (asked.ExitCode != 0)
            {
                await picked.CancelAsync();

                throw new ScreenColourException(Blame(asked));
            }

            await listening;
        }
        catch (OperationCanceledException) when (picked.IsCancellationRequested && !cancellation.IsCancellationRequested)
        {
            // The pick landing is what cancels the monitor, so this is the ordinary path.
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ProcessStartException exception)
        {
            throw new ScreenColourException($"{Tool} could not be started.", exception);
        }

        return colour;
    }

    /// <summary>
    /// What the portal said when it refused. Its own words, since a person copying them into
    /// a search is better served by those than by anything written here.
    /// </summary>
    private static string Blame(ProcessOutput asked)
    {
        var said = string.IsNullOrWhiteSpace(asked.StandardError)
            ? asked.StandardOutput
            : asked.StandardError;

        return string.IsNullOrWhiteSpace(said)
            ? $"The desktop portal refused, and {Tool} exited with {asked.ExitCode}."
            : said.Trim();
    }

    /// <summary>
    /// Reads the colour out of a Response line. A refusal comes back with a code other than
    /// zero and no colour at all, so a line without one is a person changing their mind.
    /// </summary>
    private static ScreenColour? Read(string line)
    {
        var match = Colour().Match(line);

        return match.Success
            && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var red)
            && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var green)
            && double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var blue)
                ? new ScreenColour(red, green, blue)
                : null;
    }

    [GeneratedRegex(@"'color':\s*<\(\s*([-\d.eE+]+),\s*([-\d.eE+]+),\s*([-\d.eE+]+)\s*\)>")]
    private static partial Regex Colour();
}
