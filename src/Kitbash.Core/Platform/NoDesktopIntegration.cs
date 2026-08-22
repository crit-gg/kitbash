namespace Kitbash.Core.Platform;

/// <summary>
/// Nothing to do. On macOS the application bundle is the entry, so Launchpad and
/// Spotlight find it where it sits and Info.plist declares the URL scheme.
/// </summary>
internal sealed class NoDesktopIntegration : IDesktopIntegration
{
    public void Install()
    {
    }

    public void Remove()
    {
    }
}
