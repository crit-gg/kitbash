namespace Kitbash.Core.Platform;

/// <summary>
/// Nothing to do. On Windows Velopack's Setup.exe writes the start menu shortcut and the
/// entry in Apps and features. On macOS the application bundle is the entry, so Launchpad
/// and Spotlight find it where it sits.
/// </summary>
internal sealed class NoDesktopIntegration : IDesktopIntegration
{
    public void Install()
    {
    }
}
