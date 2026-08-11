using Kitbash.Core.Settings;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Where a grid's column layout is kept between runs. It is per person and per machine, so
/// it lives in application state rather than in a workspace, and an app with several grids
/// keeps one layout for each.
/// </summary>
public interface IGridColumnStore
{
    /// <summary>
    /// What was kept for one grid, or nothing when there is none or it cannot be read. A
    /// caller that gets nothing leaves the columns as the tool declared them.
    /// </summary>
    /// <param name="scope">Whose grid it is. A tool passes its own.</param>
    /// <param name="key">Which grid inside that app.</param>
    IReadOnlyList<GridColumnState> Read(SettingsScope scope, string key);

    /// <summary>
    /// Keeps a layout for one grid. A machine that will not take the write keeps working and
    /// starts on the declared columns next time, since a layout is not somebody's work.
    /// </summary>
    void Write(SettingsScope scope, string key, IReadOnlyList<GridColumnState> layout);

    /// <summary>Drops what was kept, so the next run starts on the declared columns.</summary>
    void Forget(SettingsScope scope, string key);
}
