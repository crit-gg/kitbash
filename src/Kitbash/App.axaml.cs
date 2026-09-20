using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Kitbash.Core;
using Kitbash.Core.Git;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;
using Kitbash.Settings;
using Kitbash.Tools;
using Kitbash.Updates;
using Kitbash.ViewModels;
using Kitbash.Ui;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Kitbash.Ui.Toasts;
using Kitbash.Views;
using Kitbash.Workspaces;

namespace Kitbash;

public partial class App : Application
{
    private ServiceProvider? _services;

    /// <summary>
    /// A link with nowhere to go yet. A desktop can hand one over before the launcher has
    /// a window, so it waits here rather than being dropped. UI thread only.
    /// </summary>
    private string? _pending;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// How long the launcher waits on the feed. The splash is up while it is asked, so this
    /// bounds a wait a person can see rather than a blank screen. A check that does not
    /// answer in time is dropped and tried again at the next launch, which costs one launch
    /// and never costs a start.
    /// </summary>
    private static readonly TimeSpan CheckLimit = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long a check runs before it is worth saying so. A feed that answers sooner never
    /// draws the row at all, since a row going up and straight back down is a flash on every
    /// ordinary launch.
    /// </summary>
    private static readonly TimeSpan CheckPatience = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The least time the row is up once it has appeared. Measured from the report, so a
    /// check that ran long has already paid it and waits no further.
    /// </summary>
    private static readonly TimeSpan CheckDwell = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// The least time the splash is up before the launcher replaces it. A splash that
    /// flashes past reads as a fault rather than as a start.
    /// </summary>
    private static readonly TimeSpan SplashFloor = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long a full bar is held before the restart takes the bar back to indeterminate.
    /// The fill takes 180ms to travel, so moving on from the last report any sooner would
    /// mean a hundred percent was never seen.
    /// </summary>
    private static readonly TimeSpan SplashSettle = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// How long the splash takes to fade up. Measured from the same moment as SplashFloor,
    /// so the floor has to leave room for it.
    /// </summary>
    private static readonly TimeSpan SplashFade = TimeSpan.FromSeconds(0.5f);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = BuildServices();

            // The splash is the only window until the launcher opens, and its closing would
            // otherwise be a last window closing and end the app before the launcher had
            // opened. Put back in Open.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += (_, _) => _services?.Dispose();

            // Windows and Linux hand a link over as an ordinary argument to a new copy.
            // Read before the lock, since the copy that already holds it is the one that
            // has to be given the link.
            _pending = desktop.Args?.FirstOrDefault(argument => DeepLink.TryParse(argument, out _));

            // Before the update and before any window. A copy that is not the first has
            // already asked the first to come forward, so it leaves without drawing.
            if (!Hold(_pending ?? string.Empty))
            {
                // Posted rather than called. The main loop has not started yet, and
                // shutting the dispatcher down before it does makes it throw on the way
                // in. Following a link is the common way to be the second copy, so this
                // path has to end in an ordinary exit.
                Dispatcher.UIThread.Post(() => desktop.Shutdown());

                return;
            }

            // macOS never starts a second copy for a link. It hands one to the app that is
            // already running, which Avalonia raises as an activation and nothing else does.
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
            {
                activatable.Activated += OnActivated;
            }

            // Before the first read of a settings file, since one that will not parse has
            // nowhere to report itself this early and would otherwise stop the launch.
            Repair();

            _ = StartAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Takes the single launcher lock, and listens for a later copy asking this one to
    /// come forward. False means this copy is the later one and should go.
    /// </summary>
    private bool Hold(string message)
    {
        if (_services?.GetService<ISingleInstance>() is not { } instance)
        {
            return true;
        }

        instance.AskedToComeForward += (_, asked) => ComeForward(asked.Message);

        return instance.TryHold(message);
    }

    /// <summary>
    /// Brings the launcher to the front for somebody who started a second copy, and shows
    /// whatever link that copy was following. The event arrives on a background thread, so
    /// the window is touched through the dispatcher.
    /// </summary>
    private void ComeForward(string message) =>
        _services?.GetService<IUiDispatcher>()?.Post(() =>
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            {
                return;
            }

            if (window.WindowState is WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Show();
            window.Activate();

            // A copy running an older build sends one byte rather than a link, so what
            // arrives is checked before it is kept.
            if (!DeepLink.TryParse(message, out _))
            {
                return;
            }

            _pending = message;
            Follow(window);
        });

    /// <summary>
    /// macOS hands a link to the app that is already running rather than starting another
    /// copy. Windows and Linux reach the same place through the arguments instead.
    /// </summary>
    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e is not ProtocolActivatedEventArgs { Kind: ActivationKind.OpenUri } opened)
        {
            return;
        }

        _pending = opened.Uri.ToString();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            Follow(window);
        }
    }

    /// <summary>
    /// Shows what a link asked for, once there is a launcher to show it in. Called with
    /// the splash still up does nothing, and the link waits for the window instead.
    /// </summary>
    private void Follow(Window window)
    {
        if (_pending is not { } text || window is not LauncherWindow launcher)
        {
            return;
        }

        _pending = null;

        if (DeepLink.TryParse(text, out var link))
        {
            _ = launcher.FollowAsync(link);
        }
    }

    /// <summary>
    /// The splash, then the update, then the launcher. **Every path through this ends with a
    /// window or with the app gone**, because an app that fails to update and shows nothing is
    /// worse than one that never tried, and a splash nothing replaces is worse than both.
    /// </summary>
    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var splash = BuildSplash(_services?.GetService<IApplicationVersion>()?.Current ?? string.Empty);

        // Dismissing the splash ends the app from inside the window itself, so nothing after
        // this may open one. It also stops a download that is still running.
        using var dismissed = new CancellationTokenSource();

        splash.Closed += (_, _) => dismissed.Cancel();

        // The main window until the launcher exists, so a second copy asking this one to
        // come forward has something to raise.
        desktop.MainWindow = splash;

        var since = Stopwatch.StartNew();

        await splash.ShowAsync();

        try
        {
            await UpdateAsync(splash, dismissed.Token);
        }
        catch (Exception exception)
        {
            // Nothing above rethrows on purpose, so reaching here is a fault rather than
            // a failed update. The copy on the machine still has to open either way.
            _services?.GetService<UpdateLog>()?.Say("update could not run", exception);
        }

        if (dismissed.IsCancellationRequested)
        {
            return;
        }

        // Nothing is being waited on any more, so the row goes and the card collapses back
        // to the mark and the name. Reached by a feed with nothing to offer and by a
        // download that failed alike.
        splash.IsProgressVisible = false;

        try
        {
            LoadTheme();

            var left = SplashFloor - since.Elapsed;

            if (left > TimeSpan.Zero)
            {
                await Task.Delay(left, CancellationToken.None);
            }

            if (dismissed.IsCancellationRequested)
            {
                return;
            }

            Open(desktop);

            // After the swap, so the splash closing is a replacement rather than the last window.
            splash.Close();
        }
        catch (Exception exception)
        {
            GiveUp(desktop, splash, exception);
        }
    }

    /// <summary>
    /// Ends a launch that threw on the way to the window. Leaving the splash up would hold
    /// the single launcher lock for ever, and every later launch would then be turned away.
    /// </summary>
    private void GiveUp(
        IClassicDesktopStyleApplicationLifetime desktop, SplashWindow splash, Exception exception)
    {
        var log = _services?.GetService<StartupLog>();

        log?.Say("the launcher could not open", exception);

        try
        {
            splash.Close();
        }
        catch (Exception closing)
        {
            log?.Say("the splash could not close either", closing);
        }

        // Explicit, since ShutdownMode is still OnExplicitShutdown at this point.
        desktop.Shutdown(1);
    }

    /// <summary>
    /// Replaces a settings or state file that will not parse, so every reader of it takes
    /// a default instead. The broken file is kept beside where it was.
    /// </summary>
    private void Repair()
    {
        if (_services is not { } services)
        {
            return;
        }

        try
        {
            var paths = services.GetRequiredService<ApplicationPaths>();
            var repair = services.GetRequiredService<ISettingsRepair>();
            var log = services.GetRequiredService<StartupLog>();

            string[] files =
            [
                paths.SettingsFileFor(SettingsScope.Global),
                paths.StateFileFor(SettingsScope.Global),
            ];

            foreach (var file in files)
            {
                if (repair.Replace(file) is { } broken)
                {
                    log.Say($"{file} would not parse and was moved to {broken}");
                }
            }
        }
        catch (Exception exception)
        {
            // The start carries on either way. A file this could not deal with reads as
            // empty now rather than throwing at whoever asks it for a setting.
            services.GetService<StartupLog>()?.Say("the settings files could not be checked", exception);
        }
    }

    /// <summary>
    /// The splash the launcher starts behind. A static factory so a test builds the same
    /// window this does, mark and all.
    /// </summary>
    public static SplashWindow BuildSplash(string version) =>
        new()
        {
            Mark = new Bitmap(AssetLoader.Open(new Uri(MarkAsset))),

            // The Kitbash mark is a finished badge already and wants no tile behind it.
            ShowMarkFrame = false,
            AppName = "Kitbash",
            AppVersion = version,
            Description = "Godot, without the version wrangling.",
            FadeIn = SplashFade,
        };

    /// <summary>The launcher's own mark, the same file its title bar wears.</summary>
    private const string MarkAsset = "avares://Kitbash/Assets/Icons/icon_64x64.png";

    /// <summary>
    /// Asks the feed and fetches what it offers, reporting into the splash throughout. The
    /// splash is the only thing on screen while this runs.
    /// </summary>
    private async Task UpdateAsync(SplashWindow splash, CancellationToken dismissed)
    {
        if (_services?.GetService<IApplicationUpdates>() is not { } updates)
        {
            return;
        }

        using var limit = new CancellationTokenSource(CheckLimit);

        // Null until the row goes up, and running from the moment it does.
        Stopwatch? shown = null;

        var found = await SayIfSlow(
            updates.CheckAsync(limit.Token),
            CheckPatience,
            () =>
            {
                shown = Stopwatch.StartNew();
                splash.Report("Checking for an update");
            });

        if (dismissed.IsCancellationRequested)
        {
            return;
        }

        if (found is null)
        {
            // The row may have only just gone up, so it is held rather than collapsing
            // straight back. StartAsync takes it away as soon as this returns.
            if (shown is { } since)
            {
                var left = CheckDwell - since.Elapsed;

                if (left > TimeSpan.Zero)
                {
                    await Task.Delay(left, CancellationToken.None);
                }
            }

            return;
        }

        await FetchAsync(updates, found, splash, dismissed);
    }

    /// <summary>
    /// Awaits work, calling say once if it is still running after patience. Work that
    /// answers sooner says nothing at all.
    /// </summary>
    public static async Task<T> SayIfSlow<T>(Task<T> work, TimeSpan patience, Action say)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(say);

        await Task.WhenAny(work, Task.Delay(patience, CancellationToken.None));

        if (!work.IsCompleted)
        {
            say();
        }

        return await work;
    }

    /// <summary>
    /// Downloads, then applies and restarts. Anything that goes wrong leaves the machine as
    /// it was and returns, so the launcher opens on the copy already installed.
    /// </summary>
    private static async Task FetchAsync(
        IApplicationUpdates updates,
        AvailableUpdate found,
        SplashWindow splash,
        CancellationToken dismissed)
    {
        var stages = new UpdateStages(found);

        Report(splash, stages.Downloading(0));

        // Progress captures this thread's context, so every report arrives back on the UI
        // thread and Report touches the window directly.
        var progress = new Progress<int>(percent => Report(splash, stages.Downloading(percent)));

        try
        {
            await updates.DownloadAsync(found, progress, dismissed);
        }
        catch (OperationCanceledException)
        {
            // The close mark did this and the app is already going.
            return;
        }
        catch (Exception)
        {
            // The service has already said what went wrong. Nothing has changed, so the
            // launcher carries on as it was.
            return;
        }

        // Velopack's last report is not reliably a hundred, so a full bar is stated rather
        // than waited for, then held long enough to be seen.
        Report(splash, stages.Downloading(100));

        await Task.Delay(SplashSettle, CancellationToken.None);

        // The swap reports nothing, so the bar goes back to saying only that work is
        // happening.
        Report(splash, stages.Restarting());

        try
        {
            // Does not return when it works. The process is replaced by the new copy.
            updates.ApplyAndRestart(found);
        }
        catch (Exception)
        {
            // Said by the service too.
        }
    }

    /// <summary>The library knows nothing of an update, so a stage is unpacked here.</summary>
    private static void Report(SplashWindow splash, UpdateStage stage) =>
        splash.Report(stage.Status, stage.Fraction, stage.Detail);

    /// <summary>
    /// The look, added once the splash is on screen and only on the path that opens the
    /// launcher. An update that applies never gets here.
    /// </summary>
    private void LoadTheme()
    {
        if (Styles.Count > 0)
        {
            return;
        }

        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Kitbash.Ui/Themes/"))
        {
            Source = new Uri("avares://Kitbash.Ui/Themes/KitbashTheme.axaml"),
        });
    }

    /// <summary>Opens the launcher on whatever version this copy ended up being.</summary>
    private void Open(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_services is not { } services)
        {
            return;
        }

        // Read once here. The window does not watch the setting, so a change to it
        // takes effect the next time Kitbash starts.
        var windows = services.GetRequiredService<IWindowSettings>();

        var window = new LauncherWindow
        {
            Chrome = windows.Chrome,

            // Every window Kitbash frames itself takes this, and one that opens over the
            // launcher takes it from there rather than being handed it again.
            Shadow = services.GetRequiredService<IWindowShadow>(),

            // A window is built without a container, so the services it opens for
            // itself are handed over here rather than resolved inside it.
            Settings = services.GetRequiredService<ISettingsWindows>(),
            OpenIn = services.GetRequiredService<OpenInMenu>(),
            Repositories = services.GetRequiredService<IToolRepositoryList>(),
            Version = services.GetRequiredService<IApplicationVersion>(),
            DataContext = services.GetRequiredService<LauncherViewModel>(),
        };

        desktop.MainWindow = window;

        // Back to the ordinary rule now there is a window to close.
        desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

        window.Show();

        // The launcher is up, so anything a link asked for on the way in can happen now.
        Follow(window);

        // Writes the menu entry and the url scheme this desktop needs. Touches a disk and
        // matters to nothing else that happens here, so it goes off the UI thread and is
        // not waited on.
        var integration = services.GetRequiredService<IDesktopIntegration>();
        _ = Task.Run(integration.Install);
    }

    // The composition root. Everything the launcher needs is registered here and
    // nowhere else.
    private static ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddKitbashPlatform()
            .AddKitbashWorkspaceCreation()
            .AddKitbashGit()
            .AddKitbashEngines()
            .AddKitbashWorkspaceOpeners()
            .AddKitbashSettingsSchema()
            .AddKitbashKnownWorkspaceSettings()
            .AddKitbashDispatcher()
            .AddKitbashToasts()
            .AddKitbashSettingsWindow()
            .AddSingleton<StartupLog>()
            .AddSingleton<IApplicationVersion, ApplicationVersion>()
            .AddSingleton<UpdateSettingsSchema>()
            .AddSingleton<UpdateLog>()
            .AddSingleton<IApplicationUpdates, VelopackUpdates>()
            .AddSingleton<ToolActionsEditor>()
            .AddSingleton<IAfterLaunchOverrides, AfterLaunchOverrides>()
            .AddSingleton<AfterLaunchSettingsSchema>()
            .AddSingleton<IAfterLaunchSettings, AfterLaunchSettings>()
            .AddSingleton<LauncherSettingsSchema>()
            .AddSingleton(provider => provider.GetRequiredService<LauncherSettingsSchema>().Schema)
            .AddSingleton<IApplicationShutdown, AvaloniaApplicationShutdown>()
            .AddSingleton<IAfterLaunchActions, AvaloniaAfterLaunchActions>()
            .AddSingleton<WorkspaceLog>()
            .AddSingleton<WorkspaceLinkIcons>()
            .AddSingleton<IWorkspaceLinks, WorkspaceLinks>()
            .AddSingleton<WorkspaceLinksSettingsSchema>()
            .AddSingleton<ToolLog>()
            .AddSingleton<IToolManifestReader, ToolManifestReader>()
            .AddSingleton<IToolRuntime, ToolRuntime>()
            .AddSingleton<IToolFolderReader, ToolFolderReader>()
            .AddSingleton<IInstalledTools, InstalledTools>()
            .AddSingleton<IProvidedTools, ProvidedTools>()
            .AddSingleton<IToolStarter, ToolStarter>()
            .AddSingleton<ToolProgressReader>()
            .AddSingleton<IToolScriptRunner, ToolScriptRunner>()
            .AddSingleton<IToolInputMemory, ToolInputMemory>()
            .AddSingleton<IToolRepositoryList, ToolRepositoryList>()
            .AddSingleton<IToolRepositoryFactory, ToolRepositoryFactory>()
            .AddSingleton<IToolCatalogue, ToolCatalogue>()
            .AddSingleton<IToolIcons, ToolIcons>()
            .AddSingleton<ToolIconImages>()
            .AddSingleton<IToolInstaller, ToolInstaller>()
            .AddSingleton<IToolFolderInstaller, ToolFolderInstaller>()
            .AddSingleton<ToolRepositoriesEditor>()
            .AddSingleton<ToolRepositoriesSettingsSchema>()
            .AddSingleton<CustomToolsEditor>()
            .AddSingleton<HiddenToolsEditor>()
            .AddSingleton<CustomToolsSettingsSchema>()
            .AddSingleton<ExternalToolIcons>()
            .AddSingleton<OpenInMenu>()
            .AddSingleton<OpenInFactory>()
            .AddSingleton(provider => new EnginesViewModel(
                provider.GetRequiredService<IEngineCatalogue>(),
                provider.GetRequiredService<IEngineStore>(),
                provider.GetRequiredService<IGodotSettings>(),
                provider.GetRequiredService<IPathShortener>(),
                provider.GetRequiredService<IPlatformServices>(),
                provider.GetRequiredService<IFileSystem>(),
                provider.GetRequiredService<IEngineInstaller>(),
                provider.GetRequiredService<IEngineFiles>(),
                provider.GetRequiredService<IToastService>(),
                provider.GetRequiredService<IAfterLaunchSettings>(),
                provider.GetRequiredService<IAfterLaunchActions>()))
            .AddSingleton(provider => new LauncherViewModel(
                provider.GetRequiredService<IWorkspaceRegistry>(),
                provider.GetRequiredService<IWorkspacesSettings>(),
                provider.GetRequiredService<IWorkspaceLinks>(),
                provider.GetRequiredService<IPathShortener>(),
                provider.GetRequiredService<IInstalledTools>(),
                provider.GetRequiredService<IProvidedTools>(),
                provider.GetRequiredService<IToolStarter>(),
                provider.GetRequiredService<IToolScriptRunner>(),
                provider.GetRequiredService<IToolInputMemory>(),
                provider.GetRequiredService<IToolCatalogue>(),
                provider.GetRequiredService<IToolInstaller>(),
                provider.GetRequiredService<IToolFolderInstaller>(),
                provider.GetRequiredService<ToolIconImages>(),
                provider.GetRequiredService<ToolLog>(),
                provider.GetRequiredService<IFileSystem>(),
                provider.GetRequiredService<IGitStatusMonitor>(),
                provider.GetRequiredService<IGitUpdater>(),
                provider.GetRequiredService<IGitCloner>(),
                provider.GetRequiredService<IUiDispatcher>(),
                provider.GetRequiredService<IEngineRequirementReader>(),
                provider.GetRequiredService<IEngineStore>(),
                provider.GetRequiredService<IEngineCatalogue>(),
                provider.GetRequiredService<IEngineResolver>(),
                provider.GetRequiredService<IGodotSettings>(),
                provider.GetRequiredService<IGodotLauncher>(),
                provider.GetRequiredService<IGodotProjectReader>(),
                provider.GetRequiredService<IWorkspaceMaker>(),
                provider.GetRequiredService<IToastService>(),
                provider.GetRequiredService<IPlatformServices>(),
                provider.GetRequiredService<IAfterLaunchSettings>(),
                provider.GetRequiredService<IAfterLaunchActions>(),
                provider.GetRequiredService<EnginesViewModel>(),
                provider.GetRequiredService<OpenInFactory>()))
            .BuildServiceProvider();
}