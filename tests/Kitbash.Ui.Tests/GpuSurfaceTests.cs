using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Gpu;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a surface does with no device. The headless backend has no GPU interop at all, so
/// this is the path a machine whose compositor cannot import an image also takes.
/// </summary>
public class GpuSurfaceTests
{
    [AvaloniaFact]
    public void ASurfaceWithNoInteropSaysSoAndDrawsNothing()
    {
        var surface = new Probe();
        var window = new Window { Width = 320, Height = 240, Content = surface };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.False(surface.IsDrawing);
            Assert.Equal(0, surface.Presented);
            Assert.Equal(0, surface.Draws);
            Assert.NotEqual("", surface.Info);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShuttingDownTwiceIsOneTeardown()
    {
        var surface = new Probe();
        var window = new Window { Width = 320, Height = 240, Content = surface };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var first = surface.ShutdownAsync();

        Assert.Same(first, surface.ShutdownAsync());

        window.Close();
    }

    private sealed class Probe : GpuSurface
    {
        public int Draws { get; private set; }

        protected override void Draw(VulkanContext context, CompositionSwapchain.Frame frame) => Draws++;
    }
}
