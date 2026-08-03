using Workbench.Core.Settings.Schema;
using Workbench.Updates;

namespace Workbench.Settings;

/// <summary>
/// Where Workbench looks for a newer copy of itself. The launcher's, since only the
/// launcher updates itself.
/// </summary>
public sealed class UpdateSettingsSchema
{
    /// <summary>
    /// The address releases are published to. Blank until there is a server, and this is
    /// the one line to change when there is.
    /// </summary>
    public const string DefaultFeed = "";

    /// <summary>
    /// Whether Workbench looks for a newer copy of itself at all. **Off**, because there
    /// is nowhere to publish to yet, so nothing checks and nothing downloads. Turning it
    /// on is this and a real <see cref="DefaultFeed"/>. The pipeline that has to exist
    /// first is in `.claude/plans/distribution-and-updates.md`.
    /// </summary>
    /// <remarks>
    /// A property rather than a const, so the code it turns off does not read as
    /// unreachable and fail the build.
    /// </remarks>
    public bool IsEnabled => false;

    /// <summary>
    /// This takes the version rather than <see cref="IApplicationUpdates"/>, which would
    /// be a cycle: the update service reads its feed from the descriptor below.
    /// </summary>
    public UpdateSettingsSchema(IApplicationVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        Feed = new SettingDescriptor<string>
        {
            Key = "updates.feed",
            Name = "Update feed",
            Description =
                "An https address or a folder holding published releases. Blank means "
                + "Workbench never looks for an update. A copy that was not installed "
                + "never looks either, whatever this says.",
            Default = DefaultFeed,
            NeedsRestart = true,
        };

        // Not a setting, so it is the schema's read only row kind. Read runs again on
        // every page load, which is what makes it right after an update has landed. No
        // description, because the name says the whole of it.
        Running = new SettingsReadoutRow
        {
            Name = "Current version",
            Read = () =>
            [
                new SettingsListEntry(version.Current.Length > 0 ? version.Current : "unknown"),
            ],
        };

        // Feed is deliberately not on it. See the descriptor below.
        Page = new SettingsPage
        {
            Id = "updates",
            Title = "Updates",
            Home = SettingsHome.Application,
            Sections = [new SettingsSection("This copy", [Running])],
        };
    }

    /// <summary>Read only, since the app decides this and a person cannot.</summary>
    public SettingsReadoutRow Running { get; }

    /// <summary>
    /// **A descriptor that is on no page, so the settings window never draws it.** It is
    /// still a real key in the application settings file, read from a descriptor the way
    /// every other setting is, so somebody who knows it is there can point a copy at a
    /// feed by hand. Where releases come from is the app's answer rather than a person's,
    /// and <see cref="DefaultFeed"/> is where that answer goes.
    /// </summary>
    /// <remarks>
    /// No rule, because the value is one of two shapes and the closed rule set describes
    /// neither pair. A value that is neither counts as blank when it is read. Being on no
    /// page also means <c>ISettingsWriter</c> refuses it, which is the guard that stops a
    /// window writing what it never drew.
    /// </remarks>
    public SettingDescriptor<string> Feed { get; }

    public SettingsPage Page { get; }
}
