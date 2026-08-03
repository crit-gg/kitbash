namespace Workbench.Core.Platform;

/// <summary>
/// Makes the running copy findable the way this desktop expects, so it is a program
/// rather than a file somebody has to remember the location of.
/// </summary>
public interface IDesktopIntegration
{
    /// <summary>
    /// Writes whatever this desktop needs, replacing what is there when it has gone
    /// stale. Never throws, since the app works without it.
    /// </summary>
    void Install();
}
