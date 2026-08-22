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
    /// <param name="page">
    /// The id of the page to show. Null opens the first page in the tree, and an id the
    /// schema does not hold does the same.
    /// </param>
    void Open(Window owner, string? page = null);

    /// <summary>
    /// Raised on the UI thread when the window closes, so an app can reread a setting it
    /// holds. Nothing says what changed, since the window writes whatever a page declares.
    /// </summary>
    event EventHandler? Closed;
}
