using Avalonia.Controls;

namespace Kitbash.Ui.Projects;

/// <summary>
/// How an app opens its welcome window. One per app, since the list belongs to the
/// process that is running.
/// </summary>
public interface IProjectsWindows
{
    /// <summary>
    /// Opens it, or brings the one already open to the front. It is not modal, so
    /// whatever is behind it stays usable.
    /// </summary>
    /// <param name="owner">
    /// Null for an app opening this before it has any other window, which centres it on
    /// the screen and puts it in the task bar. An owner centres it on that window.
    /// </param>
    Window Open(Window? owner);
}
