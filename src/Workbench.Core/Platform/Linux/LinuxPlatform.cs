using Workbench.Core.IO;

namespace Workbench.Core.Platform.Linux;

internal sealed class LinuxPlatform : DesktopPlatform
{
    private readonly IProcessRunner _processes;
    private readonly IDesktopLauncherResolver _launchers;

    public LinuxPlatform(
        IFileSystem fileSystem,
        IProcessRunner processes,
        IDesktopLauncherResolver launchers)
        : base(fileSystem)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(launchers);

        _processes = processes;
        _launchers = launchers;
    }

    public override PlatformKind Kind => PlatformKind.Linux;

    protected override void Open(string target)
    {
        var launcher = _launchers.Resolve();

        _processes.Run(new ProcessRequest(
            launcher.FileName,
            launcher.ArgumentsFor(target),
            UseShellExecute: false));
    }
}
