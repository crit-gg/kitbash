using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Rendering.Composition;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Kitbash.Gallery.Views;
using Kitbash.Core;
using Kitbash.Ui;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery;

public partial class App : Application
{
    /// <summary>When Initialize finished, for the startup probe.</summary>
    internal long Initialized { get; private set; }

    /// <summary>When Initialize was entered, so the platform's own cost stands alone.</summary>
    internal long Entered { get; private set; }

    public override void Initialize()
    {
        Entered = DateTime.UtcNow.Ticks;

        AvaloniaXamlLoader.Load(this);

        Initialized = DateTime.UtcNow.Ticks;
    }

    /// <summary>
    /// Opens behind a splash, the way a real app would. The splash is the main window until
    /// the gallery is built, since the lifetime ends the app when the last window closes and
    /// swapping first is what lets the splash be closed afterwards.
    /// </summary>
    public override async void OnFrameworkInitializationCompleted()
    {
        // Called first rather than last, so a startup that gives up part way through has
        // still run it.
        base.OnFrameworkInitializationCompleted();

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var ready = DateTime.UtcNow.Ticks;

        var splash = new SplashWindow
        {
            Mark = GalleryWindow.SplashMark(),
            AppName = "Kitbash Gallery",
            AppVersion = GalleryWindow.BuildVersion(),
            Description = "Every control in the library, live",
        };

        using var abandoned = new CancellationTokenSource();

        // Closing the splash before the gallery exists is a request to give up, and the
        // window it would have replaced is never built.
        splash.Closed += (_, _) => abandoned.Cancel();

        var built = DateTime.UtcNow.Ticks;

        desktop.MainWindow = splash;

        var asked = DateTime.UtcNow.Ticks;

        splash.Show();

        var shown = DateTime.UtcNow.Ticks;

        if (Environment.GetEnvironmentVariable(ProbeVariable) is { Length: > 0 } probe)
        {
            await Report(probe, ready, built, asked, shown, splash);

            desktop.Shutdown();

            return;
        }

        GalleryWindow gallery;

        try
        {
            gallery = await Open(splash, abandoned.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        desktop.MainWindow = gallery;
        gallery.Show();

        splash.Close();
    }

    /// <summary>
    /// Set to the wall clock in microseconds taken just before this process was created, and
    /// the app then prints what the splash cost to reach the screen and exits.
    /// </summary>
    private const string ProbeVariable = "KITBASH_STARTUP_PROBE";

    /// <summary>
    /// Prints the startup breakdown. Visible means the compositor has rendered the batch the
    /// splash is in, which is the last thing this process can observe before the pixels are
    /// the desktop's problem.
    /// </summary>
    private async Task Report(
        string probe, long ready, long built, long asked, long shown, SplashWindow splash)
    {
        var visual = ElementComposition.GetElementVisual(splash);

        if (visual is not null)
        {
            // Bounded, so a compositor that never answers still lets the process go.
            await Task.WhenAny(
                visual.Compositor.RequestCompositionBatchCommitAsync().Rendered,
                Task.Delay(TimeSpan.FromSeconds(10)));
        }

        var visible = DateTime.UtcNow.Ticks;

        if (!long.TryParse(probe, out var micros))
        {
            return;
        }

        var created = DateTime.UnixEpoch.Ticks + (micros * 10);

        Console.WriteLine($"{"stage",-26}{"ms",9}{"total",10}");

        Say("process start to Main", created, Program.Entered, created);
        Say("AppBuilder configured", Program.Entered, Program.Configured, created);
        Say("platform up", Program.Configured, Entered, created);
        Say("App.Initialize", Entered, Initialized, created);
        Say("lifetime ready", Initialized, ready, created);
        Say("splash built", ready, built, created);
        Say("Show called", built, asked, created);
        Say("Show returned", asked, shown, created);
        Say("first frame rendered", shown, visible, created);
        Say("VISIBLE", visible, visible, created);

    }

    private static void Say(string stage, long from, long to, long created) =>
        Console.WriteLine(
            $"{stage,-26}{(to - from) / 10_000.0,9:0.0}{(to - created) / 10_000.0,10:0.0}");

    /// <summary>
    /// The startup the splash is reporting on. The waits are the demonstration and a real
    /// app would have work in their place.
    /// </summary>
    private async Task<GalleryWindow> Open(SplashWindow splash, CancellationToken abandoned)
    {
        // Quiet to begin with, which is the form a splash takes when nothing has said there
        // is anything to report.
        await Task.Delay(TimeSpan.FromMilliseconds(500), abandoned);

        // Nothing here is countable, so the bar sweeps.
        splash.Report("Loading the Slate theme");

        await Task.Delay(TimeSpan.FromMilliseconds(700), abandoned);

        LoadTheme();

        splash.Report("Building the gallery", 0.34, "1 of 3");

        await Task.Delay(TimeSpan.FromMilliseconds(800), abandoned);

        var services = BuildServices();

        splash.Report("Wiring the harnesses", 0.67, "2 of 3");

        await Task.Delay(TimeSpan.FromMilliseconds(800), abandoned);

        // This one really does take a moment, and it holds the UI thread while it runs, so
        // the splash stops animating until it is done. A real app builds its window off the
        // thread wherever it can.
        var gallery = services.GetRequiredService<GalleryWindow>();

        splash.Report("Opening the gallery", 1, "3 of 3");

        await Task.Delay(TimeSpan.FromMilliseconds(700), abandoned);

        return gallery;
    }

    /// <summary>
    /// The look, added once the splash is on screen. A consumer that opens straight into its
    /// window puts these three lines in App.axaml instead.
    /// </summary>
    private void LoadTheme()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(Include("KitbashTheme"));

        // The second line, taken only by an app that docks.
        Styles.Add(Include("KitbashDocking"));
    }

    private static StyleInclude Include(string name) =>
        new(new Uri("avares://Kitbash.Ui/Themes/"))
        {
            Source = new Uri($"avares://Kitbash.Ui/Themes/{name}.axaml"),
        };

    /// <summary>
    /// The gallery's composition root. Nothing here reaches for a static, which is the
    /// point: the window is handed a toast service the same way a tool would be.
    /// </summary>
    private static ServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddKitbashPlatform()
            .AddKitbashToasts()
            .AddSingleton<GalleryWindow>()
            .BuildServiceProvider();
}
