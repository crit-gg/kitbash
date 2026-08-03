using Avalonia.Collections;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core.Godot;
using Workbench.Core.IO;
using Workbench.Core.Platform;
using Workbench.Core.Settings;
using Workbench.Ui.Toasts;

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

    /// <summary>Roughly what fits a toast card, which is 352 wide.</summary>
    private const int ToastPathLength = 44;


    private readonly IEngineCatalogue _catalogue;
    private readonly IEngineStore _store;
    private readonly IGodotSettings _settings;
    private readonly IPathShortener _paths;
    private readonly IPlatformServices _platform;
    private readonly IFileSystem _files;
    private readonly IEngineInstaller _installer;
    private readonly IEngineFiles _engineFiles;
    private readonly IToastService _toasts;

    /// <summary>One token per build being installed, so each cancels on its own.</summary>
    private readonly Dictionary<EngineId, CancellationTokenSource> _running = [];
    private readonly SemaphoreSlim _loading = new(1, 1);

    private readonly Dictionary<EngineTag, ReleaseCardViewModel> _cards = [];

    private IReadOnlyList<EngineRelease> _releases = [];
    private IReadOnlyDictionary<EngineTag, EngineManifest> _manifests = new Dictionary<EngineTag, EngineManifest>();
    private IReadOnlyList<InstalledEngine> _installed = [];
    private EngineChannel? _channel = EngineChannel.Stable;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// **Installed is what opens, which the design does not do.** The page is opened far
    /// more often to see what is here than to fetch something new, and the release list is
    /// a network read, so the first thing on screen is the half that is already true. The
    /// release list is still read, since the Available count and the update marker in the
    /// status bar both need it.
    /// </summary>
    [ObservableProperty]
    private bool _onAvailable;

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
        IPathShortener paths,
        IPlatformServices platform,
        IFileSystem files,
        IEngineInstaller installer,
        IEngineFiles engineFiles,
        IToastService toasts)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(engineFiles);
        ArgumentNullException.ThrowIfNull(toasts);

        _catalogue = catalogue;
        _store = store;
        _settings = settings;
        _paths = paths;
        _platform = platform;
        _files = files;
        _installer = installer;
        _engineFiles = engineFiles;
        _toasts = toasts;

        // Not read in the constructor, unlike the workspace page. That one reads a config
        // file and this one fetches over a network, so the window would wait on it.
        InstallRoot = _paths.Shorten(_settings.EngineDirectory, RootLength);
    }

    /// <summary>
    /// Whether every processor is shown or only this machine's. **Global rather than per
    /// card, and it resets on relaunch.** A machine either cross compiles or it does not,
    /// so the answer holds while a person moves between releases. It is view state and
    /// never a setting, which is why nothing writes it down.
    /// </summary>
    [ObservableProperty]
    private bool _showAllArchitectures;

    /// <summary>
    /// The rows on screen. **One collection, refilled in two notifications.** An
    /// ObservableCollection raises one per item, so filtering 183 releases cost 184 rounds
    /// of work, and replacing the whole ItemsSource instead made the list throw its
    /// containers away and build them again. AvaloniaList adds a range in one go and the
    /// list keeps its panel.
    /// </summary>
    public AvaloniaList<EngineRowViewModel> Rows { get; } = [];

    public bool OnInstalled => !OnAvailable;

    /// <summary>The release cards, which are what the Available tab lists.</summary>
    public AvaloniaList<ReleaseCardViewModel> Cards { get; } = [];

    /// <summary>The service the window's host draws.</summary>
    public IToastService Toasts => _toasts;

    public EnginePlatform HostPlatform => _engineFiles.Platform;

    public EngineArchitecture HostArchitecture => _engineFiles.Architecture;

    /// <summary>The host platform as a person reads it, for the absent release card.</summary>
    public string HostPlatformText => _engineFiles.Platform switch
    {
        EnginePlatform.Windows => "Windows",
        EnginePlatform.Linux => "Linux",
        _ => "macOS",
    };

    public bool HasUpdate => UpdateVersion.Length > 0;

    /// <summary>A zero is never drawn, which is the status bar's own rule.</summary>
    public bool HasInstalled => InstalledCount > 0;

    public bool HasFailed => Failure.Length > 0;

    public bool IsEmpty => (OnAvailable ? Cards.Count : Rows.Count) == 0 && !HasFailed;

    /// <summary>
    /// Raised after a read, so anything else showing what is installed can follow it.
    /// </summary>
    /// <remarks>
    /// **Every mutation on this page ends in a read**, which is why one event here covers
    /// installing, uninstalling, importing and naming a default. It is the same reason
    /// the git strip watches a repository: what a person does in one place has to show up
    /// in the other, and the workspace page's engine strip is the other place.
    ///
    /// It fires on the first read as well. Refreshing a strip that was already right
    /// costs a read nobody sees, where missing one leaves the strip wrong.
    /// </remarks>
    public event EventHandler? InstallsChanged;

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

        // After the release, so a listener that reads engines cannot meet this page still
        // holding its own load open.
        InstallsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Opens this page showing what would answer a version, which is where the strip
    /// sends somebody whose workspace asks for something that is not installed.
    /// </summary>
    public void ShowAvailable(string version)
    {
        Query = version;
        OnAvailable = true;
    }

    /// <summary>
    /// Installs the build that answers a requirement, and shows it happening.
    /// </summary>
    /// <remarks>
    /// **One press, and the page it lands on is the one already built to show an install.**
    /// The row's own bar, its cancel and the toasts all belong to this page, so the strip
    /// starts the install here rather than owning a second way of reporting one. The page
    /// is filtered to the release and the card is opened, so what is running is on screen
    /// rather than somewhere in 183 rows.
    ///
    /// The pattern picks the release the same way the resolver picks an install, so a
    /// workspace asking for 4.7 gets the newest 4.7 stable rather than a candidate.
    /// </remarks>
    public async Task InstallForAsync(EngineVersionPattern wanted, bool mono)
    {
        // Nothing has been read if the page has not been opened yet, and the strip can
        // send somebody here before that has happened.
        if (_cards.Count == 0)
        {
            await LoadAsync().ConfigureAwait(true);
        }

        OnAvailable = true;

        if (wanted.BestMatch(_cards.Keys) is not { } tag)
        {
            Query = string.Empty;

            Post(
                ToastTier.Error,
                $"Nothing published matches {wanted}",
                "Check the version this workspace asks for.");

            return;
        }

        var card = _cards[tag];

        Query = tag.ToString();
        card.IsOpen = true;

        var row = card.Rows.FirstOrDefault(candidate =>
            candidate.Build.IsMono == mono
            && candidate.Build.Architecture == HostArchitecture);

        if (row is null)
        {
            Post(
                ToastTier.Error,
                $"Godot {EngineRowViewModel.NameOf(tag)} has no build for this machine",
                $"Nothing published for {HostPlatformText} {EngineBuild.TextFor(HostArchitecture)}"
                + (mono ? " with C# support." : "."));

            return;
        }

        if (row.IsInstalled)
        {
            return;
        }

        await InstallAsync(row).ConfigureAwait(true);
    }

    /// <summary>
    /// Shows the engine directory in whatever file browser this desktop has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// **The directory is created when it is not there.** It is Workbench's own folder and
    /// the first install would create it anyway, so a person who clicks it before
    /// installing anything sees where engines go rather than nothing happening. The
    /// alternative was a link that does nothing until an engine exists.
    /// </para>
    /// <para>
    /// Off the UI thread. On Linux, opening a folder searches PATH for a launcher before it
    /// starts anything, and that reads a disk.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private Task OpenEngineDirectoryAsync() => Task.Run(() =>
    {
        var directory = _settings.EngineDirectory;

        try
        {
            _files.CreateDirectory(directory);
            _platform.OpenInFileBrowser(DirectoryLocation.Parse(directory));
        }
        catch (Exception error) when (error is DirectoryNotFoundException
                                          or IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or ProcessStartException)
        {
            // Nothing on this page can report yet. Step 7 brings the toast host, and this
            // is the first thing that will use it.
        }
    });

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

    partial void OnShowAllArchitecturesChanged(bool value)
    {
        // Every open card regroups. Nothing is fetched again, since what a release holds
        // was read when it opened.
        foreach (var card in Cards)
        {
            card.Arrange();
        }
    }

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

            // **Every manifest, not one per card as it opens.** A card should hold
            // everything it will ever show the moment it is made, so a release that cannot
            // be read is a card that says so rather than one that finds out later.
            // Measured: 183 of them in 1.8 seconds cold, and they are cached without an
            // expiry, so every launch after the first reads them off a disk.
            var manifests = await Task.WhenAll(releases.Releases.Select(async release =>
            {
                try
                {
                    return await _catalogue
                        .ReadManifestAsync(release.Tag, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (EngineCatalogueException)
                {
                    return null;
                }
            })).ConfigureAwait(false);

            return new Loaded(
                releases.Releases,
                manifests.Where(manifest => manifest is not null).ToDictionary(m => m!.Tag, m => m!),
                installed,
                releases.IsStale,
                releases.ReadAt,
                string.Empty);
        }
        catch (EngineCatalogueException error)
        {
            return new Loaded([], new Dictionary<EngineTag, EngineManifest>(), installed, false, default, error.Message);
        }
    }

    private void Apply(Loaded loaded)
    {
        _releases = loaded.Releases;
        _manifests = loaded.Manifests;
        _installed = loaded.Installed;

        Failure = loaded.Failure;
        IsStale = loaded.IsStale;
        StaleNote = loaded.IsStale
            ? $"This list was read {Ago(loaded.ReadAt)} and could not be checked just now."
            : string.Empty;

        // **A card per release, whatever tab is showing and whatever the filter is.** They
        // hold everything they will ever draw, so making them is the point at which the
        // page is complete, and filtering only decides which of them are listed.
        foreach (var release in _releases)
        {
            if (!_cards.ContainsKey(release.Tag))
            {
                _cards[release.Tag] = new ReleaseCardViewModel(
                    release,
                    _manifests.GetValueOrDefault(release.Tag),
                    this);
            }

            _cards[release.Tag].CountInstalled(_installed);
        }

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
        var built = new List<EngineRowViewModel>();

        if (OnAvailable)
        {
            // The cards are their own list. A card holds what it read when it opened, so
            // it is kept across a filter rather than built again.
            var cards = new List<ReleaseCardViewModel>();

            foreach (var release in _releases)
            {
                if (_channel is { } channel && release.Channel != channel)
                {
                    continue;
                }

                if (query.Length > 0
                    && !EngineRowViewModel.NameOf(release.Tag).Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_cards.TryGetValue(release.Tag, out var card))
                {
                    cards.Add(card);
                }
            }

            Cards.Clear();
            Cards.AddRange(cards);

            EmptyNote = query.Length > 0 ? $"No versions match {query}" : "Nothing here yet";
            Rows.Clear();

            OnPropertyChanged(nameof(IsEmpty));

            return;
        }

        {
            foreach (var engine in _installed)
            {
                var row = new EngineRowViewModel(engine, _settings.DefaultEngine, _paths, this);

                if (query.Length == 0
                    || row.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    built.Add(row);
                }
            }
        }

        EmptyNote = query.Length > 0 ? $"No versions match {query}" : "Nothing here yet";

        Rows.Clear();
        Rows.AddRange(built);

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// The notes for a release, or null when the feed carried none. An imported engine of
    /// a version nobody published has none either, which is why this is a lookup.
    /// </summary>
    public WebAddress? NotesFor(EngineTag tag) =>
        _releases.FirstOrDefault(release => release.Tag == tag)?.Notes;

    /// <summary>
    /// Starts this engine's project manager, which is the window Godot opens when it is
    /// run outside a project.
    /// </summary>
    /// <remarks>
    /// The flag is <c>--project-manager</c>, read off <c>--help</c> on 4.7.1 rather than
    /// remembered: "Start the project manager, even if a project is auto-detected."
    ///
    /// Detached rather than merely unwaited on. An engine is a person's next few hours of
    /// work and Workbench is a launcher they may well close, so the two do not share a
    /// fate. What that costs per platform is <see cref="IPlatformServices.StartDetached"/>.
    /// </remarks>
    public void OpenProjectManager(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        Task.Run(() =>
        {
            try
            {
                _platform.StartDetached(
                    ProcessRequest.Command(engine.Executable, "--project-manager"));
            }
            catch (ProcessStartException)
            {
                Post(
                    ToastTier.Error,
                    $"Could not start Godot {EngineRowViewModel.NameOf(engine.Tag)}",
                    Shorten(engine.Executable, ToastPathLength));
            }
        });
    }

    /// <summary>Shows a folder in the file browser. Off the UI thread, like the root does.</summary>
    public void OpenFolder(string directory) => Task.Run(() =>
    {
        try
        {
            _platform.OpenInFileBrowser(DirectoryLocation.Parse(directory));
        }
        catch (Exception error) when (error is DirectoryNotFoundException
                                          or ArgumentException
                                          or ProcessStartException)
        {
            // Our own words with a path written for the card, rather than the operating
            // system's, which puts the whole path in and runs off the edge.
            Post(ToastTier.Error, "Could not open the folder", Shorten(directory, ToastPathLength));
        }
    });

    /// <summary>
    /// Puts a path on the clipboard. The clipboard belongs to a window, so the view hands
    /// this in and the page never reaches for one.
    /// </summary>
    public Func<string, Task>? Copier { get; set; }

    public async Task CopyAsync(string text)
    {
        if (Copier is null)
        {
            return;
        }

        // The whole path is copied and a shortened one is shown, since a toast is 352 wide
        // and a path is not.
        await Copier(text).ConfigureAwait(true);

        Post(ToastTier.Ok, "Path copied", Shorten(text, ToastPathLength));
    }

    /// <summary>
    /// A path written for a fixed width. **A path shown in a row, a bar or a toast comes
    /// through here**, so the middle is elided and the end, which is the part that says
    /// which folder it is, survives. The uninstall dialog is the one place that overrides
    /// that and shows the whole thing, since it is evidence rather than a readout.
    /// </summary>
    public string Shorten(string path, int length) => _paths.Shorten(path, length);

    /// <summary>
    /// Names the machine default. **Nothing is chosen automatically**, here or when the
    /// default is removed, which is what the spec asks for.
    /// </summary>
    public void SetDefault(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        _settings.SetDefaultEngine(engine.Id);

        _ = LoadAsync();
    }

    /// <summary>
    /// Asks first, then removes. **Deletes only what Workbench installed**, and the dialog
    /// says which of the two it is about to do before it is pressed.
    /// </summary>
    public async Task RemoveAsync(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        if (Confirm is null || !await Confirm(engine).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            await Task.Run(() => _store.RemoveAsync(engine, CancellationToken.None)).ConfigureAwait(true);

            // Uninstalling the default leaves the machine without one. Choosing the next is
            // a person's act and never this method's.
            if (_settings.DefaultEngine == engine.Id)
            {
                _settings.SetDefaultEngine(null);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Post(
                ToastTier.Error,
                $"Could not remove Godot {EngineRowViewModel.NameOf(engine.Tag)}",
                $"{Shorten(engine.Directory, ToastPathLength)} could not be deleted.");
        }

        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>Asks the person before an engine is removed. The view supplies the dialog.</summary>
    public Func<InstalledEngine, Task<bool>>? Confirm { get; set; }

    /// <summary>Adds an engine already on this machine. The view supplies the folder picker.</summary>
    public Func<Task<string?>>? Picker { get; set; }

    [RelayCommand]
    private async Task AddExistingAsync()
    {
        if (Picker is null || await Picker().ConfigureAwait(true) is not { Length: > 0 } directory)
        {
            return;
        }

        var added = await Task.Run(() => _store.ImportAsync(directory, CancellationToken.None)).ConfigureAwait(true);

        if (added is null)
        {
            Post(
                ToastTier.Error,
                "That folder holds no Godot editor",
                "Pick the folder the editor itself is in, not one above it.");

            return;
        }

        Post(
            ToastTier.Ok,
            $"Godot {EngineRowViewModel.NameOf(added.Tag)} added",
            Shorten(added.Directory, ToastPathLength));

        OnAvailable = false;

        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>Opens a release notes page in the browser.</summary>
    public void OpenNotes(WebAddress notes) => Task.Run(() =>
    {
        try
        {
            _platform.OpenInBrowser(notes);
        }
        catch (Exception error) when (error is ProcessStartException or ArgumentException)
        {
            Post(ToastTier.Error, "Could not open the release notes", error.Message);
        }
    });

    /// <summary>One build row, wired to the install and the cancel.</summary>
    public EngineBuildViewModel BuildFor(EngineBuild build) =>
        new(build, _installed.Any(engine => engine.Id == build.Id), InstallAsync, Cancel);

    /// <summary>
    /// Installs one build. Several run at once behind the installer's own cap, and each
    /// carries its own token, so cancelling one leaves the others alone.
    /// </summary>
    private async Task InstallAsync(EngineBuildViewModel row)
    {
        if (_running.ContainsKey(row.Build.Id))
        {
            return;
        }

        using var stopping = new CancellationTokenSource();

        _running[row.Build.Id] = stopping;
        row.Started();

        try
        {
            await _installer.InstallAsync(
                row.Build,
                new Progress<EngineInstallProgress>(row.Report),
                stopping.Token).ConfigureAwait(true);

            row.Stopped(installed: true);

            Post(
                ToastTier.Ok,
                $"Godot {EngineRowViewModel.NameOf(row.Build.Tag)} installed",
                $"Unpacked to {Shorten(Path.Combine(_settings.EngineDirectory, row.Build.Id.DirectoryName), ToastPathLength)}.",
                new ToastAction("Show in Installed", () => OnAvailable = false) { IsPrimary = true });

            await LoadAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancelling is a person's own doing and is already visible in place, so it
            // says nothing.
            row.Stopped(installed: false);
        }
        catch (Exception error) when (error is EngineInstallException or EngineCatalogueException)
        {
            row.Stopped(installed: false);

            Post(
                ToastTier.Error,
                $"Could not install Godot {EngineRowViewModel.NameOf(row.Build.Tag)}",
                error.Message,
                new ToastAction("Retry", () => _ = InstallAsync(row)));
        }
        finally
        {
            _running.Remove(row.Build.Id);
        }
    }

    private void Cancel(EngineBuildViewModel row)
    {
        if (_running.TryGetValue(row.Build.Id, out var stopping))
        {
            stopping.Cancel();
        }
    }

    /// <summary>
    /// Toasts are posted rather than shown, since an install reports from wherever it ran.
    /// An error stays until it is dismissed, which is the library's own dwell.
    /// </summary>
    private void Post(ToastTier tier, string title, string? body, ToastAction? action = null) =>
        _toasts.Post(new ToastRequest
        {
            Tier = tier,
            Title = title,
            Body = body,
            Actions = action is null ? [] : [action],
        });

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
        IReadOnlyDictionary<EngineTag, EngineManifest> Manifests,
        IReadOnlyList<InstalledEngine> Installed,
        bool IsStale,
        DateTimeOffset ReadAt,
        string Failure);
}
