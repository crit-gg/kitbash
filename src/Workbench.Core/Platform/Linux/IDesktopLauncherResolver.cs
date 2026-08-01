namespace Workbench.Core.Platform.Linux;

/// <summary>Finds a launcher this machine actually has.</summary>
internal interface IDesktopLauncherResolver
{
    /// <exception cref="PlatformNotSupportedException">No known launcher is installed.</exception>
    DesktopLauncher Resolve();
}
