namespace Kitbash.Core.Platform.Windows;

/// <summary>
/// Nothing to do. Velopack's Setup.exe writes the start menu shortcut and the entry in
/// Apps and features, and removes both when the app is uninstalled.
/// </summary>
internal sealed class WindowsDesktopIntegration : IDesktopIntegration
{
    public void Install()
    {
    }
}
