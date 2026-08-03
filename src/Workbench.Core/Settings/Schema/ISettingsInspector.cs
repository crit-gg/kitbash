namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Reads a page the way a settings window has to see it: every layer separately, which
/// layer won, what a layer holds that could not be used, and whether each file behind
/// the page is there and readable.
/// </summary>
public interface ISettingsInspector
{
    /// <param name="place">
    /// Which of the home's places to read, such as one workspace of several. Null takes
    /// the first, which is the only one for a home that has just one.
    /// </param>
    SettingsPageView Read(SettingsScope scope, SettingsPage page, SettingsPlace? place = null);

    /// <summary>
    /// What a home keeps files for right now. Empty when nothing is composed for it or it
    /// has nowhere to be. A tree draws a level for the named ones.
    /// </summary>
    IReadOnlyList<SettingsPlace> PlacesIn(SettingsHome home);
}
