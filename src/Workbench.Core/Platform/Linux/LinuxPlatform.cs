using Workbench.Core.IO;

namespace Workbench.Core.Platform.Linux;

internal sealed class LinuxPlatform : DesktopPlatform
{
    /// <summary>The program that puts a command in a session of its own. util-linux.</summary>
    private const string NewSession = "setsid";

    private readonly IDesktopLauncherResolver _launchers;
    private readonly IExecutableFinder _executables;

    public LinuxPlatform(
        IFileSystem fileSystem,
        IProcessRunner processes,
        IDesktopLauncherResolver launchers,
        IExecutableFinder executables)
        : base(fileSystem, processes)
    {
        ArgumentNullException.ThrowIfNull(launchers);
        ArgumentNullException.ThrowIfNull(executables);

        _launchers = launchers;
        _executables = executables;
    }

    public override PlatformKind Kind => PlatformKind.Linux;

    protected override void Open(string target)
    {
        var launcher = _launchers.Resolve();

        Processes.Run(new ProcessRequest(
            launcher.FileName,
            launcher.ArgumentsFor(target),
            UseShellExecute: false));
    }

    /// <remarks>
    /// Measured: a child started from .NET joins this process's group and its session, so
    /// a signal aimed at Workbench is delivered to it as well. A terminal closing, a
    /// person pressing ctrl C and a logout all do exactly that. Under setsid the same
    /// signal leaves the child running.
    ///
    /// It does not leave a cgroup, and nothing can, so a desktop that tears the app's
    /// scope down by control group takes the engine either way. This covers signals.
    ///
    /// setsid is util-linux and is on every distribution this app has been run on, but no
    /// program is assumed present. Without it the request goes as it is, which still
    /// outlives a launcher that simply exits, and only loses the signal case.
    /// </remarks>
    protected override ProcessRequest Detach(ProcessRequest request) =>
        _executables.Find(NewSession) is { } setsid
            ? request with
            {
                FileName = setsid,
                Arguments = [request.FileName, .. request.Arguments],
                UseShellExecute = false,
            }
            : request;
}
