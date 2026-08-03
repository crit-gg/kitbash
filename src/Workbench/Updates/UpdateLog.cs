using System.Diagnostics;
using Velopack.Logging;

namespace Workbench.Updates;

/// <summary>
/// Where the update says what it is doing. Velopack keeps its own file log whatever this
/// does, at <c>/tmp/velopack_{appid}.log</c> and
/// <c>%LocalAppData%\velopack\velopack_{appid}.log</c>, so this is only for watching one
/// happen.
/// </summary>
public sealed class UpdateLog : IVelopackLogger
{
    public void Log(VelopackLogLevel level, string? message, Exception? exception) =>
        Write($"[{level}] {message}", exception);

    /// <summary>What the launcher's own update code has to say, in the same place.</summary>
    public void Say(string message, Exception? exception = null) => Write(message, exception);

    /// <summary>
    /// Standard error, because Trace alone reaches nobody: the default listener writes
    /// only under a debugger, so a run from a terminal would show nothing.
    /// </summary>
    private static void Write(string message, Exception? exception)
    {
        var line = exception is null ? $"update: {message}" : $"update: {message} {exception}";

        Trace.WriteLine(line);
        Console.Error.WriteLine(line);
    }
}
