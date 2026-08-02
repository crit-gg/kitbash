namespace Workbench.Core.Settings;

/// <summary>A workspace's settings. The launcher and every tool read through this.</summary>
public interface ISettingsService
{
    /// <summary>Settings the launcher and every tool share.</summary>
    ISettings Global { get; }

    /// <summary>Settings for one tool. Tools may also read <see cref="Global"/>.</summary>
    ISettings ForTool(string toolId);

    /// <summary>
    /// Writes one value to one layer and saves that file. The layer is explicit
    /// because writing to the shared layer changes the setting for everyone.
    /// </summary>
    void Set<T>(SettingsScope scope, SettingsLayer layer, string key, T value) where T : notnull;

    /// <summary>
    /// Writes several changes to one file at once, so saving a page of edits is one read
    /// and one write rather than one of each per key. A removal takes the key out
    /// entirely, which is what resetting a setting does.
    /// </summary>
    /// <exception cref="SettingsFileUnreadableException">
    /// The file is there and could not be read, so nothing was written.
    /// </exception>
    void Apply(SettingsScope scope, SettingsLayer layer, IReadOnlyList<SettingsEdit> edits);

    /// <summary>Drops cached values so the next read comes from disk.</summary>
    void Reload();
}
