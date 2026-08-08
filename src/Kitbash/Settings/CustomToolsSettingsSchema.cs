using Kitbash.Core.Platform.Openers;
using Kitbash.Core.Settings.Schema;
using Kitbash.ViewModels;

namespace Kitbash.Settings;

/// <summary>
/// What the Open in button offers. The launcher's own page, since the key is an array of
/// tables and the schema describes settings one value at a time.
/// </summary>
public sealed class CustomToolsSettingsSchema
{
    public CustomToolsSettingsSchema(
        CustomToolsEditor editor,
        HiddenToolsEditor found,
        IWorkspaceOpeners openers)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(found);
        ArgumentNullException.ThrowIfNull(openers);

        var list = new SettingsEditorRow
        {
            Name = "Tools",
            Description = "Programs the Open in button offers, beside the ones Kitbash found.",
            Layout = SettingsRowLayout.Below,
            Editor = _ => editor,
        };

        var detected = new SettingsEditorRow
        {
            Name = "Found on this machine",
            Description = "Turn one off to keep it out of the Open in menu.",
            Editor = _ => found,
        };

        // Kitbash finds most of these itself, so this is what tells somebody whether the
        // tool they added was understood.
        var inForce = new SettingsReadoutRow
        {
            Name = "On offer",
            Style = SettingsReadoutStyle.List,

            // Every line is a path, and a path is what elides first at a third of the page.
            Layout = SettingsRowLayout.Below,
            Description = "Every program the menu offers right now, found or added.",
            // The page load runs off the UI thread, and what is installed is looked up
            // once and held, so only the first read here does any work.
            Read = () =>
            [
                .. openers.ReadAsync().GetAwaiter().GetResult()
                    .Select(opener => new SettingsListEntry(
                        $"{opener.Name} at {opener.Program}",
                        IsCurrent: opener.Kind is WorkspaceOpenerKind.Custom,
                        Full: opener.Program)),
            ],
        };

        Page = new SettingsPage
        {
            Id = "customTools",
            Title = "Open in",
            Home = SettingsHome.Application,
            Sections =
            [
                new SettingsSection("What Kitbash found", [detected]),
                new SettingsSection("Your own tools", [list]),
                new SettingsSection("Everything offered", [inForce]),
            ],
        };
    }

    public SettingsPage Page { get; }
}
