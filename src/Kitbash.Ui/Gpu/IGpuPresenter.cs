using Avalonia;
using Avalonia.Media;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// How a surface's pixels reach the screen. One hands the compositor an exported image, the
/// other reads the frame back and draws it like any other bitmap.
/// </summary>
internal interface IGpuPresenter : IAsyncDisposable
{
    /// <summary>True while the compositor draws the image itself and nothing is read back.</summary>
    bool IsHandedOver { get; }

    /// <summary>False when there is nothing to draw into, which is the frame to skip.</summary>
    bool TryBeginFrame(PixelSize size, out GpuFrame frame);

    /// <summary>Closes the frame with one submission and puts it on screen.</summary>
    void EndFrame(GpuFrame frame);

    /// <summary>Draws whatever the last frame produced. Nothing, when the compositor has it.</summary>
    void Present(DrawingContext context, Rect bounds);
}
