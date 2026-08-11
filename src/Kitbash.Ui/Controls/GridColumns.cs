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

    /// <summary>Each column as the tool declared it, which is what a change is measured from.</summary>
    private readonly Dictionary<GridColumn, Declared> declared = [];

    private readonly record struct Declared(GridLength Width, bool IsVisible, bool IsPinned, int Order);

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
    /// How wide the pinned columns came out, which is where the seam falls. Zero when none
    /// are pinned, and that is the case where nothing about the layout changes at all.
    /// </summary>
    public double PinnedWidth { get; private set; }

    /// <summary>
    /// What a person may do to these columns. The grid holds the property and hands it over,
    /// and a column still has to allow the gesture for itself.
    /// </summary>
    public ColumnGestures Gestures { get; set; } = ColumnGestures.Resize | ColumnGestures.FitToContents;

    /// <summary>Whether this column can be dragged wider or narrower.</summary>
    public bool CanResize(GridColumn column) => Gestures.HasFlag(ColumnGestures.Resize) && column.CanResize;

    /// <summary>Whether this column can be fitted to what is on screen.</summary>
    public bool CanFit(GridColumn column) => Gestures.HasFlag(ColumnGestures.FitToContents) && column.CanResize;

    /// <summary>
    /// Whether this column can be taken off. The last one showing never can, since a grid
    /// with no columns is a grid nothing can be put back through.
    /// </summary>
    public bool CanHide(GridColumn column) =>
        Gestures.HasFlag(ColumnGestures.Hide) && column.IsVisible && Reachable.Count > 1;

    /// <summary>
    /// Whether this column can be held against the left edge. The last one not pinned never
    /// can, since pinning every column leaves nothing to scroll under them.
    /// </summary>
    public bool CanPin(GridColumn column) =>
        Gestures.HasFlag(ColumnGestures.Pin)
        && column.CanPin
        && (column.IsPinned || Reachable.Count(other => !other.IsPinned) > 1);

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
        var pinned = 0d;

        // Pinned columns take their offsets first, so they sit against the left edge whatever
        // order they were declared in. With none pinned this is the declared order, which is
        // what keeps the ordinary grid on exactly the pass it always had.
        var ordered = this.Any(column => column.IsPinned)
            ? this.Where(column => column.IsPinned).Concat(this.Where(column => !column.IsPinned))
            : (IEnumerable<GridColumn>)this;

        foreach (var column in ordered)
        {
            var width = widths[column];

            if (column.ActualWidth != width || column.Offset != offset)
            {
                moved = true;
            }

            column.ActualWidth = width;
            column.Offset = offset;
            offset += width;

            if (column.IsPinned)
            {
                pinned = offset;
            }
        }

        if (TotalWidth != offset)
        {
            TotalWidth = offset;
            moved = true;
        }

        if (PinnedWidth != pinned)
        {
            PinnedWidth = pinned;
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

    /// <summary>
    /// What a person has changed about the columns, and nothing else. A column left as the
    /// tool declared it says nothing at all, so a layout kept from one run does not pin a
    /// grid to a declaration that has since moved.
    /// </summary>
    public IReadOnlyList<GridColumnState> Capture()
    {
        var kept = new List<GridColumnState>();

        for (var index = 0; index < Count; index++)
        {
            var column = this[index];

            if (column.Key is not { Length: > 0 } key || !declared.TryGetValue(column, out var was))
            {
                continue;
            }

            var state = new GridColumnState(
                key,
                was.Width.Equals(column.Width) ? null : column.Width.Value,
                was.IsVisible == column.IsVisible ? null : column.IsVisible,
                was.IsPinned == column.IsPinned ? null : column.IsPinned,
                was.Order == index ? null : index);

            if (state.Matters)
            {
                kept.Add(state);
            }
        }

        return kept;
    }

    /// <summary>
    /// Puts a kept layout back. A column the layout does not name is left as the tool
    /// declared it, and a name the grid no longer has is passed over.
    /// </summary>
    public void Apply(IEnumerable<GridColumnState> layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        foreach (var state in layout)
        {
            if (Find(state.Key) is not { } column)
            {
                continue;
            }

            if (state.Width is { } width)
            {
                column.Width = new GridLength(Clamp(column, width), GridUnitType.Pixel);
            }

            if (state.IsVisible is { } visible)
            {
                column.IsVisible = visible;
            }

            if (state.IsPinned is { } pinned)
            {
                column.IsPinned = pinned;
            }
        }

        // After the rest, since moving a column while reading the layout would change what
        // the indexes in it mean.
        foreach (var state in layout.Where(state => state.Order is not null).OrderBy(state => state.Order))
        {
            if (Find(state.Key) is { } column && IndexOf(column) is var from and >= 0)
            {
                Move(from, Math.Clamp(state.Order!.Value, 0, Count - 1));
            }
        }
    }

    /// <summary>Puts every column back the way the tool declared it.</summary>
    public void Reset()
    {
        foreach (var (column, was) in declared.OrderBy(pair => pair.Value.Order).ToList())
        {
            column.Width = was.Width;
            column.IsVisible = was.IsVisible;
            column.IsPinned = was.IsPinned;

            if (IndexOf(column) is var from and >= 0 && from != was.Order)
            {
                Move(from, Math.Clamp(was.Order, 0, Count - 1));
            }
        }
    }

    private GridColumn? Find(string key) =>
        this.FirstOrDefault(column => string.Equals(column.Key, key, StringComparison.Ordinal));

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

            // Taken as the column arrives, so what a person changed later can be told from
            // what the tool asked for. A move does not raise Add, so this is only ever the
            // declaration.
            declared.TryAdd(column, new Declared(column.Width, column.IsVisible, column.IsPinned, IndexOf(column)));
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnColumnChanged(object? sender, EventArgs e) => LayoutChanged?.Invoke(this, EventArgs.Empty);
}
