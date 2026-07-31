using Avalonia;

namespace Workbench;

internal static class Program
{
    // Avalonia needs to be initialized before any UI type is referenced, so keep
    // this method free of anything that could pull one in early.
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
