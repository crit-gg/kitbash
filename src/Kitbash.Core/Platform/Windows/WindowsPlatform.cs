using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Windows;

internal sealed class WindowsPlatform : DesktopPlatform
{
    public WindowsPlatform(IFileSystem fileSystem, IProcessRunner processes)
        : base(fileSystem, processes)
    {
    }

    public override PlatformKind Kind => PlatformKind.Windows;

    // The shell picks the handler the user registered, so a replaced browser or file
    // browser is honored.
    protected override void Open(string target) => Processes.Run(ProcessRequest.Shell(target));

    protected override ProcessRequest Detach(ProcessRequest request) =>
        request with { UseShellExecute = true };
}
