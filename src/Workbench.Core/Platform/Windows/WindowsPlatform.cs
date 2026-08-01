using Workbench.Core.IO;

namespace Workbench.Core.Platform.Windows;

internal sealed class WindowsPlatform : DesktopPlatform
{
    private readonly IProcessRunner _processes;

    public WindowsPlatform(IFileSystem fileSystem, IProcessRunner processes)
        : base(fileSystem)
    {
        ArgumentNullException.ThrowIfNull(processes);
        _processes = processes;
    }

    public override PlatformKind Kind => PlatformKind.Windows;

    // The shell picks the handler the user registered, so a replaced browser or file
    // browser is honored.
    protected override void Open(string target) => _processes.Run(ProcessRequest.Shell(target));
}
