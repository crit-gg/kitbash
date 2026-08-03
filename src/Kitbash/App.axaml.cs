using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Kitbash.Core;
using Kitbash.Core.Git;
using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;
using Kitbash.Mock;
using Kitbash.Settings;
using Kitbash.Updates;
using Kitbash.ViewModels;
using Kitbash.Ui;
using Kitbash.Ui.Settings;
using Kitbash.Ui.Toasts;
using Kitbash.Views;

namespace Kitbash;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// How long the launcher stays off screen while the feed is asked. Short, because
    /// nothing is drawn during it. A check that does not answer in time is dropped and
    /// tried again at the next launch, which costs one launch and never costs a start.
    /// </summary>
    private static readonly TimeSpan CheckLimit = TimeSpan.FromSeconds(3);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = BuildServices();

            // Nothing is on screen until the update is settled, and the update dialog
            // closing would otherwise be a last window closing and end the app before the
            // launcher had opened. Put back in Open.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.ShutdownRequested += (_, _) => _services?.Dispose();

            _ = StartAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Updates first, then opens. **Every path through this ends with a window**, because
    /// an app that fails to update and shows nothing is worse than one that never tried.
    /// </summary>
    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            await UpdateAsync();
        }
        catch (Exception exception)
        {
            // Nothing above rethrows on purpose, so reaching here is a fault rather than
            // a failed update. The copy on the machine still has to open either way.
            _services?.GetService<UpdateLog>()?.Say("update could not run", exception);
        }

        Open(desktop);
    }

    /// <summary>
    /// Asks the feed, and hands over to the dialog when it has something. Draws nothing
    /// while asking, since a feed that has gone takes its own time to say so and a window
    /// that flashed on every launch would be worse than the wait.
    /// </summary>
    private async Task UpdateAsync()
    {
        if (_services?.GetService<IApplicationUpdates>() is not { } updates)
        {
            return;
        }

        using var limit = new CancellationTokenSource(CheckLimit);

        if (await updates.CheckAsync(limit.Token) is { } found)
        {
            await UpdateDialog.RunAsync(updates, found);
        }
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
            .AddKitbashWorkspaces()
            .AddKitbashGit()
            .AddKitbashEngines()
            .AddKitbashSettingsSchema()
            .AddKitbashKnownWorkspaceSettings()
            .AddKitbashToasts()
            .AddKitbashSettingsWindow()
            .AddSingleton<IApplicationVersion, ApplicationVersion>()
            .AddSingleton<UpdateSettingsSchema>()
            .AddSingleton<UpdateLog>()
            .AddSingleton<IApplicationUpdates, VelopackUpdates>()
            .AddSingleton<LauncherSettingsSchema>()
            .AddSingleton(provider => provider.GetRequiredService<LauncherSettingsSchema>().Schema)
            .AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>()
            .AddSingleton(BuildRegistry())
            .AddSingleton<MockToolCatalogue>()
            .AddSingleton(provider => new EnginesViewModel(
                provider.GetRequiredService<IEngineCatalogue>(),
                provider.GetRequiredService<IEngineStore>(),
                provider.GetRequiredService<IGodotSettings>(),
                provider.GetRequiredService<IPathShortener>(),
                provider.GetRequiredService<IPlatformServices>(),
                provider.GetRequiredService<IFileSystem>(),
                provider.GetRequiredService<IEngineInstaller>(),
                provider.GetRequiredService<IEngineFiles>(),
                provider.GetRequiredService<IToastService>()))
            .AddSingleton(provider => new LauncherViewModel(
                provider.GetRequiredService<IWorkspaceRegistry>(),
                provider.GetRequiredService<IWorkspacesSettings>(),
                provider.GetRequiredService<IPathShortener>(),
                provider.GetRequiredService<IToolRegistry>(),
                provider.GetRequiredService<MockToolCatalogue>(),
                provider.GetRequiredService<IGitStatusMonitor>(),
                provider.GetRequiredService<IGitUpdater>(),
                provider.GetRequiredService<IGitCloner>(),
                provider.GetRequiredService<IUiDispatcher>(),
                provider.GetRequiredService<IEngineRequirementReader>(),
                provider.GetRequiredService<IEngineStore>(),
                provider.GetRequiredService<IEngineResolver>(),
                provider.GetRequiredService<IGodotSettings>(),
                provider.GetRequiredService<IGodotLauncher>(),
                provider.GetRequiredService<IPlatformServices>(),
                provider.GetRequiredService<EnginesViewModel>()))
            .BuildServiceProvider();

    // Placeholders until real tools exist. The ids are what MockToolCatalogue keys its
    // versions off, so renaming one there and not here leaves a card with no version.
    private static IToolRegistry BuildRegistry() =>
        new ToolRegistry()
            .Add(new ToolDescriptor(
                "foundry",
                "Foundry",
                "Data editor for attributes, stats, effects, machines and recipes, plus the "
                + "graphs designers author: pure, exec, state machines and behaviour trees.",
                "Content",
                NotBuiltYet("Foundry")))
            .Add(new ToolDescriptor(
                "balance-sim",
                "Balance Sim",
                "Runs a factory graph headless over simulated time and reports throughput, "
                + "bottlenecks and drift against the balance targets.",
                "Analysis",
                NotBuiltYet("Balance Sim")))
            .Add(new ToolDescriptor(
                "pipeline",
                "Pipeline",
                "Runs the export and packaging steps for a build, from data validation "
                + "through to a signed archive.",
                "Build",
                NotBuiltYet("Pipeline")))
            .Add(new ToolDescriptor(
                "strings",
                "Strings",
                "Localization tables for every piece of player facing text, with coverage "
                + "per language and a diff against the last shipped build.",
                "Content",
                NotBuiltYet("Strings")))
            .Add(new ToolDescriptor(
                "atlas",
                "Atlas",
                "Sprite and texture atlas packer that writes import presets straight into "
                + "the workspace.",
                "Content",
                NotBuiltYet("Atlas")));

    private static IToolActivation NotBuiltYet(string name) =>
        DelegateToolActivation.Sync(() =>
            ToolActivationResult.Failure($"{name} is not built yet."));
}
