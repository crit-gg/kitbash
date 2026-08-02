namespace Workbench.Core.Settings.Schema;

/// <summary>
/// Which store a page stands on. This is the top of a settings tree, since it is the
/// question that decides which file a change lands in.
/// </summary>
public enum SettingsHome
{
    /// <summary>
    /// This user on this machine. One file, so there is no layer to choose.
    /// See <see cref="IApplicationSettings"/>.
    /// </summary>
    Application = 0,

    /// <summary>
    /// The open workspace. Two layers, team shared and personal.
    /// See <see cref="ISettingsService"/>.
    /// </summary>
    Workspace = 1,

    /// <summary>
    /// What the app remembers for itself. One file, and read only, since a person is
    /// not expected to edit it. See <see cref="IApplicationState"/>.
    /// </summary>
    State = 2,
}
