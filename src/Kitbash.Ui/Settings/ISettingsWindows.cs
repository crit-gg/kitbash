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

    /// <summary>
    /// Raised on the UI thread when the window closes, so an app can reread a setting it
    /// holds. Nothing says what changed, since the window writes whatever a page declares.
    /// </summary>
    event EventHandler? Closed;
}
