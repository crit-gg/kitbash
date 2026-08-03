namespace Workbench.Core.Settings.Schema;

/// <summary>One line of a <see cref="SettingsListRow"/>.</summary>
/// <param name="IsCurrent">Marked out from the rest, such as the workspace that is open.</param>
public sealed record SettingsListEntry(string Text, bool IsCurrent = false);

/// <summary>
/// A row that is not a setting. This is the escape hatch an app supplies for something
/// a page has to show that no descriptor can describe, such as the workspaces a person
/// has added. Read only, so the schema never grows a way to describe an action.
/// </summary>
public sealed class SettingsListRow : ISettingsRow
{
    private readonly string _name = string.Empty;
    private readonly string _description = string.Empty;
    private readonly Func<IReadOnlyList<SettingsListEntry>> _read = () => [];

    public required string Name
    {
        get => _name;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = value;
        }
    }

    public required string Description
    {
        get => _description;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _description = value;
        }
    }

    /// <summary>
    /// Asked again every time the page loads, since what it lists changes while the app
    /// runs. Runs off the UI thread with the rest of the page load.
    /// </summary>
    public required Func<IReadOnlyList<SettingsListEntry>> Read
    {
        get => _read;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _read = value;
        }
    }
}
