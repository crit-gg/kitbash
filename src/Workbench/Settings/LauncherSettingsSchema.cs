using Workbench.Core.IO;
using Workbench.Core.Settings;
using Workbench.Core.Settings.Schema;
using Workbench.Core.Workspaces;

namespace Workbench.Settings;

/// <summary>
/// Every page the launcher's settings window draws. The pages themselves belong to the
/// schemas that declare the keys, so this only says which of them the launcher owns and
/// adds the one page nothing else could describe.
/// </summary>
public sealed class LauncherSettingsSchema
{
    private const int PathWidth = 96;

    public LauncherSettingsSchema(
        WindowSettingsSchema window,
        ExternalToolsSettingsSchema tools,
        GodotSettingsSchema godot,
        WorkspacesSettingsSchema workspaces,
        WorkspaceGodotSettingsSchema workspaceGodot,
        IWorkspaceRegistry registry,
        IPathShortener shortener,
        IPathRules paths)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(godot);
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(workspaceGodot);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(shortener);
        ArgumentNullException.ThrowIfNull(paths);

        // The registry is what the app remembers rather than what a person chose, so it
        // has no descriptor and arrives as rows the launcher supplies for itself.
        var current = new SettingsListRow
        {
            Name = "Open workspace",
            Description = "The workspace every page below and every tool follows.",
            Read = () => registry.Current is { } workspace
                ? [new SettingsListEntry(shortener.Shorten(workspace.Root, PathWidth), IsCurrent: true)]
                : [],
        };

        var known = new SettingsListRow
        {
            Name = "Known workspaces",
            Description = "Every workspace offered in the selector. The open one is marked.",
            Read = () =>
            [
                .. registry.All.Select(workspace => new SettingsListEntry(
                    shortener.Shorten(workspace.Root, PathWidth),
                    IsCurrent: registry.Current is { } open && paths.AreSame(workspace.Root, open.Root))),
            ],
        };

        State = new SettingsPage
        {
            Id = "workspaceState",
            Title = "Workspaces",
            Home = SettingsHome.State,
            Sections = [new SettingsSection("Registry", [current, known])],
        };

        Schema = new SettingsSchema(
            SettingsScope.Global,
            "Workbench Settings",
            [
                window.Page,
                tools.Page,
                godot.Page,
                workspaces.Page,
                workspaceGodot.Page,
                State,
            ]);
    }

    /// <summary>The launcher's own page, since nothing in Core describes a registry.</summary>
    public SettingsPage State { get; }

    public SettingsSchema Schema { get; }
}
