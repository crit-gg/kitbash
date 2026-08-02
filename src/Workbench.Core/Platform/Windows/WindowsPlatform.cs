using Workbench.Core.IO;

namespace Workbench.Core.Platform.Windows;

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

    /// <remarks>
    /// The shell again, which is how everything else here is opened. Windows has no
    /// process group to leave, and a child already outlives its parent, so the case this
    /// closes is the job object: a launcher started by a debugger or a development host
    /// can be in one that kills what is left in it, and a process the shell creates is
    /// outside it. Reasoned rather than measured, since this machine is Linux.
    ///
    /// A request carrying environment variables is started here rather than by the shell,
    /// which <see cref="ProcessRunner"/> already decides. Nothing detached sets any.
    /// </remarks>
    protected override ProcessRequest Detach(ProcessRequest request) =>
        request with { UseShellExecute = true };
}
