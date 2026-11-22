namespace Kitbash.Core.Platform;

/// <summary>
/// Makes the running copy findable the way this desktop expects, so it is a program
/// rather than a file somebody has to remember the location of, and so the desktop hands
/// it a <c>kitbash</c> link and a <c>project.godot</c>.
/// </summary>
public interface IDesktopIntegration
{
    /// <summary>
    /// Writes whatever this desktop needs, replacing what is there when it has gone
    /// stale. Never throws, since the app works without it.
    /// </summary>
    /// <param name="projectsByDefault">
    /// True makes this app what opens a <c>project.godot</c>. False offers it among the
    /// others and gives the default back if this app held it.
    /// </param>
    void Install(bool projectsByDefault);

    /// <summary>
    /// Takes back what <see cref="Install"/> wrote, for an uninstaller to call before the
    /// program it points at goes. Never throws.
    /// </summary>
    void Remove();
}
