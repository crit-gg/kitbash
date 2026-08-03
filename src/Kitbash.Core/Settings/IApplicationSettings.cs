namespace Kitbash.Core.Settings;

/// <summary>
/// Settings for this user on this machine, held outside any workspace. There is no
/// layer to choose, so these can never be shared through a workspace's team config.
/// Use this for choices that belong to the person and the machine, such as which
/// windowing backend to use, or that must be read before a workspace is known.
/// </summary>
public interface IApplicationSettings
{
    ISettings Global { get; }

    ISettings ForTool(string toolId);

    void Set<T>(SettingsScope scope, string key, T value) where T : notnull;

    /// <summary>
    /// Writes several changes to the file at once. A removal takes the key out entirely,
    /// which is what resetting a setting does.
    /// </summary>
    /// <exception cref="SettingsFileUnreadableException">
    /// The file is there and could not be read, so nothing was written.
    /// </exception>
    void Apply(SettingsScope scope, IReadOnlyList<SettingsEdit> edits);

    void Reload();
}
