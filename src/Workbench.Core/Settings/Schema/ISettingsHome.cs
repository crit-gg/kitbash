namespace Workbench.Core.Settings.Schema;

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

    /// <summary>The file behind one scope and layer. It need not exist.</summary>
    string FileFor(SettingsScope scope, SettingsLayer? layer);

    /// <summary>
    /// Writes every edit to one file at once, through the service that owns it, so
    /// anything holding that file's values reads them again afterwards.
    /// </summary>
    void Apply(SettingsScope scope, SettingsLayer? layer, IReadOnlyList<SettingsEdit> edits);
}
