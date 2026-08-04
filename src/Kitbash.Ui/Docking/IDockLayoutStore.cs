using Dock.Model.Controls;
using Kitbash.Core.Settings;

namespace Kitbash.Ui.Docking;

/// <summary>
/// Where a view's dock layout is kept between runs. It is per person and per machine, so
/// it lives in application state rather than in a workspace, and a tool with several
/// views keeps one layout for each.
/// </summary>
public interface IDockLayoutStore
{
    /// <summary>
    /// The layout kept for one view, or null when there is none or it cannot be read. A
    /// caller that gets null builds its default layout.
    /// </summary>
    /// <param name="scope">Whose layout it is. A tool passes its own.</param>
    /// <param name="view">Which view inside that app.</param>
    IRootDock? Read(SettingsScope scope, string view);

    /// <summary>
    /// Keeps a layout for one view. A machine that will not take the write keeps working
    /// and starts on the default layout next time, since a layout is not somebody's work.
    /// </summary>
    void Write(SettingsScope scope, string view, IRootDock layout);

    /// <summary>Drops what was kept, so the next run builds the default layout.</summary>
    void Forget(SettingsScope scope, string view);
}
