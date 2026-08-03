using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Settings.Schema;

namespace Kitbash.Ui.Settings;

/// <summary>One row of a settings page, whatever kind of row it is.</summary>
public abstract class SettingsRowViewModel : ObservableObject
{
    protected SettingsRowViewModel(ISettingsRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        Name = row.Name;
        Description = row.Description;
    }

    public string Name { get; }

    public string Description { get; }

    /// <summary>A readout may leave it blank, and an empty line still takes its height.</summary>
    public bool HasDescription => Description.Length > 0;
}

/// <summary>
/// A row an app supplied for itself. Read only, so it stages nothing and saves nothing.
/// </summary>
public sealed partial class SettingsReadoutRowViewModel : SettingsRowViewModel
{
    [ObservableProperty]
    private IReadOnlyList<SettingsListEntry> _entries = [];

    public SettingsReadoutRowViewModel(SettingsReadoutRow row)
        : base(row)
    {
        ArgumentNullException.ThrowIfNull(row);

        IsList = row.Style == SettingsReadoutStyle.List;
    }

    /// <summary>
    /// Which of the two presentations the row asked for. Fixed when the row is built,
    /// since it comes from the schema rather than from what was read.
    /// </summary>
    public bool IsList { get; }
}
