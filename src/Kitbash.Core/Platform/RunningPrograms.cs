using System.ComponentModel;
using System.Diagnostics;
using Kitbash.Core.IO;

namespace Kitbash.Core.Platform;

/// <summary>
/// Reads each process's main module, which .NET answers on Windows, Linux and macOS. A
/// process belonging to somebody else refuses and is skipped, which is fine, since an
/// engine this person would replace is one this person started.
/// </summary>
internal sealed class RunningPrograms : IRunningPrograms
{
    private readonly IPathRules _paths;

    public RunningPrograms(IPathRules paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        _paths = paths;
    }

    public bool IsRunning(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        var wanted = Path.GetFullPath(executable);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainModule?.FileName is { } file && _paths.AreSame(file, wanted))
                {
                    return true;
                }
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException
                                              or UnauthorizedAccessException or NotSupportedException)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }
}
