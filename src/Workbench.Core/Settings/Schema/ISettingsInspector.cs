namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Reads a page the way a settings window has to see it: every layer separately, which
/// layer won, what a layer holds that could not be used, and whether each file behind
/// the page is there and readable.
/// </summary>
public interface ISettingsInspector
{
    SettingsPageView Read(SettingsScope scope, SettingsPage page);
}
