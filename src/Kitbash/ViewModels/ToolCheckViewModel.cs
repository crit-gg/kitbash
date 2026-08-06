using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kitbash.ViewModels;

/// <summary>
/// The tools page's Check for updates action. One for the page rather than one per group,
/// since a check asks every repository and the groups are rebuilt while it runs.
/// </summary>
public partial class ToolCheckViewModel : ObservableObject
{
    private readonly Func<Task> _check;

    [ObservableProperty]
    private bool _isChecking;

    public ToolCheckViewModel(Func<Task> check)
    {
        ArgumentNullException.ThrowIfNull(check);
        _check = check;
    }

    public string Label => IsChecking ? "Checking" : "Check for updates";

    /// <summary>
    /// Asks every repository now rather than using what was cached. Reports on itself in
    /// place rather than raising a toast, the way the status bar does.
    /// </summary>
    [RelayCommand]
    private async Task Check()
    {
        IsChecking = true;

        try
        {
            await _check().ConfigureAwait(true);
        }
        finally
        {
            IsChecking = false;
        }
    }

    partial void OnIsCheckingChanged(bool value) => OnPropertyChanged(nameof(Label));
}
