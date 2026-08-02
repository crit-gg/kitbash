using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core.Godot;
using Workbench.Ui.Controls;

namespace Workbench.ViewModels;

/// <summary>
/// One release on the Available tab: the header always, and the builds once it is opened.
/// </summary>
/// <remarks>
/// <para>
/// **A card fetches nothing until it is opened.** The feed says a release exists and says
/// nothing about its files, so the manifest is read on the first expand and kept. Sizes are
/// a second request per build, since neither the feed nor the manifest carries one, and
/// they go out together rather than one after another.
/// </para>
/// <para>
/// Three states, and the third is the one the design does not draw. Builds for this
/// machine is the ordinary case. Nothing at all for this platform is spec 5.1. Builds for
/// this platform but none for this processor is real on any ARM machine, since Linux ARM
/// starts at 4.2-beta5 and Windows arm64 at 4.3-rc1.
/// </para>
/// </remarks>
public sealed partial class ReleaseCardViewModel : ViewModelBase
{
    private readonly EngineRelease _release;
    private readonly EnginesViewModel _page;

    /// <summary>
    /// The rows this card has already made, by file name. **Kept rather than rebuilt.**
    /// Regrouping happens whenever the architecture disclosure moves, and building fresh
    /// rows threw away whatever they were doing: a running install lost its bar, its
    /// percentage and the button that cancels it.
    /// </summary>
    private readonly Dictionary<string, EngineBuildViewModel> _rows = new(StringComparer.Ordinal);

    private readonly IReadOnlyList<EngineBuild> _forHost;

    [ObservableProperty]
    private bool _isOpen;

    /// <summary>
    /// The subtitle is two parts rather than one string, since the design spaces them 18
    /// apart and a joined string cannot be spaced.
    /// </summary>
    [ObservableProperty]
    private string _released = string.Empty;

    [ObservableProperty]
    private string _buildCount = string.Empty;

    [ObservableProperty]
    private string _installedNote = string.Empty;

    /// <summary>Set when the release published nothing at all for this platform.</summary>
    [ObservableProperty]
    private bool _isUnavailable;

    [ObservableProperty]
    private string _unavailableNote = string.Empty;

    [ObservableProperty]
    private string _publishedFor = string.Empty;

    /// <summary>Set when the platform has builds but this processor has none of them.</summary>
    [ObservableProperty]
    private bool _hasNoNative;

    [ObservableProperty]
    private string _noNativeNote = string.Empty;

    [ObservableProperty]
    private string _otherArchitecturesLabel = string.Empty;

    [ObservableProperty]
    private bool _hasOtherArchitectures;

    [ObservableProperty]
    private string _failure = string.Empty;

    public ReleaseCardViewModel(EngineRelease release, EngineManifest? manifest, EnginesViewModel page)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(page);

        _release = release;
        _page = page;

        Title = EngineRowViewModel.NameOf(release.Tag);
        Channel = EngineRowViewModel.ChannelOf(release.Tag);
        Released = $"released {release.Released.ToString("d", CultureInfo.CurrentCulture)}";

        // **Everything the card will ever show, worked out here.** Opening it does no
        // reading and no fetching, so there is nothing to wait for and nothing to pop in.
        _forHost = manifest is null
            ? []
            : [.. manifest.Builds.Where(build => build.Platform == page.HostPlatform)];

        Failure = manifest is null ? "This release could not be read." : string.Empty;
        IsUnavailable = manifest is not null && _forHost.Count == 0;
        UnavailableNote = $"No {page.HostPlatformText} builds in this release";

        PublishedFor = manifest is { PublishedTargets.Count: > 0 }
            ? $"Published for {string.Join(", ", manifest.PublishedTargets)}."
            : string.Empty;

        BuildCount = _forHost.Count == 0
            ? string.Empty
            : _forHost.Count == 1 ? "1 build" : $"{_forHost.Count} builds";

        Arrange();
    }

    public EngineTag Tag => _release.Tag;

    public string Title { get; }

    public string Channel { get; }

    public bool HasChannel => Channel.Length > 0;

    /// <summary>
    /// The tint a channel pill takes. Stable carries no pill at all, which is why it has
    /// no tier here.
    /// </summary>
    public BadgeTier ChannelTier => Tag.Channel switch
    {
        EngineChannel.Rc => BadgeTier.Modified,
        EngineChannel.Beta => BadgeTier.Graph,
        EngineChannel.Alpha => BadgeTier.Error,
        _ => BadgeTier.Neutral,
    };

    public bool HasNotes => _release.Notes is not null;

    public bool HasInstalled => InstalledNote.Length > 0;

    public bool HasFailed => Failure.Length > 0;

    public bool HasBuildCount => BuildCount.Length > 0;

    public ObservableCollection<EngineArchitectureGroupViewModel> Groups { get; } = [];

    /// <summary>Opening is a flag. Everything it reveals was worked out when it was made.</summary>
    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private void OpenNotes()
    {
        if (_release.Notes is { } notes)
        {
            _page.OpenNotes(notes);
        }
    }

    [RelayCommand]
    private void ToggleArchitectures() => _page.ShowAllArchitectures = !_page.ShowAllArchitectures;

    /// <summary>Groups what was read by processor, host first, honouring the disclosure.</summary>
    public void Arrange()
    {
        Groups.Clear();

        if (_forHost.Count == 0)
        {
            HasOtherArchitectures = false;
            HasNoNative = false;

            return;
        }

        // A row per build whatever is shown, so a size that arrives lands on one and the
        // disclosure reveals rows that are already filled in.
        foreach (var build in _forHost)
        {
            RowFor(build);
        }

        var native = _forHost.Where(build => build.Architecture == _page.HostArchitecture).ToList();
        var others = _forHost.Count - native.Count;
        var shown = _page.ShowAllArchitectures ? _forHost : native;

        foreach (var group in shown
                     .GroupBy(build => build.Architecture)
                     .OrderBy(group => group.Key == _page.HostArchitecture ? 0 : 1)
                     .ThenBy(group => group.Key))
        {
            Groups.Add(new EngineArchitectureGroupViewModel(
                EngineBuild.TextFor(group.Key).ToUpperInvariant(),
                [.. group.Select(RowFor)]));
        }

        // The design does not draw this one and it is the common case on an ARM machine.
        HasNoNative = native.Count == 0 && !_page.ShowAllArchitectures;
        NoNativeNote = $"No {EngineBuild.TextFor(_page.HostArchitecture)} build in this release";

        HasOtherArchitectures = others > 0 || _page.ShowAllArchitectures;
        OtherArchitecturesLabel = _page.ShowAllArchitectures
            ? $"Show {EngineBuild.TextFor(_page.HostArchitecture)} only"
            : others == 1
                ? "Show 1 other architecture"
                : $"Show {others} other architectures";
    }

    /// <summary>
    /// The row for one build, made once and kept. Whether it is installed is refreshed on
    /// the way out, since that changes under a row that already exists.
    /// </summary>
    private EngineBuildViewModel RowFor(EngineBuild build)
    {
        if (!_rows.TryGetValue(build.FileName, out var row))
        {
            row = _page.BuildFor(build);
            _rows[build.FileName] = row;
        }

        return row;
    }

    /// <summary>Every row this card holds, whatever the disclosure is showing.</summary>
    public IReadOnlyCollection<EngineBuildViewModel> Rows => _rows.Values;

    /// <summary>How many builds of this release are on the machine, for the header mark.</summary>
    public void CountInstalled(IReadOnlyList<InstalledEngine> installed)
    {
        ArgumentNullException.ThrowIfNull(installed);

        var here = installed.Count(engine => engine.Tag == Tag);

        // A row that already exists is told, rather than being made again to find out.
        foreach (var row in _rows.Values)
        {
            row.IsInstalled = installed.Any(engine => engine.Id == row.Build.Id);
        }

        InstalledNote = here switch
        {
            0 => string.Empty,
            1 => "1 build installed",
            _ => $"{here} builds installed",
        };

        OnPropertyChanged(nameof(HasInstalled));
    }

    partial void OnInstalledNoteChanged(string value) => OnPropertyChanged(nameof(HasInstalled));

    partial void OnBuildCountChanged(string value) => OnPropertyChanged(nameof(HasBuildCount));
}

/// <summary>One processor's builds inside an open card.</summary>
public sealed class EngineArchitectureGroupViewModel
{
    public EngineArchitectureGroupViewModel(string label, IReadOnlyList<EngineBuildViewModel> builds)
    {
        Label = label;
        Builds = builds;
    }

    public string Label { get; }

    public IReadOnlyList<EngineBuildViewModel> Builds { get; }
}
