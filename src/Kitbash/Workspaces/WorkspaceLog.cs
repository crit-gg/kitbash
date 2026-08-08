using System.Diagnostics;

namespace Kitbash.Workspaces;

/// <summary>
/// Where the workspace half says what went wrong. A link a workspace spelled wrongly is
/// simply not drawn, so this is the only record that it was there.
/// </summary>
public sealed class WorkspaceLog
{
    public void Say(string message, Exception? exception = null)
    {
        var line = exception is null
            ? $"workspace: {message}"
            : $"workspace: {message} {exception.Message}";

        // Standard error as well, since the default trace listener writes only under a
        // debugger and a run from a terminal would otherwise show nothing.
        Trace.WriteLine(line);
        Console.Error.WriteLine(line);
    }
}
