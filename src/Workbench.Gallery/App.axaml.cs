using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Workbench.Gallery.Views;
using Workbench.Ui;

namespace Workbench.Gallery;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = BuildServices().GetRequiredService<GalleryWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// The gallery's composition root. Nothing here reaches for a static, which is the
    /// point: the window is handed a toast service the same way a tool would be.
    /// </summary>
    private static ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddWorkbenchToasts()
            .AddSingleton<GalleryWindow>()
            .BuildServiceProvider();
}
