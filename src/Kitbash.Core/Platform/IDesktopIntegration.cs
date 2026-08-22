namespace Kitbash.Core.Platform;

/// <summary>
/// Makes the running copy findable the way this desktop expects, so it is a program
/// rather than a file somebody has to remember the location of, and so the desktop hands
/// it a <c>kitbash</c> link.
/// </summary>
public interface IDesktopIntegration
{
    /// <summary>
    /// Writes whatever this desktop needs, replacing what is there when it has gone
    /// stale. Never throws, since the app works without it.
    /// </summary>
    void Install();

    /// <summary>
    /// Takes back what <see cref="Install"/> wrote, for an uninstaller to call before the
    /// program it points at goes. Never throws.
    /// </summary>
    void Remove();
}
