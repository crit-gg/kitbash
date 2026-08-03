using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core;

namespace Kitbash.ViewModels;

/// <summary>A tool as the launcher lists it.</summary>
public partial class ToolCardViewModel : ObservableObject
{
    /// <summary>Milliseconds before a simulated update shows its first progress.</summary>
    private const int UpdateLead = 300;

    /// <summary>Milliseconds between steps of a simulated update.</summary>
    private const int UpdateStep = 260;

    /// <summary>Most actions a card promotes out of the menu, counting the primary.</summary>
    private const int ProminentActions = 3;

    private readonly string _installed;
    private readonly string? _offered;
    private readonly bool _blocks;
    private readonly IReadOnlyList<string> _actions;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private double _progress;

    private bool _updated;

    public ToolCardViewModel(
        ITool tool,
        string mark,
        string version,
        string? offered,
        bool installed,
        bool blocked,
        IReadOnlyList<string> actions,
        IReadOnlyList<ToolMenuItemViewModel> menu)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(menu);

        Tool = tool;
        Mark = mark;
        IsInstalled = installed;
        Menu = menu;

        _installed = version;
        _offered = offered;
        _blocks = blocked;
        _actions = actions;
    }

    public ITool Tool { get; }

    /// <summary>The letter on the card's tile, until a tool supplies an icon.</summary>
    public string Mark { get; }

    public string Name => Tool.Name;

    public string Description => Tool.Description;

    /// <summary>False lists the tool under available rather than installed.</summary>
    public bool IsInstalled { get; }

    public IReadOnlyList<ToolMenuItemViewModel> Menu { get; }

    /// <summary>The version installed, which becomes the offered one once an update lands.</summary>
    public string Version => _updated ? _offered! : _installed;

    public bool HasUpdate => _offered is not null && !_updated && !IsUpdating;

    /// <summary>
    /// The tool cannot open this workspace until it is updated. Nothing says so in words.
    /// The card offers one button, which is its own explanation.
    /// </summary>
    public bool IsBlocked => _blocks && !_updated;

    public bool ShowLaunch => !IsDead && IsInstalled;

    public bool ShowInstall => !IsDead && !IsInstalled;

    public bool ShowUpdate => HasUpdate && !IsDead;

    /// <summary>A blocked tool offers only the update, so it takes the lead button.</summary>
    public bool ShowUpdateLead => IsDead;

    public string PercentLabel => $"{(int)Progress}%";

    /// <summary>
    /// The column the progress bar sits in. It takes the update button's place while one
    /// runs, and collapses the rest of the time, so the lead button keeps the row.
    /// </summary>
    public GridLength BarColumn => IsUpdating ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;

    public string ReleaseNoteLabel => $"What is new in {_offered}";

    public string UninstallLabel => $"Uninstall {Name}";

    /// <summary>
    /// The actions promoted out of the menu. An update takes one of the three slots, and a
    /// blocked tool promotes none, since only the update works.
    /// </summary>
    public IReadOnlyList<string> Extras => IsBlocked
        ? []
        : [.. _actions.Take(ProminentActions - (HasUpdate || IsUpdating ? 2 : 1))];

    public bool HasExtras => Extras.Count > 0;

    /// <summary>Neither the lead button nor a promoted action is offered.</summary>
    private bool IsDead => IsBlocked && !IsUpdating;

    /// <summary>
    /// Runs an update and reports it in the card. Nothing is downloaded. There is no answer
    /// yet to where a tool comes from, so this only drives the states the design draws.
    /// </summary>
    [RelayCommand]
    private async Task Update()
    {
        if (IsUpdating || _updated || _offered is null)
        {
            return;
        }

        Progress = 0;
        IsUpdating = true;

        await Task.Delay(UpdateLead).ConfigureAwait(true);

        while (Progress < 100)
        {
            Progress = Math.Min(100, Progress + Random.Shared.Next(9, 23));

            await Task.Delay(UpdateStep).ConfigureAwait(true);
        }

        _updated = true;
        IsUpdating = false;

        OnPropertyChanged(nameof(Version));
    }

    partial void OnProgressChanged(double value) => OnPropertyChanged(nameof(PercentLabel));

    partial void OnIsUpdatingChanged(bool value)
    {
        OnPropertyChanged(nameof(BarColumn));
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(IsBlocked));
        OnPropertyChanged(nameof(ShowLaunch));
        OnPropertyChanged(nameof(ShowInstall));
        OnPropertyChanged(nameof(ShowUpdate));
        OnPropertyChanged(nameof(ShowUpdateLead));
        OnPropertyChanged(nameof(Extras));
        OnPropertyChanged(nameof(HasExtras));
    }
}

/// <summary>One line of a tool card's menu.</summary>
public sealed record ToolMenuItemViewModel(string Label, string? Hint = null);
