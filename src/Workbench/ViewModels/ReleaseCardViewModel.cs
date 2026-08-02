using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core.Godot;

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

    private IReadOnlyList<EngineBuild> _forHost = [];
    private bool _read;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _subtitle = string.Empty;

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

    public ReleaseCardViewModel(EngineRelease release, EnginesViewModel page)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(page);

        _release = release;
        _page = page;

        Title = EngineRowViewModel.NameOf(release.Tag);
        Channel = EngineRowViewModel.ChannelOf(release.Tag);
        Subtitle = $"released {release.Released.ToString("d", CultureInfo.CurrentCulture)}";
    }

    public EngineTag Tag => _release.Tag;

    public string Title { get; }

    public string Channel { get; }

    public bool HasChannel => Channel.Length > 0;

    public bool HasNotes => _release.Notes is not null;

    public bool HasInstalled => InstalledNote.Length > 0;

    public bool HasFailed => Failure.Length > 0;

    public ObservableCollection<EngineArchitectureGroupViewModel> Groups { get; } = [];

    [RelayCommand]
    private async Task ToggleAsync()
    {
        IsOpen = !IsOpen;

        if (IsOpen)
        {
            await OpenAsync().ConfigureAwait(true);
        }
    }

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

    /// <summary>Reads the manifest once, then arranges what it holds.</summary>
    public async Task OpenAsync()
    {
        if (_read)
        {
            Arrange();

            return;
        }

        IsLoading = true;

        try
        {
            var manifest = await _page.ReadManifestAsync(Tag).ConfigureAwait(true);

            _forHost = [.. manifest.Builds.Where(build => build.Platform == _page.HostPlatform)];
            _read = true;

            IsUnavailable = _forHost.Count == 0;
            UnavailableNote = $"No {_page.HostPlatformText} builds in this release";
            PublishedFor = manifest.PublishedTargets.Count > 0
                ? $"Published for {string.Join(", ", manifest.PublishedTargets)}."
                : string.Empty;

            Subtitle = IsUnavailable
                ? $"released {_release.Released.ToString("d", CultureInfo.CurrentCulture)}"
                : $"released {_release.Released.ToString("d", CultureInfo.CurrentCulture)}"
                  + $"   {_forHost.Count} builds";

            Arrange();

            // Sizes come from a request per build, so they go out together and land as they
            // answer. The column is blank until then and never holds the rows up.
            await _page.MeasureAsync(Groups.SelectMany(group => group.Builds).ToList()).ConfigureAwait(true);
        }
        catch (EngineCatalogueException error)
        {
            Failure = error.Message;
            OnPropertyChanged(nameof(HasFailed));
        }
        finally
        {
            IsLoading = false;
        }
    }

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
                [.. group.Select(_page.BuildFor)]));
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

    /// <summary>How many builds of this release are on the machine, for the header mark.</summary>
    public void CountInstalled(IReadOnlyList<InstalledEngine> installed)
    {
        ArgumentNullException.ThrowIfNull(installed);

        var here = installed.Count(engine => engine.Tag == Tag);

        InstalledNote = here switch
        {
            0 => string.Empty,
            1 => "1 build installed",
            _ => $"{here} builds installed",
        };

        OnPropertyChanged(nameof(HasInstalled));
    }

    partial void OnInstalledNoteChanged(string value) => OnPropertyChanged(nameof(HasInstalled));
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
