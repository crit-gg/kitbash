namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Reads a page the way a settings window has to see it: every layer separately, which
/// layer won, what a layer holds that could not be used, and whether each file behind
/// the page is there and readable.
/// </summary>
/// <remarks>
/// <see cref="ISettings"/> merges and forgets. That is right for code reading one value
/// and useless for a window that has to say where the value came from, whether reset
/// would do anything, and that a team value is being masked by a personal one.
///
/// Touches every file behind the page, so it runs off the UI thread. Nothing is cached,
/// since the point is to see what is on disk now.
/// </remarks>
public interface ISettingsInspector
{
    SettingsPageView Read(SettingsScope scope, SettingsPage page);
}
