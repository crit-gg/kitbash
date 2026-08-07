using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The splash card's art. A lozenge lattice, the corner brackets and the veil over both,
/// drawn here so the window needs no vector package, no image decode and no theme.
/// </summary>
public sealed class SplashBackdrop : Control
{
    // The tile of splash-lozenge-tile.svg in the design project, in device independent
    // pixels. The lattice repeats on this grid and never scales with the card.
    private const double TileWidth = 64;
    private const double TileHeight = 32;

    private static readonly IPen LatticeEdge =
        new Pen(new ImmutableSolidColorBrush(Color.FromArgb(28, 0xff, 0xff, 0xff)));

    private static readonly IBrush LatticeFill =
        new ImmutableSolidColorBrush(Color.FromArgb(20, 0x56, 0x9e, 0xff));

    private static readonly IPen LatticeInnerEdge =
        new Pen(new ImmutableSolidColorBrush(Color.FromArgb(71, 0x56, 0x9e, 0xff)));

    private static readonly IPen BracketInner =
        new Pen(new ImmutableSolidColorBrush(Color.FromArgb(15, 0xff, 0xff, 0xff)), 1);

    // The accent glow rising from below the card, then the tone that settles the whole
    // pattern back down. Both are relative, so one instance serves any size.
    private static readonly IBrush Glow = new RadialGradientBrush
    {
        Center = new RelativePoint(0.5, 1.08, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(0.5, 1.08, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(0.7, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(1.1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(41, 0x56, 0x9e, 0xff), 0),
            new GradientStop(Color.FromArgb(0, 0x56, 0x9e, 0xff), 0.62),
        },
    };

    private static readonly IBrush Veil = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(158, 0x1e, 0x1f, 0x22), 0),
            new GradientStop(Color.FromArgb(46, 0x1e, 0x1f, 0x22), 0.45),
            new GradientStop(Color.FromArgb(77, 0x0f, 0x10, 0x12), 1),
        },
    };

    private readonly List<(Geometry Shape, IPen Pen)> _brackets = [];

    private Geometry? _outlines;
    private Geometry? _inners;
    private Size _built;

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;

        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        Build(size);

        context.DrawGeometry(null, LatticeEdge, _outlines!);
        context.DrawGeometry(LatticeFill, LatticeInnerEdge, _inners!);

        foreach (var (shape, pen) in _brackets)
        {
            context.DrawGeometry(null, pen, shape);
        }

        var whole = new Rect(size);

        context.FillRectangle(Glow, whole);
        context.FillRectangle(Veil, whole);
    }

    /// <summary>The art is cut for one size, so a new size throws the last one away.</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (finalSize != _built)
        {
            _outlines = null;
            InvalidateVisual();
        }

        return base.ArrangeOverride(finalSize);
    }

    private static Geometry Polyline(params Point[] points)
    {
        var shape = new StreamGeometry();

        using (var context = shape.Open())
        {
            context.BeginFigure(points[0], false);

            for (var index = 1; index < points.Length; index++)
            {
                context.LineTo(points[index]);
            }

            context.EndFigure(false);
        }

        return shape;
    }

    private void Build(Size size)
    {
        if (_outlines is not null && _built == size)
        {
            return;
        }

        _built = size;
        _outlines = Lattice(size, 32, 16);
        _inners = Lattice(size, 20, 10);

        BuildBrackets(size);
    }

    /// <summary>
    /// Every diamond the card can show, as one geometry. <paramref name="halfWidth"/> and
    /// <paramref name="halfHeight"/> pick the outer diamond or the smaller one inside it.
    /// </summary>
    private static Geometry Lattice(Size size, double halfWidth, double halfHeight)
    {
        var shape = new StreamGeometry();

        using (var context = shape.Open())
        {
            for (var y = TileHeight / 2; y - TileHeight / 2 < size.Height; y += TileHeight)
            {
                for (var x = TileWidth / 2; x - TileWidth / 2 < size.Width; x += TileWidth)
                {
                    context.BeginFigure(new Point(x, y - halfHeight), true);
                    context.LineTo(new Point(x + halfWidth, y));
                    context.LineTo(new Point(x, y + halfHeight));
                    context.LineTo(new Point(x - halfWidth, y));
                    context.EndFigure(true);
                }
            }
        }

        return shape;
    }

    /// <summary>
    /// The corner brackets, mirrored about the vertical centre. Two at the top and one at
    /// the bottom, all of them the inner lines of the corner files.
    /// </summary>
    private void BuildBrackets(Size size)
    {
        _brackets.Clear();

        var right = size.Width;
        var foot = size.Height;

        _brackets.Add((Polyline(
            new Point(9.5, 81.5),
            new Point(9.5, 9.5),
            new Point(81.5, 9.5)), BracketInner));

        _brackets.Add((Polyline(
            new Point(18.5, 72.5),
            new Point(18.5, 18.5),
            new Point(72.5, 18.5)), BracketInner));

        _brackets.Add((Polyline(
            new Point(right - 81.5, 9.5),
            new Point(right - 9.5, 9.5),
            new Point(right - 9.5, 81.5)), BracketInner));

        _brackets.Add((Polyline(
            new Point(right - 72.5, 18.5),
            new Point(right - 18.5, 18.5),
            new Point(right - 18.5, 72.5)), BracketInner));

        _brackets.Add((Polyline(
            new Point(9.5, foot - 58.5),
            new Point(9.5, foot - 9.5),
            new Point(58.5, foot - 9.5)), BracketInner));

        _brackets.Add((Polyline(
            new Point(right - 9.5, foot - 58.5),
            new Point(right - 9.5, foot - 9.5),
            new Point(right - 58.5, foot - 9.5)), BracketInner));
    }
}
