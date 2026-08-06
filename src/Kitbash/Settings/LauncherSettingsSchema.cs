using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Core.Workspaces;
using Kitbash.Updates;

namespace Kitbash.Settings;

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
        AfterLaunchSettingsSchema afterLaunch,
        WorkspacesSettingsSchema workspaces,
        WorkspaceGodotSettingsSchema workspaceGodot,
        ToolRepositoriesSettingsSchema toolRepositories,
        CustomToolsSettingsSchema customTools,
        UpdateSettingsSchema updates,
        IWorkspaceRegistry registry,
        IPathShortener shortener,
        IPathRules paths)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(godot);
        ArgumentNullException.ThrowIfNull(afterLaunch);
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(workspaceGodot);
        ArgumentNullException.ThrowIfNull(toolRepositories);
        ArgumentNullException.ThrowIfNull(customTools);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(shortener);
        ArgumentNullException.ThrowIfNull(paths);

        // The registry is what the app remembers rather than what a person chose, so it
        // has no descriptor and arrives as rows the launcher supplies for itself.
        var current = new SettingsReadoutRow
        {
            Name = "Open workspace",
            Style = SettingsReadoutStyle.List,
            Description = "The workspace every tool follows.",
            // Shortened to draw and whole to copy, since an elided path pastes as nothing
            // anybody can use.
            Read = () => registry.Current is { } workspace
                ? [new SettingsListEntry(
                    shortener.Shorten(workspace.Root, PathWidth),
                    IsCurrent: true,
                    Full: workspace.Root)]
                : [],
        };

        var known = new SettingsReadoutRow
        {
            Name = "Known workspaces",
            Style = SettingsReadoutStyle.List,
            Description = "Every workspace in the selector. The open one is marked.",
            Read = () =>
            [
                .. registry.All.Select(workspace => new SettingsListEntry(
                    shortener.Shorten(workspace.Root, PathWidth),
                    IsCurrent: registry.Current is { } open && paths.AreSame(workspace.Root, open.Root),
                    Full: workspace.Root)),
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
            "Kitbash Settings",
            [
                window.Page,
                tools.Page,
                customTools.Page,
                godot.Page,
                afterLaunch.Page,
                workspaces.Page,
                workspaceGodot.Page,
                toolRepositories.Page,
                updates.Page,
                State,
            ]);
    }

    /// <summary>The launcher's own page, since nothing in Core describes a registry.</summary>
    public SettingsPage State { get; }

    public SettingsSchema Schema { get; }
}
