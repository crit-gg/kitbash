using System.Diagnostics;

namespace Kitbash;

/// <summary>
/// Where the start says what went wrong. Nothing is on screen but the splash while this
/// runs, so a failure here has nowhere else to go.
/// </summary>
public sealed class StartupLog
{
    public void Say(string message, Exception? exception = null)
    {
        var line = exception is null
            ? $"start: {message}"
            : $"start: {message} {exception}";

        // Standard error as well, since the default trace listener writes only under a
        // debugger and a run from a terminal would otherwise show nothing.
        Trace.WriteLine(line);
        Console.Error.WriteLine(line);
    }
}
