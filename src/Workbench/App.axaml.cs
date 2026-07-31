using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Workbench.Core;
using Workbench.ViewModels;
using Workbench.Views;

namespace Workbench;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new LauncherWindow
            {
                DataContext = new LauncherViewModel(BuildRegistry()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

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
            ToolActivationResult.Failure($"'{name}' has no implementation yet."));
}
