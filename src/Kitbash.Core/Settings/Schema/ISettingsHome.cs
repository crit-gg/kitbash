namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// Where one <see cref="SettingsHome"/> keeps its files, and how a change reaches them.
/// One of these is composed per store an app's settings can stand on.
/// </summary>
public interface ISettingsHome
{
    SettingsHome Home { get; }

    /// <summary>
    /// Shown but never written. True for <see cref="SettingsHome.State"/>, which the app
    /// writes and a person does not.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Lowest precedence first. A home that does not layer has one entry and it is null,
    /// so a caller walks layers the same way whether there are two of them or one.
    /// </summary>
    IReadOnlyList<SettingsLayer?> Layers { get; }

    /// <summary>
    /// What this home keeps files for. Asked again every time, since a person can add a
    /// workspace while the app runs. Empty means the home has nowhere to be at all, such
    /// as a workspace home for somebody who has added no workspace.
    /// </summary>
    IReadOnlyList<SettingsPlace> Places { get; }

    /// <summary>The file behind one place, scope and layer. It need not exist.</summary>
    string FileFor(SettingsPlace place, SettingsScope scope, SettingsLayer? layer);

    /// <summary>
    /// Writes every edit to one file at once, through the service that owns it, so
    /// anything holding that file's values reads them again afterwards.
    /// </summary>
    void Apply(
        SettingsPlace place,
        SettingsScope scope,
        SettingsLayer? layer,
        IReadOnlyList<SettingsEdit> edits);
}
