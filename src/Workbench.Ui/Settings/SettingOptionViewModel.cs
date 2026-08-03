using CommunityToolkit.Mvvm.ComponentModel;

namespace Workbench.Ui.Settings;

/// <summary>
/// One option a setting offers. <see cref="IsMissing"/> marks the value a file holds
/// that nothing offered, such as an engine version this machine has not installed. It
/// is still the right value, so it is kept rather than dropped.
/// </summary>
public sealed partial class SettingOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isChosen;

    public SettingOptionViewModel(object value, string label, string? description, bool isMissing = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Value = value;
        Label = label;
        Description = description;
        IsMissing = isMissing;
    }

    public object Value { get; }

    public string Label { get; }

    public string? Description { get; }

    public bool IsMissing { get; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public override string ToString() => Label;
}
