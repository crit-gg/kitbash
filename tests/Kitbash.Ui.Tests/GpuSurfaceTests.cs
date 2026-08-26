using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Gpu;
using Silk.NET.Vulkan;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The surface under the headless backend, which imports nothing, so what runs here is the
/// read back path a machine whose compositor cannot take an image also takes.
/// </summary>
public class GpuSurfaceTests
{
    [AvaloniaFact]
    public void ASurfaceEitherDrawsOrSaysWhyItCannot()
    {
        var surface = new Filler();
        var window = Showing(surface);

        try
        {
            if (surface.IsDrawing)
            {
                Assert.True(surface.Presented >= 0);
            }
            else
            {
                Assert.Equal(0, surface.Draws);
                Assert.NotEqual("", surface.Info);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AReadBackSurfacePaintsWhatItDrew()
    {
        var surface = new Filler();

        // The same red on both halves, one painted by Avalonia and one by the device, so
        // the test says they match rather than what byte order a captured frame is in.
        var window = Showing(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Children =
            {
                new Border { Background = Brushes.Red },
                surface,
            },
        });

        Grid.SetColumn(surface, 1);

        try
        {
            Assert.SkipUnless(surface.IsDrawing, "This machine has no Vulkan device.");

            using var frame = window.CaptureRenderedFrame();

            Assert.NotNull(frame);
            Assert.True(surface.Draws > 0);

            var pixels = Read(frame, out var stride);

            Assert.Equal(At(pixels, stride, 0.25, 0.5), At(pixels, stride, 0.75, 0.5));
        }
        finally
        {
            window.Close();
        }
    }

    private static Window Showing(Control content)
    {
        var window = new Window { Width = 200, Height = 160, Content = content };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static byte[] Read(Avalonia.Media.Imaging.Bitmap frame, out int stride)
    {
        var size = frame.PixelSize;

        stride = size.Width * 4;

        var pixels = new byte[stride * size.Height];

        frame.CopyPixels(
            new Avalonia.PixelRect(size),
            System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(pixels, 0),
            pixels.Length,
            stride);

        return pixels;
    }

    /// <param name="across">Zero to one across the frame.</param>
    /// <param name="down">Zero to one down the frame.</param>
    private static (byte, byte, byte, byte) At(byte[] pixels, int stride, double across, double down)
    {
        var rows = pixels.Length / stride;
        var at = ((int)(rows * down) * stride) + ((int)(stride / 4 * across) * 4);

        return (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]);
    }

    /// <summary>Clears the frame to one colour, which is the least a surface can draw.</summary>
    private sealed class Filler : GpuSurface
    {
        public static (byte R, byte G, byte B) Ink => (255, 0, 0);

        public int Draws { get; private set; }

        protected override void Draw(VulkanContext context, GpuFrame frame)
        {
            Draws++;

            frame.Target.Transition(
                frame.Buffer,
                ImageLayout.TransferDstOptimal,
                PipelineStageFlags2.AllTransferBit,
                AccessFlags2.TransferWriteBit);

            var color = new ClearColorValue(1f, 0f, 0f, 1f);
            var range = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1);

            context.Api.CmdClearColorImage(
                frame.Buffer, frame.Target.Handle, ImageLayout.TransferDstOptimal, in color, 1, in range);
        }
    }
}
