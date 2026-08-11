using System.Collections.Specialized;
using Avalonia.Collections;
using Avalonia.Controls;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The columns of a grid, and the one place their widths are worked out. Both grids share
/// an instance of this rather than each keeping a model of its own.
/// </summary>
public sealed class GridColumns : AvaloniaList<GridColumn>
{
    /// <summary>How many times the solver hands out what clamping freed up.</summary>
    private const int Passes = 4;

    public GridColumns()
    {
        CollectionChanged += OnCollectionChanged;
    }

    /// <summary>
    /// Raised when a column is added, taken away, or changed in a way that moves the
    /// layout. A grid listens and lays its header and its rows out again.
    /// </summary>
    public event EventHandler? LayoutChanged;

    /// <summary>How wide the whole set came out. Wider than the viewport means it scrolls.</summary>
    public double TotalWidth { get; private set; }

    /// <summary>
    /// What a person may do to these columns. The grid holds the property and hands it over,
    /// and a column still has to allow the gesture for itself.
    /// </summary>
    public ColumnGestures Gestures { get; set; } = ColumnGestures.Resize | ColumnGestures.FitToContents;

    /// <summary>Whether this column can be dragged wider or narrower.</summary>
    public bool CanResize(GridColumn column) => Gestures.HasFlag(ColumnGestures.Resize) && column.CanResize;

    /// <summary>Whether this column can be fitted to what is on screen.</summary>
    public bool CanFit(GridColumn column) => Gestures.HasFlag(ColumnGestures.FitToContents) && column.CanResize;

    /// <summary>The columns a person can reach, in the order they are drawn.</summary>
    public IReadOnlyList<GridColumn> Reachable => [.. this.Where(column => column.IsVisible)];

    /// <summary>
    /// Works out every visible column's width and offset for a viewport this wide, and
    /// says whether anything moved. Star columns take what the pixel ones leave, so they
    /// never make the grid scroll sideways on their own.
    /// </summary>
    public bool Resolve(double available)
    {
        if (double.IsNaN(available) || double.IsInfinity(available) || available < 0)
        {
            available = 0;
        }

        var widths = new Dictionary<GridColumn, double>();
        var stars = new List<GridColumn>();
        var taken = 0d;

        foreach (var column in this)
        {
            if (!column.IsVisible)
            {
                widths[column] = 0;
            }
            else if (column.Width.IsStar)
            {
                stars.Add(column);
            }
            else
            {
                widths[column] = Clamp(column, column.Width.Value);
                taken += widths[column];
            }
        }

        Share(stars, widths, Math.Max(0, available - taken));

        var moved = false;
        var offset = 0d;

        foreach (var column in this)
        {
            var width = widths[column];

            if (column.ActualWidth != width || column.Offset != offset)
            {
                moved = true;
            }

            column.ActualWidth = width;
            column.Offset = offset;
            offset += width;
        }

        if (TotalWidth != offset)
        {
            TotalWidth = offset;
            moved = true;
        }

        return moved;
    }

    /// <summary>
    /// Hands a column a pixel width, which is what a resize leaves behind. A star column
    /// stops being one, since a person who has dragged an edge means that width.
    /// </summary>
    public void SetWidth(GridColumn column, double width) =>
        column.Width = new GridLength(Clamp(column, width), GridUnitType.Pixel);

    /// <summary>
    /// Splits what is left between the star columns. A column that clamps frees room the
    /// others can take, so the share is worked out again rather than only once.
    /// </summary>
    private static void Share(List<GridColumn> stars, Dictionary<GridColumn, double> widths, double room)
    {
        var open = stars;

        for (var pass = 0; pass < Passes && open.Count > 0; pass++)
        {
            var weight = open.Sum(column => Math.Max(0, column.Width.Value));

            if (weight <= 0)
            {
                weight = open.Count;
            }

            var clamped = new List<GridColumn>();

            foreach (var column in open)
            {
                var share = room * Math.Max(0, column.Width.Value) / weight;
                var width = Clamp(column, share);

                widths[column] = width;

                if (width != share)
                {
                    clamped.Add(column);
                }
            }

            if (clamped.Count == 0)
            {
                return;
            }

            room = Math.Max(0, room - clamped.Sum(column => widths[column]));
            open = open.Except(clamped).ToList();
        }

        // Out of passes with columns still open. Whatever the last share was stands, so
        // the grid draws rather than leaving a column at nothing.
    }

    private static double Clamp(GridColumn column, double width)
    {
        var min = double.IsNaN(column.MinWidth) ? 0 : Math.Max(0, column.MinWidth);
        var max = double.IsNaN(column.MaxWidth) ? double.PositiveInfinity : column.MaxWidth;

        return Math.Min(Math.Max(width, min), Math.Max(min, max));
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var column in e.OldItems?.OfType<GridColumn>() ?? [])
        {
            column.LayoutChanged -= OnColumnChanged;
        }

        foreach (var column in e.NewItems?.OfType<GridColumn>() ?? [])
        {
            column.LayoutChanged += OnColumnChanged;
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnColumnChanged(object? sender, EventArgs e) => LayoutChanged?.Invoke(this, EventArgs.Empty);
}
