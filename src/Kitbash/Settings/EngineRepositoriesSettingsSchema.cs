using Kitbash.Core.Settings.Schema;
using Kitbash.ViewModels;

namespace Kitbash.Settings;

/// <summary>
/// Where Godot builds that are not official ones come from. The launcher's own page, since
/// the key is an array of tables and the schema describes settings one value at a time.
/// </summary>
public sealed class EngineRepositoriesSettingsSchema
{
    public EngineRepositoriesSettingsSchema(EngineRepositoriesEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        var list = new SettingsEditorRow
        {
            Name = "Repositories",
            Description =
                "Godot builds these publish are offered beside the official ones. A workspace "
                + "names one to take its engine from it. Nothing installs on its own.",
            Layout = SettingsRowLayout.Below,
            Editor = _ => editor,
        };

        Page = new SettingsPage
        {
            Id = "engineRepositories",
            Title = "Engine repositories",
            Home = SettingsHome.Application,
            Sections = [new SettingsSection("Where engines come from", [list])],
        };
    }

    public SettingsPage Page { get; }
}
