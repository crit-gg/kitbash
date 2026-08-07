using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The window an app opens with while it starts. What matters is that it stands up with no
/// theme loaded at all, since that is the only reason it is built the way it is.
/// </summary>
public class SplashWindowTests
{
    private static T Part<T>(SplashWindow splash, string name)
        where T : StyledElement =>
        splash.GetVisualDescendants().OfType<T>().First(part => part.Name == name);

    /// <summary>
    /// The width the fill was asked for. Its live width is mid transition for as long as the
    /// transition runs, and the headless clock advances on real time, so reading the animated
    /// value would be reading a stopwatch.
    /// </summary>
    private static double Target(Border sweep) =>
        sweep.GetBaseValue(Layoutable.WidthProperty).GetValueOrDefault(double.NaN);

    private static SplashWindow Open()
    {
        var splash = new SplashWindow
        {
            AppName = "Workbench",
            AppVersion = "1.4.2",
            Description = "Game data tooling for Godot projects",
        };

        splash.Show();

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return splash;
    }

    [AvaloniaFact]
    public void ItDrawsWithNoThemeLoadedAtAll()
    {
        var app = Application.Current!;
        var kept = app.Styles.ToArray();

        app.Styles.Clear();

        try
        {
            var splash = Open();

            Assert.Empty(app.Styles);
            Assert.NotNull(splash.CaptureRenderedFrame());
            Assert.Equal("WORKBENCH", Part<TextBlock>(splash, "AppName").Text);
        }
        finally
        {
            app.Styles.AddRange(kept);
        }
    }

    [AvaloniaFact]
    public void TheLookCanArriveAfterTheSplashIsAlreadyUp()
    {
        var app = Application.Current!;
        var kept = app.Styles.ToArray();

        app.Styles.Clear();

        try
        {
            var splash = Open();

            Assert.NotNull(splash.CaptureRenderedFrame());

            // The order an app starts in: the splash first, then the look it did not wait for.
            app.Styles.AddRange(kept);

            var later = new ChromelessWindow { Width = 300, Height = 200 };

            later.Show();

            Dispatcher.UIThread.RunJobs();
            later.UpdateLayout();

            // A window built after the styles arrived still wears the frame they carry, and
            // the splash still draws its own.
            Assert.Contains(
                later.GetVisualDescendants().OfType<Border>(),
                part => part.Name == "PART_ContentRoot");

            Assert.NotNull(splash.CaptureRenderedFrame());

            later.Close();
            splash.Close();
        }
        finally
        {
            if (app.Styles.Count == 0)
            {
                app.Styles.AddRange(kept);
            }
        }
    }

    [AvaloniaFact]
    public void TheDesktopNeverDrawsThisFrame()
    {
        var splash = Open();

        // Both routes a window takes the desktop's frame by, the setting the launcher reads
        // onto ChromelessWindow and the class that follows it.
        splash.WindowDecorations = WindowDecorations.Full;
        splash.Classes.Add("nativeChrome");

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        Assert.Equal(WindowDecorations.None, splash.WindowDecorations);
        Assert.Equal(600, Part<Border>(splash, "PART_ContentRoot").Bounds.Width);
        Assert.NotNull(splash.CaptureRenderedFrame());
    }

    [AvaloniaFact]
    public void ProgressIsHiddenUntilTheHostAsksForIt()
    {
        var splash = Open();

        Assert.False(splash.IsProgressVisible);
        Assert.False(Part<StackPanel>(splash, "Progress").IsVisible);

        splash.Report("Loading workspace index", "3 of 5");

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        Assert.True(Part<StackPanel>(splash, "Progress").IsVisible);
        Assert.Equal("Loading workspace index", Part<TextBlock>(splash, "Status").Text);
        Assert.Equal("3 of 5", Part<TextBlock>(splash, "Step").Text);
    }

    [AvaloniaFact]
    public void ProgressCanBeTakenBackDownAgain()
    {
        var splash = Open();

        var card = Part<Border>(splash, "PART_ContentRoot");
        var quiet = card.Bounds.Height;

        splash.Report("Checking for an update");

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        Assert.True(card.Bounds.Height > quiet);

        splash.IsProgressVisible = false;

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        Assert.False(Part<StackPanel>(splash, "Progress").IsVisible);
        Assert.Equal(quiet, card.Bounds.Height);
    }

    [AvaloniaFact]
    public void TheBarIsIndeterminateUntilItIsGivenAFraction()
    {
        var splash = Open();

        splash.Report("Loading workspace index");

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        var track = Part<Border>(splash, "Track");
        var sweep = Part<Border>(splash, "Sweep");
        var inner = track.Bounds.Width - 2;

        Assert.Null(splash.Progress);
        Assert.Equal(inner * 0.3, Target(sweep), 3);

        splash.Report("Reading the registry", 0.5);

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        Assert.Equal(inner * 0.5, Target(sweep), 3);
        Assert.Null(sweep.RenderTransform);
    }

    [AvaloniaFact]
    public void TheFirstReportOfAllSweeps()
    {
        var splash = Open();

        // The first report is the case that broke: the sweep sized its journey from its own
        // width, which the fill transition had only just started moving off zero.
        splash.Report("Checking for an update");

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        AvaloniaHeadlessPlatform.ForceRenderTimerTick(5);

        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(Part<Border>(splash, "Sweep").RenderTransform);
    }

    [AvaloniaFact]
    public void AFilledBarStillMovesSoItNeverReadsAsFrozen()
    {
        var splash = Open();

        splash.Report("Installing Godot 4.7.1", 0.6);

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        AvaloniaHeadlessPlatform.ForceRenderTimerTick(5);

        Dispatcher.UIThread.RunJobs();

        var shimmer = Part<Border>(splash, "Shimmer");

        Assert.True(shimmer.IsVisible);
        Assert.NotNull(shimmer.RenderTransform);
        Assert.Null(Part<Border>(splash, "Sweep").RenderTransform);

        splash.Report("Reading the registry");

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        Assert.False(shimmer.IsVisible);
        Assert.Null(shimmer.RenderTransform);
    }

    [AvaloniaFact]
    public void AFractionOutsideItsRangeIsPulledBackIntoIt()
    {
        var splash = Open();

        splash.Report("Nearly there", 4.2);

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        var track = Part<Border>(splash, "Track");
        var sweep = Part<Border>(splash, "Sweep");

        Assert.Equal(track.Bounds.Width - 2, Target(sweep), 3);

        splash.Progress = -1;

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, Target(sweep), 3);
    }

    [AvaloniaFact]
    public void ItSitsInTheOrdinaryWindowOrder()
    {
        var splash = Open();

        Assert.False(splash.Topmost);
    }

    [AvaloniaFact]
    public void PressingTheCardMovesTheWindowRatherThanClosingIt()
    {
        var splash = Open();

        var closed = false;
        splash.Closed += (_, _) => closed = true;

        splash.MouseDown(new Point(300, 60), MouseButton.Left);

        Dispatcher.UIThread.RunJobs();

        Assert.False(closed);
    }

    [AvaloniaFact]
    public void TheCardIsSixHundredWideAndOnlyProgressMakesItTaller()
    {
        var splash = Open();

        var card = Part<Border>(splash, "PART_ContentRoot");

        Assert.Equal(600, card.Bounds.Width);

        var quiet = card.Bounds.Height;

        splash.Report("Reading the registry");

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        Assert.True(card.Bounds.Height > quiet);
        Assert.Equal(600, card.Bounds.Width);
    }

    [AvaloniaFact]
    public void BlankValuesCollapseTheirOwnSlots()
    {
        var splash = Open();

        Assert.Null(Part<Image>(splash, "MarkImage").Source);
        Assert.False(Part<Border>(splash, "Mark").IsVisible);
        Assert.True(Part<Border>(splash, "Version").IsVisible);
        Assert.True(Part<TextBlock>(splash, "Description").IsVisible);

        splash.AppVersion = string.Empty;
        splash.Description = string.Empty;
        splash.Report("Reading the registry");

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        Assert.False(Part<Border>(splash, "Version").IsVisible);
        Assert.False(Part<TextBlock>(splash, "Description").IsVisible);
        Assert.False(Part<TextBlock>(splash, "Step").IsVisible);
    }

    [AvaloniaFact]
    public void TheMarkFillsTheTileWhenItIsAskedToStandAlone()
    {
        var splash = Open();

        splash.Mark = new DrawingImage { Drawing = new GeometryDrawing() };
        splash.ShowMarkFrame = false;

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        var mark = Part<Border>(splash, "Mark");
        var image = Part<Image>(splash, "MarkImage");

        Assert.True(mark.IsVisible);
        Assert.Null(mark.Background);
        Assert.Equal(48, image.Width);
    }

    [AvaloniaFact]
    public void TheCloseMarkAnswersOnRelease()
    {
        var splash = Open();

        var closed = false;
        splash.Closed += (_, _) => closed = true;

        var over = Part<Border>(splash, "Close").TranslatePoint(new Point(12, 12), splash)!.Value;

        splash.MouseDown(over, MouseButton.Left);

        Dispatcher.UIThread.RunJobs();

        Assert.False(closed);

        splash.MouseUp(over, MouseButton.Left);

        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    }

    [AvaloniaFact]
    public void ADismissedSplashAsksForNothingOfTheHostWhenThereIsNoLifetime()
    {
        var splash = Open();

        var over = Part<Border>(splash, "Close").TranslatePoint(new Point(12, 12), splash)!.Value;

        // Ending the launch reaches for the application lifetime, and a host that has none
        // has to be survivable rather than a crash on the way out.
        Assert.Null(Application.Current!.ApplicationLifetime);

        splash.MouseDown(over, MouseButton.Left);
        splash.MouseUp(over, MouseButton.Left);

        Dispatcher.UIThread.RunJobs();

        Assert.False(splash.IsVisible);
    }

    [AvaloniaFact]
    public void APressThatWandersOffTheCloseMarkIsAPressThatChangedItsMind()
    {
        var splash = Open();

        var closed = false;
        splash.Closed += (_, _) => closed = true;

        var close = Part<Border>(splash, "Close");
        var over = close.TranslatePoint(new Point(12, 12), splash)!.Value;
        var away = close.TranslatePoint(new Point(-90, 40), splash)!.Value;

        splash.MouseDown(over, MouseButton.Left);
        splash.MouseMove(away);
        splash.MouseUp(away, MouseButton.Left);

        Dispatcher.UIThread.RunJobs();

        Assert.False(closed);
    }

    [AvaloniaFact]
    public void TheCloseMarkGoesWhenAnAppSaysSo()
    {
        var splash = Open();

        splash.ShowClose = false;

        Dispatcher.UIThread.RunJobs();

        Assert.False(Part<Border>(splash, "Close").IsVisible);
    }
}
