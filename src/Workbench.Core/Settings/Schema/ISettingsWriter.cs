namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Saves a page of staged edits. One read and one write per file, whatever the page
/// changed, and nothing is written when a file could not be read.
/// </summary>
public interface ISettingsWriter
{
    /// <summary>
    /// Every edit has to name a key the page declares, so a window can only write what
    /// it drew.
    /// </summary>
    /// <param name="place">Which of the home's places, and null for the only one.</param>
    /// <param name="layer">Which file, and null for a home that does not layer.</param>
    /// <exception cref="SettingsFileUnreadableException">
    /// The file is there and could not be read, so nothing was written.
    /// </exception>
    void Write(
        SettingsScope scope,
        SettingsPage page,
        SettingsPlace? place,
        SettingsLayer? layer,
        IReadOnlyList<SettingsEdit> edits);
}
