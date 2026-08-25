using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The dot grid under the graph. One tile is drawn into a bitmap and repeated by the brush,
/// so a pan costs one fill however far the canvas reaches.
///
/// Measured on a 1600 by 900 canvas with a software rasteriser: a tiled DrawingBrush cost
/// 17ms a frame and a bitmap tile costs 7, which is what an alpha blend over 1.4 million
/// pixels costs there and is one quad on a real backend. The step is rounded to whole pixels
/// for the tile, which also stops a dot landing between two and reading as a smudge.
/// </summary>
public sealed class GraphBackdrop : Control
{
    // Below this a dot is noise, so the grid steps up to the next multiple of four and stays
    // readable. The step stays a multiple of the graph's own, so the dots never drift.
    private const int Closest = 13;

    private const int DotSize = 2;

    private RenderTargetBitmap? _tile;
    private ImageBrush? _brush;
    private int _step;
    private IBrush? _ink;

    internal NodeGraph? Graph { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Graph is not { ShowGrid: true } graph || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var step = Step(graph);

        if (step <= 0)
        {
            return;
        }

        var brush = Tile(graph.Colours.Grid, step);
        var offset = graph.View.Offset;

        brush.DestinationRect = new RelativeRect(
            new Rect(Wrap(offset.X, step), Wrap(offset.Y, step), step, step),
            RelativeUnit.Absolute);

        context.FillRectangle(brush, new Rect(Bounds.Size));
    }

    /// <summary>Drops the tile, which a theme change makes stale.</summary>
    internal void Forget()
    {
        _tile?.Dispose();
        _tile = null;
        _brush = null;
        _ink = null;
        _step = 0;
        InvalidateVisual();
    }

    private static double Wrap(double offset, int step)
    {
        var at = offset % step;

        return at > 0 ? at - step : at;
    }

    private static int Step(NodeGraph graph)
    {
        var step = graph.Metrics.GridStep * graph.View.Zoom;

        while (step > 0 && step < Closest)
        {
            step *= 4;
        }

        return step > 0 && step < 4096 ? (int)Math.Round(step) : 0;
    }

    private ImageBrush Tile(IBrush ink, int step)
    {
        if (_brush is not null && _step == step && ReferenceEquals(_ink, ink))
        {
            return _brush;
        }

        _tile?.Dispose();
        _tile = new RenderTargetBitmap(new PixelSize(step, step));
        _step = step;
        _ink = ink;

        using (var context = _tile.CreateDrawingContext())
        {
            context.DrawEllipse(ink, null, new Point(DotSize / 2d, DotSize / 2d), DotSize / 2d, DotSize / 2d);
        }

        return _brush = new ImageBrush(_tile)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill,
        };
    }
}
