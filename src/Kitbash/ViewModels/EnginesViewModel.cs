using Avalonia.Collections;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Settings;
using Kitbash.Ui.Toasts;

namespace Kitbash.ViewModels;

/// <summary>
/// The Godot engines page. What is installed, what is available, and what the machine has
/// to say about both.
/// </summary>
public sealed partial class EnginesViewModel : ViewModelBase
{
    /// <summary>Roughly what fits the status bar beside the counts.</summary>
    private const int RootLength = 46;

    /// <summary>Roughly what fits a toast card, which is 352 wide.</summary>
    private const int ToastPathLength = 44;


    private readonly IEngineCatalogue _catalogue;
    private readonly IEngineRepositories _repositories;
    private readonly IEngineRepositoryList _repositoryList;
    private readonly IEngineUpdater _updater;
    private readonly IEngineTemplates _templates;
    private readonly IEngineStore _store;
    private readonly IGodotSettings _settings;
    private readonly IPathShortener _paths;
    private readonly IPlatformServices _platform;
    private readonly IFileSystem _files;
    private readonly IEngineInstaller _installer;
    private readonly IEngineFiles _engineFiles;
    private readonly IToastService _toasts;
    private readonly IAfterLaunchSettings _afterLaunch;
    private readonly IAfterLaunchActions _actions;

    /// <summary>One token per build being installed, so each cancels on its own.</summary>
    private readonly Dictionary<EngineId, CancellationTokenSource> _running = [];
    private readonly SemaphoreSlim _loading = new(1, 1);

    /// <summary>Every card by <see cref="EngineRelease.Key"/>, official and repository alike.</summary>
    private readonly Dictionary<string, ReleaseCardViewModel> _cards = new(StringComparer.Ordinal);

    /// <summary>Engines whose export templates are downloading, by install folder.</summary>
    private readonly HashSet<string> _addingTemplates = new(StringComparer.Ordinal);

    private IReadOnlyList<EngineRelease> _releases = [];
    private IReadOnlyList<EngineRelease> _repositoryReleases = [];
    private IReadOnlyDictionary<string, EngineManifest> _manifests = new Dictionary<string, EngineManifest>();
    private IReadOnlyList<EngineRepositorySource> _sources = [];
    private IReadOnlyList<InstalledEngine> _installed = [];
    private EngineChannel? _channel = EngineChannel.Stable;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>The tab that opens. Both halves are read either way.</summary>
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

    /// <summary>
    /// What went wrong reading engine repositories: one that could not be reached, or
    /// releases whose tag could not be read. Empty when nothing did.
    /// </summary>
    [ObservableProperty]
    private string _repositoryNote = string.Empty;

    /// <summary>
    /// The open workspace's folder, so its own repositories are listed beside the global
    /// ones. Set by the launcher, which knows which workspace is open.
    /// </summary>
    public string? WorkspaceRoot { get; set; }

    public EnginesViewModel(
        IEngineCatalogue catalogue,
        IEngineRepositories repositories,
        IEngineRepositoryList repositoryList,
        IEngineUpdater updater,
        IEngineTemplates templates,
        IEngineStore store,
        IGodotSettings settings,
        IPathShortener paths,
        IPlatformServices platform,
        IFileSystem files,
        IEngineInstaller installer,
        IEngineFiles engineFiles,
        IToastService toasts,
        IAfterLaunchSettings afterLaunch,
        IAfterLaunchActions actions)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(repositoryList);
        ArgumentNullException.ThrowIfNull(updater);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(engineFiles);
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(afterLaunch);
        ArgumentNullException.ThrowIfNull(actions);

        _catalogue = catalogue;
        _repositories = repositories;
        _repositoryList = repositoryList;
        _updater = updater;
        _templates = templates;
        _store = store;
        _settings = settings;
        _paths = paths;
        _platform = platform;
        _files = files;
        _installer = installer;
        _engineFiles = engineFiles;
        _toasts = toasts;
        _afterLaunch = afterLaunch;
        _actions = actions;

        // Not read here. It fetches over a network and the window would wait on it.
        InstallRoot = _paths.Shorten(_settings.EngineDirectory, RootLength);
    }

    /// <summary>
    /// Whether every processor is shown or only this machine's. View state, so it is
    /// global to the page and resets on relaunch.
    /// </summary>
    [ObservableProperty]
    private bool _showAllArchitectures;

    /// <summary>
    /// The rows on screen. AvaloniaList rather than ObservableCollection, so a refill is
    /// two notifications instead of one per row. Keep it as one instance and clear it
    /// rather than assigning a new ItemsSource, which would discard the containers.
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

    /// <summary>Empty below one, so a zero is never drawn.</summary>
    public bool HasInstalled => InstalledCount > 0;

    public bool HasFailed => Failure.Length > 0;

    public bool HasRepositoryNote => RepositoryNote.Length > 0;

    public bool IsEmpty => (OnAvailable ? Cards.Count : Rows.Count) == 0 && !HasFailed;

    /// <summary>
    /// Raised after a read, so anything else showing what is installed can follow it.
    /// </summary>
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

        // After the release, so a listener that reads engines does not meet a held lock.
        InstallsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Brings a card into view. Set by the page, since scrolling belongs to a list and a
    /// view model has none.
    /// </summary>
    public Action<ReleaseCardViewModel>? Reveal { get; set; }

    /// <summary>
    /// Opens the card holding the build that answers a requirement and scrolls to it.
    /// Null when nothing published answers, which is said in a toast.
    /// </summary>
    /// <param name="advice">The second line of that toast, since only the caller knows who asked.</param>
    public async Task<EngineBuildViewModel?> RevealForAsync(
        EngineVersionPattern wanted, bool mono, string advice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(advice);

        // The page may never have been opened, so nothing has been read yet.
        if (_cards.Count == 0)
        {
            await LoadAsync().ConfigureAwait(true);
        }

        OnAvailable = true;

        var official = _cards.Values.Where(candidate => candidate.Release.Repository is null).ToList();

        if (wanted.BestMatch(official.Select(candidate => candidate.Tag)) is not { } tag)
        {
            Post(ToastTier.Error, $"Nothing published matches {wanted}", advice);

            return null;
        }

        var card = official.First(candidate => candidate.Tag == tag);

        card.IsOpen = true;
        Reveal?.Invoke(card);

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
        }

        return row;
    }

    /// <summary>
    /// Installs the build that answers a requirement, and shows it happening.
    /// </summary>
    public async Task InstallForAsync(EngineVersionPattern wanted, bool mono)
    {
        var row = await RevealForAsync(wanted, mono, "Check the version this workspace asks for.")
            .ConfigureAwait(true);

        if (row is not { IsInstalled: false })
        {
            return;
        }

        await InstallAsync(row).ConfigureAwait(true);
    }

    /// <summary>
    /// Installs what a workspace naming a repository asks for, into its slot for a newest
    /// pin, and shows it happening on the build's own card.
    /// </summary>
    public async Task InstallForAsync(EngineRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        var name = requirement.RepositoryName ?? string.Empty;

        if (requirement.Repository is null || requirement.Version is not { } wanted)
        {
            Post(ToastTier.Error, $"No engine repository is named {name}", "Check the repository this workspace names.");

            return;
        }

        EngineBuild? build;

        try
        {
            build = await Task.Run(() => _updater.FindAsync(requirement, refresh: false, CancellationToken.None))
                .ConfigureAwait(true);
        }
        catch (EngineCatalogueException error)
        {
            Post(ToastTier.Error, $"Could not read {name}", error.Message);

            return;
        }

        if (build is null)
        {
            Post(
                ToastTier.Error,
                $"Nothing from {name} matches {wanted}",
                $"Nothing published for {HostPlatformText} {EngineBuild.TextFor(HostArchitecture)}"
                + (requirement.NeedsDotnet ? " with C# support." : "."));

            return;
        }

        var key = $"{build.Id.Repository}/{build.Release}";

        if (!_cards.ContainsKey(key))
        {
            await LoadAsync().ConfigureAwait(true);
        }

        OnAvailable = true;

        // A repository build is custom, so a filter on an official channel would hide the
        // card the install is about to draw on.
        if (_channel is { } shown && shown != EngineChannel.Custom)
        {
            ShowChannel(EngineChannel.Custom);
        }

        EngineBuildViewModel row;

        if (_cards.TryGetValue(key, out var card))
        {
            card.IsOpen = true;
            Reveal?.Invoke(card);
            row = card.Rows.FirstOrDefault(candidate => candidate.Build.FileName == build.FileName) ?? BuildFor(build);
        }
        else
        {
            // Not listed, which a repository in no list here would be, so it installs unseen.
            row = BuildFor(build);
        }

        if (!row.IsInstalled || requirement.Slot is not null)
        {
            await InstallAsync(row, requirement).ConfigureAwait(true);
        }
    }

    /// <summary>The name a repository goes by here, or its own name when no list gives one.</summary>
    public string RepositoryLabel(EngineRepositoryAddress address) =>
        _sources.FirstOrDefault(source => source.Address == address)?.Name ?? address.Name;

    /// <summary>
    /// Shows the engine directory in whatever file browser this desktop has.
    /// </summary>
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
            // TODO no toast host on this page yet, so a failure here is silent.
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

    // One flag per choice in the segmented row, bound both ways, so a filter this page
    // moves by itself shows as chosen there.
    public bool ShowsAll { get => _channel is null; set => Choose(value, null); }

    public bool ShowsStable { get => _channel == EngineChannel.Stable; set => Choose(value, EngineChannel.Stable); }

    public bool ShowsRc { get => _channel == EngineChannel.Rc; set => Choose(value, EngineChannel.Rc); }

    public bool ShowsBeta { get => _channel == EngineChannel.Beta; set => Choose(value, EngineChannel.Beta); }

    public bool ShowsDev { get => _channel == EngineChannel.Dev; set => Choose(value, EngineChannel.Dev); }

    public bool ShowsCustom { get => _channel == EngineChannel.Custom; set => Choose(value, EngineChannel.Custom); }

    private void Choose(bool chosen, EngineChannel? channel)
    {
        if (chosen && _channel != channel)
        {
            ShowChannel(channel);
        }
    }

    private void ShowChannel(EngineChannel? channel)
    {
        _channel = channel;

        OnPropertyChanged(nameof(ShowsAll));
        OnPropertyChanged(nameof(ShowsStable));
        OnPropertyChanged(nameof(ShowsRc));
        OnPropertyChanged(nameof(ShowsBeta));
        OnPropertyChanged(nameof(ShowsDev));
        OnPropertyChanged(nameof(ShowsCustom));

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
        // Every open card regroups. Nothing is fetched again.
        foreach (var card in Cards)
        {
            card.Arrange();
        }
    }

    partial void OnUpdateVersionChanged(string value) => OnPropertyChanged(nameof(HasUpdate));

    partial void OnInstalledCountChanged(int value) => OnPropertyChanged(nameof(HasInstalled));

    partial void OnRepositoryNoteChanged(string value) => OnPropertyChanged(nameof(HasRepositoryNote));

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
            // A directory that cannot be read is an empty list, not a failed load.
            installed = [];
        }

        var repositories = await ReadRepositoriesAsync(refresh).ConfigureAwait(false);

        try
        {
            var releases = await _catalogue.ReadReleasesAsync(refresh, CancellationToken.None).ConfigureAwait(false);

            // Every manifest, so a card holds everything it will draw the moment it is
            // made. Cached with no expiry, so only the first run pays for the fetch.
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

            var all = new Dictionary<string, EngineManifest>(repositories.Manifests, StringComparer.Ordinal);

            foreach (var manifest in manifests.OfType<EngineManifest>())
            {
                all[manifest.Tag.ToString()] = manifest;
            }

            return new Loaded(
                releases.Releases,
                all,
                installed,
                releases.IsStale,
                releases.ReadAt,
                string.Empty,
                repositories);
        }
        catch (EngineCatalogueException error)
        {
            return new Loaded([], repositories.Manifests, installed, false, default, error.Message, repositories);
        }
    }

    /// <summary>
    /// Every repository the global list and the open workspace name, once each by address,
    /// with every release and what it holds. A repository that cannot be read is said in
    /// the note and leaves the rest of the page alone.
    /// </summary>
    private async Task<RepositoriesRead> ReadRepositoriesAsync(bool refresh)
    {
        IReadOnlyList<EngineRepositorySource> sources;

        try
        {
            sources = WorkspaceRoot is { } root ? _repositoryList.ReadFor(root) : _repositoryList.ReadGlobal();
        }
        catch (Exception error) when (error is SettingsFileUnreadableException or IOException or UnauthorizedAccessException)
        {
            sources = [];
        }

        sources = [.. sources.DistinctBy(source => source.Address)];

        var releases = new List<EngineRelease>();
        var manifests = new Dictionary<string, EngineManifest>(StringComparer.Ordinal);
        var notes = new List<string>();

        foreach (var source in sources)
        {
            var repository = _repositories.For(source.Address);

            try
            {
                var read = await repository.ReadReleasesAsync(refresh, CancellationToken.None).ConfigureAwait(false);

                foreach (var release in read.Releases)
                {
                    try
                    {
                        manifests[release.Key] = await repository
                            .ReadManifestAsync(release.Name, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch (EngineCatalogueException)
                    {
                        // The card says it could not be read.
                    }
                }

                releases.AddRange(read.Releases);

                if (read.Skipped > 0)
                {
                    notes.Add(read.Skipped == 1
                        ? $"1 release from {source.Name} has a tag that could not be read."
                        : $"{read.Skipped} releases from {source.Name} have a tag that could not be read.");
                }

                if (read.IsStale)
                {
                    notes.Add($"{source.Name} could not be checked just now.");
                }
            }
            catch (EngineCatalogueException)
            {
                notes.Add($"{source.Name} could not be reached.");
            }
        }

        return new RepositoriesRead(sources, releases, manifests, string.Join(" ", notes));
    }

    private void Apply(Loaded loaded)
    {
        _releases = loaded.Releases;
        _repositoryReleases = loaded.Repositories.Releases;
        _sources = loaded.Repositories.Sources;
        _manifests = loaded.Manifests;
        _installed = loaded.Installed;
        RepositoryNote = loaded.Repositories.Note;

        Failure = loaded.Failure;
        IsStale = loaded.IsStale;
        StaleNote = loaded.IsStale
            ? $"This list was read {Ago(loaded.ReadAt)} and could not be checked just now."
            : string.Empty;

        // A card per release, whatever the tab and filter are. Filtering only decides
        // which of them are listed.
        foreach (var release in _releases.Concat(_repositoryReleases))
        {
            if (!_cards.TryGetValue(release.Key, out var card))
            {
                card = new ReleaseCardViewModel(release, _manifests.GetValueOrDefault(release.Key), this);
                _cards[release.Key] = card;
            }

            card.CountInstalled(_installed);
        }

        InstallRoot = _paths.Shorten(_settings.EngineDirectory, RootLength);
        InstalledCount = _installed.Count;
        ReleaseCount = _releases.Count + _repositoryReleases.Count;
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
            // Cards are kept across a filter, since each holds what it read when it opened.
            var cards = new List<ReleaseCardViewModel>();

            // Newest first across every source, so a repository build sits among the
            // official releases of its time.
            foreach (var release in _releases
                         .Concat(_repositoryReleases)
                         .OrderByDescending(release => release.PublishedAt))
            {
                if (_channel is { } channel && release.Channel != channel)
                {
                    continue;
                }

                if (!_cards.TryGetValue(release.Key, out var card))
                {
                    continue;
                }

                if (query.Length > 0
                    && !card.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && !card.Channel.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cards.Add(card);
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

    /// <summary>The notes for a release, or null when the feed carried none.</summary>
    public WebAddress? NotesFor(EngineTag tag) =>
        _releases.FirstOrDefault(release => release.Tag == tag)?.Notes;

    /// <summary>
    /// The notes for the release an install came from. A repository release's page on its
    /// forge, since that is where a fork writes what changed.
    /// </summary>
    public WebAddress? NotesFor(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        return engine.Repository is { } address
            ? _repositoryReleases.FirstOrDefault(release =>
                release.Repository == address && release.Build == engine.Id.Build)?.Notes
            : NotesFor(engine.Tag);
    }

    /// <summary>True while an engine's export templates are downloading.</summary>
    public bool IsAddingTemplates(InstalledEngine engine) => _addingTemplates.Contains(engine.Directory);

    /// <summary>
    /// Downloads the export templates an engine's release published, into the folder Godot
    /// reads them from. Never done without being asked.
    /// </summary>
    public async Task AddTemplatesAsync(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        if (!_addingTemplates.Add(engine.Directory))
        {
            return;
        }

        var name = EngineRowViewModel.DisplayName(engine.Id);

        Rebuild();

        try
        {
            var done = await Task.Run(() => _templates.InstallAsync(engine, progress: null, CancellationToken.None))
                .ConfigureAwait(true);

            Post(ToastTier.Ok, $"Export templates added for Godot {name}", $"Godot finds them as {done.Record.Templates}.");
        }
        catch (Exception error) when (error is EngineInstallException or EngineCatalogueException
            or IOException or UnauthorizedAccessException)
        {
            Post(
                ToastTier.Error,
                $"Could not add export templates for Godot {name}",
                error.Message,
                new ToastAction("Retry", () => _ = AddTemplatesAsync(engine)));
        }
        finally
        {
            _addingTemplates.Remove(engine.Directory);
        }

        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Deletes the export templates Kitbash installed for an engine. Every build of the
    /// version reads the same folder, so each install that recorded it forgets it.
    /// </summary>
    public async Task RemoveTemplatesAsync(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var folder = engine.Record.Templates;

        if (folder.Length == 0)
        {
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                _templates.Remove(folder);

                foreach (var holder in _installed.Where(other => other.Record.Templates == folder))
                {
                    _store.RecordTemplates(holder, string.Empty);
                }
            }).ConfigureAwait(true);

            Post(ToastTier.Ok, "Export templates removed", $"{folder} was deleted.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Post(ToastTier.Error, "Could not remove export templates", error.Message);
        }

        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Starts this engine's project manager, which is the window Godot opens when it is
    /// run outside a project.
    /// </summary>
    public void OpenProjectManager(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        Task.Run(() =>
        {
            try
            {
                _platform.StartDetached(
                    ProcessRequest.Command(engine.Executable, "--project-manager"));

                // Only once the desktop has taken it, so a start that failed leaves the
                // launcher up with its toast on screen.
                _actions.Apply(_afterLaunch.AfterProjectManager);
            }
            catch (ProcessStartException)
            {
                Post(
                    ToastTier.Error,
                    $"Could not start Godot {EngineRowViewModel.DisplayName(engine.Id)}",
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
            // Our own words with an elided path. The platform message uses the full path
            // and overflows the card.
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

        // The whole path is copied, and an elided one shown to fit the toast.
        await Copier(text).ConfigureAwait(true);

        Post(ToastTier.Ok, "Path copied", Shorten(text, ToastPathLength));
    }

    /// <summary>
    /// Elides the middle of a path to fit a width, keeping the end. Every path shown in a
    /// row, a bar or a toast goes through here.
    /// </summary>
    public string Shorten(string path, int length) => _paths.Shorten(path, length);

    /// <summary>Names the machine default. Nothing is ever chosen automatically.</summary>
    public void SetDefault(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        _settings.SetDefaultEngine(engine.Id);

        _ = LoadAsync();
    }

    /// <summary>
    /// Asks first, then removes. Deletes files only for an install Kitbash made. An
    /// imported engine is forgotten and left on disk.
    /// </summary>
    public async Task RemoveAsync(InstalledEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var installed = _installed;
        var owned = engine.IsImported ? null : _templates.OwnedBy(engine, installed);
        var templatesNote = owned is null
            ? null
            : $"Its export templates go too, {Size(await Task.Run(() => _templates.SizeOf(owned)).ConfigureAwait(true))}.";

        if (Confirm is null || !await Confirm(engine, templatesNote).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            await Task.Run(async () =>
            {
                await _store.RemoveAsync(engine, CancellationToken.None).ConfigureAwait(false);

                if (owned is not null)
                {
                    _templates.Remove(owned);
                }
            }).ConfigureAwait(true);

            // Uninstalling the default leaves the machine without one. Nothing is promoted.
            if (_settings.DefaultEngine == engine.Id)
            {
                _settings.SetDefaultEngine(null);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Post(
                ToastTier.Error,
                $"Could not remove Godot {EngineRowViewModel.DisplayName(engine.Id)}",
                $"{Shorten(engine.Directory, ToastPathLength)} could not be deleted.");
        }

        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Asks the person before an engine is removed, with a line about its export templates
    /// when they go too. The view supplies the dialog.
    /// </summary>
    public Func<InstalledEngine, string?, Task<bool>>? Confirm { get; set; }

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
    private Task InstallAsync(EngineBuildViewModel row) => InstallAsync(row, requirement: null);

    /// <summary>
    /// Installs one build. With a requirement naming a repository it goes where that
    /// requirement says, which for a newest pin is its slot.
    /// </summary>
    private async Task InstallAsync(EngineBuildViewModel row, EngineRequirement? requirement)
    {
        if (_running.ContainsKey(row.Build.Id))
        {
            return;
        }

        using var stopping = new CancellationTokenSource();

        _running[row.Build.Id] = stopping;
        row.Started();

        var name = EngineRowViewModel.DisplayName(row.Build.Id);

        try
        {
            var progress = new Progress<EngineInstallProgress>(row.Report);

            var engine = requirement?.RepositoryName is not null
                ? await Task.Run(() => _updater.InstallAsync(requirement, row.Build, progress, stopping.Token))
                    .ConfigureAwait(true)
                : await _installer.InstallAsync(row.Build, progress, stopping.Token).ConfigureAwait(true);

            row.Stopped(installed: true);

            Post(
                ToastTier.Ok,
                $"Godot {name} installed",
                $"Unpacked to {Shorten(engine.Directory, ToastPathLength)}.",
                new ToastAction("Show in Installed", () => OnAvailable = false) { IsPrimary = true });

            await LoadAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancelling reports nothing. It is already visible in place.
            row.Stopped(installed: false);
        }
        catch (Exception error) when (error is EngineInstallException or EngineCatalogueException
            or IOException or UnauthorizedAccessException)
        {
            row.Stopped(installed: false);

            Post(
                ToastTier.Error,
                $"Could not install Godot {name}",
                error.Message,
                new ToastAction("Retry", () => _ = InstallAsync(row, requirement)));
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

    /// <summary>Posted, not shown, since an install reports from any thread.</summary>
    private void Post(ToastTier tier, string title, string? body, ToastAction? action = null) =>
        _toasts.Post(new ToastRequest
        {
            Tier = tier,
            Title = title,
            Body = body,
            Actions = action is null ? [] : [action],
        });

    /// <summary>
    /// The newest stable release that is newer than the newest stable installed. Empty
    /// when there is none.
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
        IReadOnlyDictionary<string, EngineManifest> Manifests,
        IReadOnlyList<InstalledEngine> Installed,
        bool IsStale,
        DateTimeOffset ReadAt,
        string Failure,
        RepositoriesRead Repositories);

    /// <summary>What the engine repositories said, and what went wrong asking them.</summary>
    private sealed record RepositoriesRead(
        IReadOnlyList<EngineRepositorySource> Sources,
        IReadOnlyList<EngineRelease> Releases,
        IReadOnlyDictionary<string, EngineManifest> Manifests,
        string Note);
}
