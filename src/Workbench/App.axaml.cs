using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Workbench.Core;
using Workbench.Core.Git;
using Workbench.Core.Godot;
using Workbench.Core.IO;
using Workbench.Core.Platform;
using Workbench.Core.Settings;
using Workbench.Core.Workspaces;
using Workbench.Mock;
using Workbench.ViewModels;
using Workbench.Ui;
using Workbench.Ui.Toasts;
using Workbench.Views;

namespace Workbench;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = BuildServices();

            // Read once here. The window does not watch the setting, so a change to it
            // takes effect the next time Workbench starts.
            var windows = _services.GetRequiredService<IWindowSettings>();

            desktop.MainWindow = new LauncherWindow
            {
                UsesNativeChrome = windows.UseNativeChrome,
                DataContext = _services.GetRequiredService<LauncherViewModel>(),
            };

            desktop.ShutdownRequested += (_, _) => _services?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // The composition root. Everything the launcher needs is registered here and
    // nowhere else.
    private static ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddWorkbenchPlatform()
            .AddWorkbenchWorkspaces()
            .AddWorkbenchGit()
            .AddWorkbenchEngines()
            .AddWorkbenchToasts()
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
