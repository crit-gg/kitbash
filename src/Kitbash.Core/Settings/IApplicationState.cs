namespace Kitbash.Core.Settings;

/// <summary>
/// What Kitbash remembers for itself, for this user on this machine. The list of
/// workspaces, which one is open, where a window was. The app writes it, not a
/// person, and it is kept apart from settings so that deleting it resets the app
/// without touching anything anyone chose.
/// </summary>
public interface IApplicationState
{
    /// <summary>State the launcher and every tool can read.</summary>
    ISettings Global { get; }

    ISettings ForTool(string toolId);

    void Set<T>(SettingsScope scope, string key, T value) where T : notnull;

    /// <summary>Writes several changes to the file at once, the way settings do.</summary>
    /// <exception cref="SettingsFileUnreadableException">
    /// The file is there and could not be read, so nothing was written.
    /// </exception>
    void Apply(SettingsScope scope, IReadOnlyList<SettingsEdit> edits);

    void Reload();
}
