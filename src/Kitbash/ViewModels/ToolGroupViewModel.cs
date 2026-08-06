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
    private readonly Func<Task>? _check;
    private readonly bool _offersUpdates;

    [ObservableProperty]
    private bool _isChecking;

    public ToolGroupViewModel(
        string label,
        IReadOnlyList<ToolCardViewModel> tools,
        bool offersUpdates,
        Func<Task>? check = null)
    {
        ArgumentNullException.ThrowIfNull(tools);

        Label = label;
        Tools = tools;
        _offersUpdates = offersUpdates;
        _check = check;

        foreach (var tool in tools)
        {
            tool.PropertyChanged += OnToolChanged;
        }
    }

    public string Label { get; }

    public IReadOnlyList<ToolCardViewModel> Tools { get; }

    /// <summary>
    /// How many cards fit across. A compact card is narrower, so its group fits more.
    /// </summary>
    public int Columns { get; init; } = 3;

    /// <summary>
    /// Checking asks every repository, so one heading carries it for the whole page rather
    /// than each installed group carrying its own.
    /// </summary>
    public bool ShowsCheck { get; set; }

    /// <summary>
    /// Whether this heading carries the page's own action. Set on the first group before
    /// the list is bound, so it never changes while a group is on screen.
    /// </summary>
    public bool ShowsFolderInstall { get; set; }

    /// <summary>Drawn only while something is pending, so an up to date group is quiet.</summary>
    public bool ShowsUpdateAll => _offersUpdates && Pending.Count > 0;

    public string UpdateAllLabel => $"Update all ({Pending.Count})";

    public string CheckLabel => IsChecking ? "Checking" : "Check for updates";

    private IReadOnlyList<ToolCardViewModel> Pending => [.. Tools.Where(tool => tool.HasUpdate)];

    [RelayCommand]
    private Task UpdateAll() =>
        Task.WhenAll(Pending.Select(tool => tool.InstallCommand.ExecuteAsync(null)));

    /// <summary>
    /// Asks every repository now rather than using what was cached. Reports on itself in
    /// place rather than raising a toast, the way the status bar does.
    /// </summary>
    [RelayCommand]
    private async Task Check()
    {
        if (IsChecking || _check is null)
        {
            return;
        }

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
