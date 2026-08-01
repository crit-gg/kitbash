namespace Workbench.Core.Settings;

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

    void Reload();
}
