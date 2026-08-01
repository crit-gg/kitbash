using Avalonia;

namespace Workbench;

internal sealed class Program
{
    // Avalonia must initialize before any UI type is referenced. Keep this method
    // free of anything that would load one early.
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
