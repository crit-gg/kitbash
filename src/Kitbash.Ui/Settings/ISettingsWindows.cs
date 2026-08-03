using Avalonia.Controls;

namespace Kitbash.Ui.Settings;

/// <summary>
/// How an app opens its own settings. One window per app, since a schema never crosses
/// a process boundary and two apps can have theirs open at once.
/// </summary>
public interface ISettingsWindows
{
    /// <summary>
    /// Opens the window, or brings the one already open to the front. It is not modal,
    /// so the app stays usable behind it.
    /// </summary>
    void Open(Window owner);
}
