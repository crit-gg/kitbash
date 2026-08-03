using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Linux;

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
