using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A well drawing a ramp, which opens its own editor in a popover. Points draw as a line
/// and stops draw as a gradient, so a curve and a colour ramp are the same field at two
/// widths. The editor is whatever the host puts in it.
/// </summary>
public class CurveField : Button
{
    /// <summary>
    /// The run, normalised. X is the position from nothing to one and Y is the value, with
    /// one at the top. A run shorter than two points draws nothing.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<Point>?> PointsProperty =
        AvaloniaProperty.Register<CurveField, IReadOnlyList<Point>?>(nameof(Points));

    /// <summary>The colours along the ramp. When there are any, they are what is drawn.</summary>
    public static readonly StyledProperty<IReadOnlyList<GradientStop>?> StopsProperty =
        AvaloniaProperty.Register<CurveField, IReadOnlyList<GradientStop>?>(nameof(Stops));

    /// <summary>What the popover holds. Nothing opens when it is null.</summary>
    public static readonly StyledProperty<object?> EditorProperty =
        AvaloniaProperty.Register<CurveField, object?>(nameof(Editor));

    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<CurveField, object?>(nameof(Header), "RAMP");

    /// <summary>The line through the points, in this control's own pixels.</summary>
    public static readonly DirectProperty<CurveField, Geometry?> TraceProperty =
        AvaloniaProperty.RegisterDirect<CurveField, Geometry?>(nameof(Trace), field => field.Trace);

    /// <summary>The gradient the stops describe, or null when there are none.</summary>
    public static readonly DirectProperty<CurveField, IBrush?> WashProperty =
        AvaloniaProperty.RegisterDirect<CurveField, IBrush?>(nameof(Wash), field => field.Wash);

    /// <summary>Room left above and below, so a run at either end is not on the edge.</summary>
    private const double Inset = 4;

    private readonly Popover _popover = new();
    private readonly Flyout _flyout;

    private Geometry? _trace;
    private IBrush? _wash;

    public CurveField()
    {
        _flyout = new Flyout
        {
            Content = _popover,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            FlyoutPresenterClasses = { "popover" },
        };

        Flyout = _flyout;
    }

    /// <inheritdoc cref="PointsProperty"/>
    public IReadOnlyList<Point>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <inheritdoc cref="StopsProperty"/>
    public IReadOnlyList<GradientStop>? Stops
    {
        get => GetValue(StopsProperty);
        set => SetValue(StopsProperty, value);
    }

    /// <inheritdoc cref="EditorProperty"/>
    public object? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="TraceProperty"/>
    public Geometry? Trace
    {
        get => _trace;
        private set => SetAndRaise(TraceProperty, ref _trace, value);
    }

    /// <inheritdoc cref="WashProperty"/>
    public IBrush? Wash
    {
        get => _wash;
        private set => SetAndRaise(WashProperty, ref _wash, value);
    }

    protected override Size ArrangeOverride(Size size)
    {
        var arranged = base.ArrangeOverride(size);

        Redraw(arranged);

        return arranged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);

        base.OnPropertyChanged(change);

        if (change.Property == PointsProperty || change.Property == StopsProperty)
        {
            Redraw(Bounds.Size);
        }

        if (change.Property == EditorProperty || change.Property == HeaderProperty)
        {
            _popover.Content = Editor;
            _popover.Header = Header;
        }
    }

    /// <summary>
    /// The line and the gradient worked out again. Both are in this control's own pixels,
    /// so they are made here rather than scaled, which would thicken the line one way.
    /// </summary>
    private void Redraw(Size size)
    {
        Wash = Gradient();
        Trace = Line(size);
    }

    private IBrush? Gradient()
    {
        if (Stops is not { Count: > 0 } stops)
        {
            return null;
        }

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };

        foreach (var stop in stops)
        {
            brush.GradientStops.Add(stop);
        }

        return brush;
    }

    private Geometry? Line(Size size)
    {
        if (Stops is { Count: > 0 }
            || Points is not { Count: >= 2 } points
            || size.Width <= 0
            || size.Height <= Inset * 2)
        {
            return null;
        }

        var tall = size.Height - (Inset * 2);
        var geometry = new StreamGeometry();

        using var run = geometry.Open();

        for (var at = 0; at < points.Count; at++)
        {
            var point = new Point(
                Math.Clamp(points[at].X, 0, 1) * size.Width,
                Inset + ((1 - Math.Clamp(points[at].Y, 0, 1)) * tall));

            if (at == 0)
            {
                run.BeginFigure(point, isFilled: false);

                continue;
            }

            run.LineTo(point);
        }

        run.EndFigure(isClosed: false);

        return geometry;
    }
}
