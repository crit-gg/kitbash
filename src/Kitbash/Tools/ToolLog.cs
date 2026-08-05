using System.Diagnostics;

namespace Kitbash.Tools;

/// <summary>
/// Where the tools half says what went wrong. A repository that cannot be reached changes
/// nothing a person sees, so this is the only record that it happened.
/// </summary>
public sealed class ToolLog
{
    public void Say(string message, Exception? exception = null)
    {
        var line = exception is null ? $"tools: {message}" : $"tools: {message} {exception.Message}";

        // Standard error as well, since the default trace listener writes only under a
        // debugger and a run from a terminal would otherwise show nothing.
        Trace.WriteLine(line);
        Console.Error.WriteLine(line);
    }
}
