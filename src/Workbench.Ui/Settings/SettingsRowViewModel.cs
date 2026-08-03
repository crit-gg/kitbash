using CommunityToolkit.Mvvm.ComponentModel;
using Workbench.Core.Settings.Schema;

namespace Workbench.Ui.Settings;

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
}

/// <summary>
/// A row an app supplied for itself. Read only, so it stages nothing and saves nothing.
/// </summary>
public sealed partial class SettingsListRowViewModel : SettingsRowViewModel
{
    [ObservableProperty]
    private IReadOnlyList<SettingsListEntry> _entries = [];

    public SettingsListRowViewModel(SettingsListRow row)
        : base(row)
    {
    }
}
