using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>
/// A tool as the launcher lists it. One card is either something installed, which may
/// have an update waiting, or something a repository offers.
/// </summary>
public partial class ToolCardViewModel : ObservableObject
{
    /// <summary>Most actions a card promotes out of the menu, counting the primary.</summary>
    private const int ProminentActions = 3;

    private readonly Func<ToolCardViewModel, Task>? _install;
    private readonly IReadOnlyList<string> _actions;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private Bitmap? _icon;

    /// <param name="tool">What is on disk, or null for a tool that is only offered.</param>
    /// <param name="offer">What a repository has, or null when none does.</param>
    /// <param name="install">Puts <paramref name="offer"/> on this machine.</param>
    public ToolCardViewModel(
        InstalledTool? tool,
        OfferedTool? offer,
        Func<ToolCardViewModel, Task>? install = null,
        IReadOnlyList<string>? actions = null,
        IReadOnlyList<ToolMenuItemViewModel>? menu = null)
    {
        if (tool is null && offer is null)
        {
            throw new ArgumentException("A card is an installed tool, an offered one, or both.", nameof(tool));
        }

        Tool = tool;
        Offer = offer;
        Menu = menu ?? [];

        _install = install;
        _actions = actions ?? [];
    }

    /// <summary>Null when the tool is not installed.</summary>
    public InstalledTool? Tool { get; }

    /// <summary>Null when no repository is offering this tool.</summary>
    public OfferedTool? Offer { get; }

    /// <summary>
    /// The card is drawn small. A script has one button and nothing to describe at length,
    /// so its own section fits more of them across.
    /// </summary>
    public bool IsCompact { get; init; }

    /// <summary>
    /// The workspaces providing an installed tool, worked out from what it was installed
    /// from. Empty for one the global list provides and for one nothing offers.
    /// </summary>
    public IReadOnlyList<string> ProvidedBy { get; init; } = [];

    /// <summary>The letter on the card's tile, for a tool that supplies no icon.</summary>
    public string Mark => Name.Length == 0 ? "?" : Name[..1].ToUpperInvariant();

    /// <summary>The tile draws the tool's own art instead of the letter.</summary>
    public bool HasIcon => Icon is not null;

    public string Name => Tool?.Name ?? Offer!.Name;

    public string Description => Tool?.Summary ?? Offer!.Summary;

    /// <summary>False lists the tool under available rather than installed.</summary>
    public bool IsInstalled => Tool is not null;

    /// <summary>
    /// A workspace's own repository list is what offers this tool, so it is gone from the
    /// page in a workspace that does not list it.
    /// </summary>
    public bool IsFromWorkspace => Workspaces.Count > 0;

    /// <summary>The tool declares itself the loose way, so it is one somebody is building.</summary>
    public bool IsInDevelopment => Tool?.Manifest.IsDevelopment == true;

    /// <summary>What that mark says on hover.</summary>
    public string DevelopmentTip => "In development, from a folder on this machine";

    /// <summary>What the mark says on hover, naming every workspace offering the tool.</summary>
    public string WorkspaceTip => Workspaces.Count switch
    {
        0 => string.Empty,
        1 => $"Provided by the {Workspaces[0]} workspace",
        _ => $"Provided by the {string.Join(", ", Workspaces.Take(Workspaces.Count - 1))} "
            + $"and {Workspaces[^1]} workspaces",
    };

    /// <summary>
    /// The workspaces offering the tool. The one the offer came through when the catalogue
    /// named none, which is a workspace whose list could not be read a second time, and
    /// what the install recorded when no repository is offering it at all.
    /// </summary>
    private IReadOnlyList<string> Workspaces => Offer switch
    {
        { Workspaces.Count: > 0 } offer => offer.Workspaces,
        { Source.Workspace: { } workspace } => [workspace],
        _ => ProvidedBy,
    };

    /// <summary>
    /// What the menu's last line says. A linked tool is removed from the list and a tool
    /// Kitbash installed is uninstalled, since only one of the two deletes anything.
    /// </summary>
    public string RemoveLabel => Tool is { IsLinked: true } ? $"Remove {Name}" : $"Uninstall {Name}";

    public IReadOnlyList<ToolMenuItemViewModel> Menu { get; }

    public bool HasMenu => Menu.Count > 0;

    /// <summary>The version installed, or the one on offer for a tool that is not.</summary>
    public string Version => (Tool?.Version ?? Offer!.Version).ToString();

    /// <summary>A repository has a version above the one installed.</summary>
    public bool HasUpdate =>
        !IsUpdating && Tool is { } tool && Offer is { } offer && offer.Version.CompareTo(tool.Version) > 0;

    /// <summary>
    /// The tool cannot open this workspace until it is updated. Nothing says so in words.
    /// The card offers one button, which is its own explanation.
    /// </summary>
    public bool IsBlocked => HasUpdate && Offer!.Manifest.Required;

    public bool ShowLaunch => !IsDead && IsInstalled;

    /// <summary>
    /// What the lead button says. A script runs and ends, so calling that Launch would
    /// promise a window nobody is going to get.
    /// </summary>
    public string LaunchLabel => Tool?.Manifest.IsScript == true ? "Run" : "Launch";

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

    public string ReleaseNoteLabel => $"What is new in {Offer?.Version}";

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

    /// <summary>What the install reports back, as a percentage of the whole payload.</summary>
    public void Report(double percent) => Progress = Math.Clamp(percent, 0, 100);

    /// <summary>
    /// Installs the offered version, which is Install on a card without the tool and
    /// Update on one with it. The page is read again afterwards, so this card is replaced
    /// rather than updated in place.
    /// </summary>
    [RelayCommand]
    private async Task Install()
    {
        if (IsUpdating || _install is null || Offer is null)
        {
            return;
        }

        Progress = 0;
        IsUpdating = true;

        try
        {
            await _install(this).ConfigureAwait(true);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    partial void OnProgressChanged(double value) => OnPropertyChanged(nameof(PercentLabel));

    partial void OnIconChanged(Bitmap? value) => OnPropertyChanged(nameof(HasIcon));

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

/// <summary>One line of a tool card's menu, and what choosing it does.</summary>
public sealed record ToolMenuItemViewModel(string Label, Action Invoke, string? Hint = null);
