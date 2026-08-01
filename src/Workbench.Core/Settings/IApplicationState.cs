namespace Workbench.Core.Settings;

/// <summary>
/// What Workbench remembers for itself, for this user on this machine. The list of
/// workspaces, which one is open, where a window was. The app writes it, not a
/// person, and it is kept apart from settings so that deleting it resets the app
/// without touching anything anyone chose.
/// </summary>
/// <remarks>
/// Scopes work the way they do for settings, so a tool cannot collide with the
/// launcher. Values are read through <see cref="ISettings"/>, which is only a typed
/// read over a document and says nothing about being a setting.
/// </remarks>
public interface IApplicationState
{
    /// <summary>State the launcher and every tool can read.</summary>
    ISettings Global { get; }

    ISettings ForTool(string toolId);

    void Set<T>(SettingsScope scope, string key, T value) where T : notnull;

    void Reload();
}
