using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using Kitbash.Core;
using Kitbash.Core.Platform;
using Kitbash.Updates;

namespace Kitbash;

internal sealed class Program
{
    // Avalonia must initialize before any UI type is referenced. Keep this method
    // free of anything that would load one early.
    /// <summary>Velopack reruns this binary with one of these to do install time work.</summary>
    private const string HookPrefix = "--veloapp-";

    [STAThread]
    public static int Main(string[] args)
    {
        var log = new UpdateLog();

        try
        {
            // Velopack reruns this binary with hook arguments during install, update and
            // uninstall, and Run exits from inside those, so nothing may come before it.
            // Applying on startup finishes an update the app was killed part way through,
            // while there is still no window to interrupt.
            var velopack = VelopackApp.Build()
                .SetAutoApplyOnStartup(true)
                .SetLogger(log);

            // Velopack declares the uninstall hooks for Windows alone, since an AppImage
            // and a pkg have no uninstaller to run one, and CA1416 refuses the call without
            // this. It is the only place the launcher asks which OS it is on.
            if (OperatingSystem.IsWindows())
            {
                velopack.OnBeforeUninstallFastCallback(_ => Forget(log));
            }

            velopack.Run();
        }
        catch (Exception exception)
        {
            log.Say("velopack could not start", exception);

            // A hook that failed leaves an install part way through, and opening a window
            // in the middle of one would be worse than stopping. Any other launch carries
            // on, so a broken update never costs somebody the copy they already had.
            if (args.Any(argument => argument.StartsWith(HookPrefix, StringComparison.Ordinal)))
            {
                return 1;
            }
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Takes the url scheme back before the uninstaller deletes the program it names.
    /// Builds its own container, since this runs inside Velopack and the app is not up.
    /// </summary>
    private static void Forget(UpdateLog log)
    {
        try
        {
            using var services = new ServiceCollection().AddKitbashPlatform().BuildServiceProvider();

            services.GetRequiredService<IDesktopIntegration>().Remove();
        }
        catch (Exception exception)
        {
            // An uninstall carries on either way. What is left behind names a program that
            // has gone, which the desktop reports for itself.
            log.Say("the url scheme could not be taken back", exception);
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
