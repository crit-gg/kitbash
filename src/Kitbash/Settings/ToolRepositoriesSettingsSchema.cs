using Kitbash.Core.Settings.Schema;
using Kitbash.Tools;
using Kitbash.ViewModels;

namespace Kitbash.Settings;

/// <summary>
/// Where tools are offered from. The launcher's own page, since the key is an array of
/// tables and the schema describes settings one value at a time.
/// </summary>
public sealed class ToolRepositoriesSettingsSchema
{
    public ToolRepositoriesSettingsSchema(
        ToolRepositoriesEditor editor,
        IToolRepositoryList repositories)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(repositories);

        var list = new SettingsEditorRow
        {
            Name = "Repositories",
            Description = "Kitbash offers the tools these publish. Nothing installs on its own.",
            Layout = SettingsRowLayout.Below,
            Editor = _ => editor,
        };

        // The open workspace has a list of its own, and it travels in a clone, so what is
        // offered is not only what this page writes.
        var inForce = new SettingsReadoutRow
        {
            Name = "In force",
            Style = SettingsReadoutStyle.List,
            Description = "Every repository offering tools right now, and where each is listed.",
            // The scheme is dropped so the line fits the well, since eliding a url takes
            // the owner out of it. Copy still hands back the whole address.
            Read = () =>
            [
                .. repositories.Read().Select(repository => new SettingsListEntry(
                    $"{repository.Url.Value.Host}{repository.Url.Value.AbsolutePath} from {repository.Origin}",
                    IsCurrent: true,
                    Full: repository.Url.ToString())),
            ],
        };

        Page = new SettingsPage
        {
            Id = "toolRepositories",
            Title = "Tool repositories",
            Home = SettingsHome.Application,
            Sections =
            [
                new SettingsSection("Where tools come from", [list]),
                new SettingsSection("Everything offered", [inForce]),
            ],
        };
    }

    public SettingsPage Page { get; }
}
