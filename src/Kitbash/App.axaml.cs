using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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

namespace Kitbash;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// How long the launcher waits on the feed. The splash is up while it is asked, so this
    /// bounds a wait a person can see rather than a blank screen. A check that does not
    /// answer in time is dropped and tried again at the next launch, which costs one launch
    /// and never costs a start.
    /// </summary>
    private static readonly TimeSpan CheckLimit = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The least time the splash is up before the launcher replaces it. A splash that
    /// flashes past reads as a fault rather than as a start.
    /// </summary>
    private static readonly TimeSpan SplashFloor = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a full bar is held before the restart takes the bar back to indeterminate.
    /// The fill takes 180ms to travel, so moving on from the last report any sooner would
    /// mean a hundred percent was never seen.
    /// </summary>
    private static readonly TimeSpan SplashSettle = TimeSpan.FromMilliseconds(600);

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

            // Before the update and before any window. A copy that is not the first has
            // already asked the first to come forward, so it leaves without drawing.
            if (!Hold())
            {
                desktop.Shutdown();

                return;
            }

            _ = StartAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Takes the single launcher lock, and listens for a later copy asking this one to
    /// come forward. False means this copy is the later one and should go.
    /// </summary>
    private bool Hold()
    {
        if (_services?.GetService<ISingleInstance>() is not { } instance)
        {
            return true;
        }

        instance.AskedToComeForward += (_, _) => ComeForward();

        return instance.TryHold();
    }

    /// <summary>
    /// Brings the launcher to the front for somebody who started a second copy. The event
    /// arrives on a background thread, so the window is touched through the dispatcher.
    /// </summary>
    private void ComeForward() =>
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
        });

    /// <summary>
    /// The splash, then the update, then the launcher. **Every path through this ends with a
    /// window** unless a person dismissed the splash, because an app that fails to update and
    /// shows nothing is worse than one that never tried.
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

        splash.Show();

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
            Description = "Designer tools for Godot projects",
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

        splash.Report("Checking for an update");

        using var limit = new CancellationTokenSource(CheckLimit);

        if (await updates.CheckAsync(limit.Token) is not { } found)
        {
            return;
        }

        if (dismissed.IsCancellationRequested)
        {
            return;
        }

        await FetchAsync(updates, found, splash, dismissed);
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
            UsesNativeChrome = windows.UseNativeChrome,

            // A window is built without a container, so the services it opens for
            // itself are handed over here rather than resolved inside it.
            Settings = services.GetRequiredService<ISettingsWindows>(),
            OpenIn = services.GetRequiredService<OpenInMenu>(),
            Version = services.GetRequiredService<IApplicationVersion>(),
            DataContext = services.GetRequiredService<LauncherViewModel>(),
        };

        desktop.MainWindow = window;

        // Back to the ordinary rule now there is a window to close.
        desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

        window.Show();

        // Writes a menu entry for the AppImage. Touches a disk and matters to nothing
        // else that happens here, so it goes off the UI thread and is not waited on.
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
            .AddSingleton<ToolLog>()
            .AddSingleton<IToolManifestReader, ToolManifestReader>()
            .AddSingleton<IToolRuntime, ToolRuntime>()
            .AddSingleton<IToolFolderReader, ToolFolderReader>()
            .AddSingleton<IInstalledTools, InstalledTools>()
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
            .AddSingleton<CustomToolsSettingsSchema>()
            .AddSingleton<ExternalToolIcons>()
            .AddSingleton<OpenInMenu>()
            .AddSingleton<OpenInViewModel>()
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
                provider.GetRequiredService<IPathShortener>(),
                provider.GetRequiredService<IInstalledTools>(),
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
                provider.GetRequiredService<OpenInViewModel>()))
            .BuildServiceProvider();
}
