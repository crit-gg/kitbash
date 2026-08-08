using Kitbash.Core.Settings.Schema;
using Kitbash.ViewModels;
using Kitbash.Workspaces;

namespace Kitbash.Settings;

/// <summary>
/// Where a workspace's links are edited. The launcher's own page, since the key is an
/// array of tables and the schema describes settings one value at a time.
/// </summary>
public sealed class WorkspaceLinksSettingsSchema
{
    public WorkspaceLinksSettingsSchema(IWorkspaceLinks links, WorkspaceLinkIcons icons)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(icons);

        var list = new SettingsEditorRow
        {
            Name = "Links",
            Description = "Places this workspace points at. The launcher draws them above the tools.",
            Layout = SettingsRowLayout.Below,

            // The place is the workspace, so each one edits its own files and keeps its
            // own unsaved changes.
            Editor = place => new WorkspaceLinksEditor(links, icons, place?.Id ?? string.Empty),
        };

        Page = new SettingsPage
        {
            Id = "workspaceLinks",
            Title = "Links",
            Home = SettingsHome.Workspace,
            Sections = [new SettingsSection("Where this workspace points", [list])],
        };
    }

    public SettingsPage Page { get; }
}
