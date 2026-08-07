using Avalonia;

namespace Kitbash.Gallery;

internal sealed class Program
{
    // Avalonia must initialize before any UI type is referenced. Keep this method
    // free of anything that would load one early.
    /// <summary>
    /// When Main was entered, in DateTime ticks. Read by the startup probe to separate the
    /// runtime's own start from anything this app does.
    /// </summary>
    internal static long Entered { get; private set; }

    /// <summary>When the AppBuilder was configured, before the platform is brought up.</summary>
    internal static long Configured { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Entered = DateTime.UtcNow.Ticks;

        var builder = BuildAvaloniaApp();

        Configured = DateTime.UtcNow.Ticks;

        return builder.StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
