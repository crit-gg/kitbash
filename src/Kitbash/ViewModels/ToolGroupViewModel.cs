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
    private readonly bool _offersUpdates;

    public ToolGroupViewModel(
        string label,
        IReadOnlyList<ToolCardViewModel> tools,
        bool offersUpdates,
        ToolCheckViewModel? check = null)
    {
        ArgumentNullException.ThrowIfNull(tools);

        Label = label;
        Tools = tools;
        _offersUpdates = offersUpdates;
        Check = check;

        foreach (var tool in tools)
        {
            tool.PropertyChanged += OnToolChanged;
        }
    }

    public string Label { get; }

    public IReadOnlyList<ToolCardViewModel> Tools { get; }

    /// <summary>The page's own check, which the empty state draws too.</summary>
    public ToolCheckViewModel? Check { get; }

    /// <summary>
    /// How many cards fit across. Both card sizes take the same width, so a script row and
    /// a tool row line up down the page.
    /// </summary>
    public int Columns { get; init; } = 3;

    /// <summary>
    /// Checking asks every repository, so one heading carries it for the whole page rather
    /// than each installed group carrying its own.
    /// </summary>
    public bool ShowsCheck { get; set; }

    /// <summary>
    /// Whether this heading carries the page's own menu. Set on the first group before
    /// the list is bound, so it never changes while a group is on screen.
    /// </summary>
    public bool ShowsPageMenu { get; set; }

    /// <summary>Drawn only while something is pending, so an up to date group is quiet.</summary>
    public bool ShowsUpdateAll => _offersUpdates && Pending.Count > 0;

    public string UpdateAllLabel => $"Update all ({Pending.Count})";

    private IReadOnlyList<ToolCardViewModel> Pending => [.. Tools.Where(tool => tool.HasUpdate)];

    [RelayCommand]
    private Task UpdateAll() =>
        Task.WhenAll(Pending.Select(tool => tool.InstallCommand.ExecuteAsync(null)));

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
