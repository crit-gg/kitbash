using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core.Godot;
using Workbench.Core.IO;
using Workbench.Core.Settings;

namespace Workbench.ViewModels;

/// <summary>
/// The Godot engines page. What is installed, what is available, and what the machine has
/// to say about both.
/// </summary>
/// <remarks>
/// <para>
/// The same split the launcher page uses. Everything that touches a disk, a process or a
/// network happens inside <c>Task.Run</c> and comes back as one value, and only that value
/// reaches a bound property. Here the cost is real rather than theoretical: the release
/// list is a network fetch, and reading the installs runs a process per engine whose record
/// is missing.
/// </para>
/// <para>
/// Filtering does not go back to disk. What was read is kept and projected again, so typing
/// in the filter field never waits on anything.
/// </para>
/// </remarks>
public sealed partial class EnginesViewModel : ViewModelBase
{
    /// <summary>Roughly what fits the status bar beside the counts.</summary>
    private const int RootLength = 46;

    private readonly IEngineCatalogue _catalogue;
    private readonly IEngineStore _store;
    private readonly IGodotSettings _settings;
    private readonly IPathShortener _paths;
    private readonly SemaphoreSlim _loading = new(1, 1);

    private IReadOnlyList<EngineRelease> _releases = [];
    private IReadOnlyList<InstalledEngine> _installed = [];
    private EngineChannel? _channel = EngineChannel.Stable;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _onAvailable = true;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _installRoot = string.Empty;

    [ObservableProperty]
    private int _installedCount;

    [ObservableProperty]
    private int _releaseCount;

    [ObservableProperty]
    private string _onDisk = string.Empty;

    /// <summary>The newest stable that is newer than anything installed. Empty when none.</summary>
    [ObservableProperty]
    private string _updateVersion = string.Empty;

    /// <summary>True when the release list came from a cache because the network did not answer.</summary>
    [ObservableProperty]
    private bool _isStale;

    [ObservableProperty]
    private string _staleNote = string.Empty;

    /// <summary>Set when nothing could be read at all, which is the one case with no list.</summary>
    [ObservableProperty]
    private string _failure = string.Empty;

    [ObservableProperty]
    private string _emptyNote = string.Empty;

    public EnginesViewModel(
        IEngineCatalogue catalogue,
        IEngineStore store,
        IGodotSettings settings,
        IPathShortener paths)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(paths);

        _catalogue = catalogue;
        _store = store;
        _settings = settings;
        _paths = paths;

        // Not read in the constructor, unlike the workspace page. That one reads a config
        // file and this one fetches over a network, so the window would wait on it.
        InstallRoot = _paths.Shorten(_settings.EngineDirectory, RootLength);
    }

    public ObservableCollection<EngineRowViewModel> Rows { get; } = [];

    public bool OnInstalled => !OnAvailable;

    public bool HasUpdate => UpdateVersion.Length > 0;

    /// <summary>A zero is never drawn, which is the status bar's own rule.</summary>
    public bool HasInstalled => InstalledCount > 0;

    public bool HasFailed => Failure.Length > 0;

    public bool IsEmpty => Rows.Count == 0 && !HasFailed;

    /// <summary>Reads everything again. The Refresh button goes past the cache, a first open does not.</summary>
    [RelayCommand]
    public async Task LoadAsync(bool refresh = false)
    {
        if (!await _loading.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        IsLoading = true;

        try
        {
            var loaded = await Task.Run(() => ReadAsync(refresh)).ConfigureAwait(true);

            Apply(loaded);
        }
        finally
        {
            IsLoading = false;
            _loading.Release();
        }
    }

    /// <summary>Goes past the cache, which is what the button in the band means.</summary>
    [RelayCommand]
    private Task Refresh() => LoadAsync(refresh: true);

    [RelayCommand]
    private void PickTab(string tab)
    {
        OnAvailable = tab == "available";
    }

    [RelayCommand]
    private void PickChannel(string channel)
    {
        _channel = channel switch
        {
            "stable" => EngineChannel.Stable,
            "rc" => EngineChannel.Rc,
            "beta" => EngineChannel.Beta,
            "dev" => EngineChannel.Dev,
            _ => null,
        };

        Rebuild();
    }

    partial void OnOnAvailableChanged(bool value)
    {
        OnPropertyChanged(nameof(OnInstalled));
        Rebuild();
    }

    partial void OnQueryChanged(string value) => Rebuild();

    partial void OnUpdateVersionChanged(string value) => OnPropertyChanged(nameof(HasUpdate));

    partial void OnInstalledCountChanged(int value) => OnPropertyChanged(nameof(HasInstalled));

    partial void OnFailureChanged(string value)
    {
        OnPropertyChanged(nameof(HasFailed));
        OnPropertyChanged(nameof(IsEmpty));
    }

    // The disk and network half. Nothing here touches a bound property.
    private async Task<Loaded> ReadAsync(bool refresh)
    {
        IReadOnlyList<InstalledEngine> installed;

        try
        {
            installed = await _store.ReadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A directory that cannot be read is an empty list rather than a dead page,
            // since the release half may still have something to say.
            installed = [];
        }

        try
        {
            var releases = await _catalogue.ReadReleasesAsync(refresh, CancellationToken.None).ConfigureAwait(false);

            return new Loaded(releases.Releases, installed, releases.IsStale, releases.ReadAt, string.Empty);
        }
        catch (EngineCatalogueException error)
        {
            return new Loaded([], installed, false, default, error.Message);
        }
    }

    private void Apply(Loaded loaded)
    {
        _releases = loaded.Releases;
        _installed = loaded.Installed;

        Failure = loaded.Failure;
        IsStale = loaded.IsStale;
        StaleNote = loaded.IsStale
            ? $"This list was read {Ago(loaded.ReadAt)} and could not be checked just now."
            : string.Empty;

        InstallRoot = _paths.Shorten(_settings.EngineDirectory, RootLength);
        InstalledCount = _installed.Count;
        ReleaseCount = _releases.Count;
        OnDisk = Size(_installed.Sum(engine => engine.SizeOnDisk));
        UpdateVersion = Update();

        Rebuild();
    }

    /// <summary>
    /// Projects what was read through the filters. Never touches a disk, so it runs on
    /// every keystroke without a thought.
    /// </summary>
    private void Rebuild()
    {
        var query = Query.Trim();

        Rows.Clear();

        if (OnAvailable)
        {
            foreach (var release in _releases)
            {
                if (_channel is { } channel && release.Channel != channel)
                {
                    continue;
                }

                var row = EngineRowViewModel.For(release, _installed);

                if (Matches(row, query))
                {
                    Rows.Add(row);
                }
            }
        }
        else
        {
            foreach (var engine in _installed)
            {
                var row = EngineRowViewModel.For(engine, _settings.DefaultEngine, _paths);

                if (Matches(row, query))
                {
                    Rows.Add(row);
                }
            }
        }

        EmptyNote = query.Length > 0 ? $"No versions match {query}" : "Nothing here yet";

        OnPropertyChanged(nameof(IsEmpty));
    }

    private static bool Matches(EngineRowViewModel row, string query) =>
        query.Length == 0 || row.Title.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The newest stable release that is newer than the newest stable installed. Empty when
    /// there is nothing to say, which is the status bar's rule that a zero is never drawn.
    /// </summary>
    private string Update()
    {
        var newest = _releases.Where(release => release.IsStable).MaxBy(release => release.Tag);

        if (newest is null)
        {
            return string.Empty;
        }

        var here = _installed
            .Where(engine => engine.Tag.Channel == EngineChannel.Stable)
            .Select(engine => engine.Tag)
            .DefaultIfEmpty()
            .Max();

        return here == default || newest.Tag > here ? EngineRowViewModel.VersionOf(newest.Tag) : string.Empty;
    }

    private static string Size(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024 / 1024:0.0} GB",
        >= 1024 * 1024 => $"{bytes / 1024 / 1024} MB",
        _ => $"{bytes / 1024} KB",
    };

    private static string Ago(DateTimeOffset when)
    {
        var since = DateTimeOffset.UtcNow - when;

        return since switch
        {
            { TotalMinutes: < 2 } => "a moment ago",
            { TotalHours: < 1 } => $"{(int)since.TotalMinutes} minutes ago",
            { TotalHours: < 24 } => $"{(int)since.TotalHours} hours ago",
            _ => when.ToLocalTime().ToString("d", CultureInfo.CurrentCulture),
        };
    }

    /// <summary>Everything one load read, ready to be put on screen.</summary>
    private sealed record Loaded(
        IReadOnlyList<EngineRelease> Releases,
        IReadOnlyList<InstalledEngine> Installed,
        bool IsStale,
        DateTimeOffset ReadAt,
        string Failure);
}
