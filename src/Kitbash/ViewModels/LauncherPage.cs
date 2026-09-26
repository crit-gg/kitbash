namespace Kitbash.ViewModels;

/// <summary>
/// The pages the rail opens, in the order the rail lists them. A ListBox selects by
/// number, so these are the indices <see cref="LauncherViewModel.Page"/> holds.
/// </summary>
public enum LauncherPage
{
    /// <summary>Every workspace at once.</summary>
    Workspaces,

    /// <summary>The open workspace, its engine, its links and its tools.</summary>
    Workspace,

    /// <summary>The Godot engines on this machine.</summary>
    Engines,
}
