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
                provider.GetRequiredService<IPathShortener>(),
                provider.GetRequiredService<IToolRegistry>(),
                provider.GetRequiredService<IGitStatusMonitor>(),
                provider.GetRequiredService<IGitUpdater>(),
                provider.GetRequiredService<IUiDispatcher>(),
                provider.GetRequiredService<EnginesViewModel>()))
            .BuildServiceProvider();

    // Placeholders until real tools exist.
    private static IToolRegistry BuildRegistry() =>
        new ToolRegistry()
            .Add(new ToolDescriptor(
                "data-editor",
                "Data Editor",
                "Edit gameplay definition files.",
                "Content",
                NotBuiltYet("Data Editor")))
            .Add(new ToolDescriptor(
                "schema-inspector",
                "Schema Inspector",
                "Browse types and exported properties from a schema manifest.",
                "Content",
                NotBuiltYet("Schema Inspector")))
            .Add(new ToolDescriptor(
                "asset-index",
                "Asset Index",
                "Search project assets and their identifiers.",
                "Project",
                NotBuiltYet("Asset Index")));

    private static IToolActivation NotBuiltYet(string name) =>
        DelegateToolActivation.Sync(() =>
            ToolActivationResult.Failure($"{name} is not built yet."));
}
