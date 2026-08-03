using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kitbash.ViewModels;

/// <summary>
/// One headed group of tool cards. Installed and available are two groups rather than one
/// list with a badge, since a tool you have and a tool you could have answer two questions.
/// </summary>
public partial class ToolGroupViewModel : ObservableObject
{
    /// <summary>Milliseconds a simulated check for updates takes.</summary>
    private const int CheckDelay = 1800;

    [ObservableProperty]
    private bool _isChecking;

    public ToolGroupViewModel(string label, IReadOnlyList<ToolCardViewModel> tools, bool offersUpdates)
    {
        ArgumentNullException.ThrowIfNull(tools);

        Label = label;
        Tools = tools;
        ShowsCheck = offersUpdates;

        foreach (var tool in tools)
        {
            tool.PropertyChanged += OnToolChanged;
        }
    }

    public string Label { get; }

    public IReadOnlyList<ToolCardViewModel> Tools { get; }

    /// <summary>Only the installed group can check for or apply updates.</summary>
    public bool ShowsCheck { get; }

    /// <summary>Drawn only while something is pending, so an up to date group is quiet.</summary>
    public bool ShowsUpdateAll => ShowsCheck && Pending.Count > 0;

    public string UpdateAllLabel => $"Update all ({Pending.Count})";

    public string CheckLabel => IsChecking ? "Checking" : "Check for updates";

    private IReadOnlyList<ToolCardViewModel> Pending => [.. Tools.Where(tool => tool.HasUpdate)];

    [RelayCommand]
    private Task UpdateAll() =>
        Task.WhenAll(Pending.Select(tool => tool.UpdateCommand.ExecuteAsync(null)));

    /// <summary>
    /// Reports on itself in place rather than raising a toast, the way the status bar does.
    /// Nothing is asked, since there is nowhere yet to ask.
    /// </summary>
    [RelayCommand]
    private async Task Check()
    {
        if (IsChecking)
        {
            return;
        }

        IsChecking = true;

        await Task.Delay(CheckDelay).ConfigureAwait(true);

        IsChecking = false;
    }

    partial void OnIsCheckingChanged(bool value) => OnPropertyChanged(nameof(CheckLabel));

    private void OnToolChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(ToolCardViewModel.HasUpdate))
        {
            return;
        }

        OnPropertyChanged(nameof(ShowsUpdateAll));
        OnPropertyChanged(nameof(UpdateAllLabel));
    }
}
