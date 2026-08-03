namespace Workbench.Core.Settings.Schema;

/// <summary>How a readout is drawn. There is no derived option, since the answer must
/// not change with the data: a list that happened to hold one line would otherwise
/// redraw itself as a value.</summary>
public enum SettingsReadoutStyle
{
    /// <summary>Plain text where an editor would be. For one fact, such as a version.</summary>
    Value,

    /// <summary>A well of lines, each with a mark. For a set, such as known workspaces.</summary>
    List,
}

/// <summary>
/// A row that reads rather than edits. This is the escape hatch an app supplies for
/// something a page has to show that no descriptor can describe, such as the version
/// running or the workspaces a person has added. It has no key, so nothing here is ever
/// written, and the schema never grows a way to describe an action.
/// </summary>
public sealed class SettingsReadoutRow : ISettingsRow
{
    private readonly string _name = string.Empty;
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

    /// <summary>
    /// Optional here, unlike on a setting. A fact whose name says the whole of it needs
    /// no sentence under it, and one written anyway is a line of noise on every page load.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Value unless it is a set of things. See <see cref="SettingsReadoutStyle"/>.</summary>
    public SettingsReadoutStyle Style { get; init; } = SettingsReadoutStyle.Value;

    /// <summary>
    /// Asked again every time the page loads, since what it says changes while the app
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
