using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Kitbash.Updates;

namespace Kitbash.Tests;

/// <summary>
/// The window the launcher starts behind. Built through the same factory the launcher uses,
/// so a mark that cannot be loaded fails here rather than on a real launch.
/// </summary>
public class LauncherSplashTests
{
    [AvaloniaFact]
    public void ItWearsTheLaunchersOwnMarkAndVersion()
    {
        var splash = App.BuildSplash("0.6.1");

        Assert.NotNull(splash.Mark);
        Assert.Equal("Kitbash", splash.AppName);
        Assert.Equal("0.6.1", splash.AppVersion);
        Assert.False(splash.ShowMarkFrame);
        Assert.Equal(TimeSpan.FromSeconds(1), splash.FadeIn);

        splash.Show();

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(splash.CaptureRenderedFrame());

        splash.Close();
    }

    [AvaloniaFact]
    public void ItReportsAnUpdateTheWayTheLauncherDoes()
    {
        var splash = App.BuildSplash("0.6.0");
        var stages = new UpdateStages(new AvailableUpdate("0.6.1", 12_900_000));

        splash.Show();

        var stage = stages.Downloading(45);

        splash.Report(stage.Status, stage.Fraction, stage.Detail);

        Dispatcher.UIThread.RunJobs();
        splash.UpdateLayout();

        Assert.True(splash.IsProgressVisible);
        Assert.Equal("Downloading Kitbash 0.6.1", splash.Status);
        Assert.Equal("45% of 12.3 MB", splash.Step);
        Assert.Equal(0.45, splash.Progress);

        splash.Close();
    }
}
