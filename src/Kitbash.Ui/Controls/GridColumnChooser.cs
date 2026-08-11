using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// What a person turns on, moves and pins. A list rather than a grid of boxes, since order
/// matters as much as visibility and a list is what can be dragged.
/// </summary>
[TemplatePart(RowsPart, typeof(Panel))]
[TemplatePart(DropPart, typeof(Border))]
[TemplatePart(ResetPart, typeof(Button))]
public class GridColumnChooser : TemplatedControl
{
    private const string RowsPart = "PART_Rows";
    private const string DropPart = "PART_Drop";
    private const string ResetPart = "PART_Reset";

    /// <summary>How far a press has to travel before it is a drag and not a click.</summary>
    private const double Slack = 4;

    public static readonly StyledProperty<GridColumns?> ColumnsProperty =
        AvaloniaProperty.Register<GridColumnChooser, GridColumns?>(nameof(Columns));

    private readonly List<GridColumnChooserRow> rows = [];

    private Panel? host;
    private Border? drop;
    private Button? reset;
    private GridColumns? following;
    private GridColumn? moving;
    private Point? from;
    private int at = -1;
    private int held;
    private bool pins;

    /// <summary>The columns being chosen from. Changes land as they are made.</summary>
    public GridColumns? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (reset is not null)
        {
            reset.Click -= OnReset;
        }

        host = e.NameScope.Find<Panel>(RowsPart);
        drop = e.NameScope.Find<Border>(DropPart);
        reset = e.NameScope.Find<Button>(ResetPart);

        if (reset is not null)
        {
            reset.Click += OnReset;
        }

        Build();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ColumnsProperty)
        {
            return;
        }

        if (following is not null)
        {
            following.LayoutChanged -= OnColumnsChanged;
            following.CollectionChanged -= OnColumnsMoved;
        }

        following = change.GetNewValue<GridColumns?>();

        if (following is not null)
        {
            following.LayoutChanged += OnColumnsChanged;
            following.CollectionChanged += OnColumnsMoved;
        }

        Build();
    }

    private void OnColumnsChanged(object? sender, EventArgs e) => Build();

    private void OnColumnsMoved(object? sender, EventArgs e) => Build();

    private void OnReset(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Columns?.Reset();

    /// <summary>
    /// The rows, pinned ones first under a heading of their own. A group rather than a flag
    /// hidden in each row, since where a column sits is the whole point of the list.
    /// </summary>
    private void Build()
    {
        if (host is null)
        {
            return;
        }

        host.Children.Clear();
        rows.Clear();

        if (Columns is not { } columns)
        {
            return;
        }

        var pinned = columns.Where(column => column.IsPinned).ToList();

        // The group is there whenever pinning is allowed, even with nothing in it, since
        // dragging into it is the only way to pin a column from here.
        pins = columns.Gestures.HasFlag(ColumnGestures.Pin) || pinned.Count > 0;

        if (pins)
        {
            host.Children.Add(Heading("PINNED"));

            foreach (var column in pinned)
            {
                Add(columns, column);
            }

            host.Children.Add(Heading("COLUMNS"));
        }

        held = pinned.Count;

        foreach (var column in columns.Where(column => !column.IsPinned))
        {
            Add(columns, column);
        }
    }

    private void Add(GridColumns columns, GridColumn column)
    {
        var row = new GridColumnChooserRow();

        row.Follow(columns, column);
        row.Moving += OnRowMoving;
        row.Moved += OnRowMoved;
        row.Pressed += OnRowPressed;

        rows.Add(row);
        host!.Children.Add(row);
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        Classes = { "chooserGroup" },
    };

    private void OnRowPressed(object? sender, Point where)
    {
        from = where;
        moving = null;
    }

    private void OnRowMoving(object? sender, Point where)
    {
        if (sender is not GridColumnChooserRow row || row.Column is not { } column || from is not { } start)
        {
            return;
        }

        if (moving is null && Math.Abs(where.Y - start.Y) < Slack)
        {
            return;
        }

        moving = column;
        at = Landing(where.Y);
        Show();
    }

    private void OnRowMoved(object? sender, EventArgs e)
    {
        var column = moving;
        var landing = at;

        moving = null;
        from = null;
        at = -1;
        Show();

        if (column is null || landing < 0 || Columns is not { } columns)
        {
            return;
        }

        var order = rows.Select(row => row.Column).OfType<GridColumn>().ToList();
        var to = Math.Clamp(landing, 0, order.Count - 1);
        var target = order[to];

        // Dropped among the pinned rows is what pins a column, which is why the group is
        // the whole of how pinning reads here rather than a flag on every row.
        if (pins && columns.CanPin(column))
        {
            column.IsPinned = to < held || (to == held && column.IsPinned);
        }

        if (!ReferenceEquals(target, column) && columns.IndexOf(column) is var seat and >= 0)
        {
            columns.Move(seat, columns.IndexOf(target));
        }
    }

    /// <summary>Which row the pointer is nearest, measured down the list.</summary>
    private int Landing(double y)
    {
        var best = 0;
        var near = double.MaxValue;

        for (var index = 0; index < rows.Count; index++)
        {
            var middle = rows[index].Bounds.Y + (rows[index].Bounds.Height / 2);
            var apart = Math.Abs(middle - y);

            if (apart < near)
            {
                near = apart;
                best = index;
            }
        }

        return best;
    }

    private void Show()
    {
        if (drop is null)
        {
            return;
        }

        drop.IsVisible = moving is not null && at >= 0 && at < rows.Count;

        if (drop.IsVisible)
        {
            drop.Margin = new Thickness(0, Math.Max(0, rows[at].Bounds.Y), 0, 0);
        }
    }
}

/// <summary>
/// One column in the chooser. It says whether the column is drawn and whether it is pinned,
/// and it is the thing a person drags to move one.
/// </summary>
public class GridColumnChooserRow : TemplatedControl
{
    public static readonly StyledProperty<GridColumn?> ColumnProperty =
        AvaloniaProperty.Register<GridColumnChooserRow, GridColumn?>(nameof(Column));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<GridColumnChooserRow, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<GridColumnChooserRow, bool>(
            nameof(IsShown),
            true,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<GridColumnChooserRow, bool>(
            nameof(IsPinned),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether this column may be turned off at all.</summary>
    public static readonly StyledProperty<bool> CanHideProperty =
        AvaloniaProperty.Register<GridColumnChooserRow, bool>(nameof(CanHide), true);

    /// <summary>Whether this column may be pinned at all.</summary>
    public static readonly StyledProperty<bool> CanPinProperty =
        AvaloniaProperty.Register<GridColumnChooserRow, bool>(nameof(CanPin), true);

    private GridColumns? owner;
    private bool settling;

    /// <summary>Raised on a press, with where in the chooser it landed.</summary>
    internal event EventHandler<Point>? Pressed;

    /// <summary>Raised as the pointer moves with the button down.</summary>
    internal event EventHandler<Point>? Moving;

    /// <summary>Raised when the button comes up.</summary>
    internal event EventHandler? Moved;

    public GridColumn? Column
    {
        get => GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    /// <summary>The column's title as words, since a header can be a control.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IsShownProperty"/>
    public bool IsShown
    {
        get => GetValue(IsShownProperty);
        set => SetValue(IsShownProperty, value);
    }

    /// <inheritdoc cref="IsPinnedProperty"/>
    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <inheritdoc cref="CanHideProperty"/>
    public bool CanHide
    {
        get => GetValue(CanHideProperty);
        set => SetValue(CanHideProperty, value);
    }

    /// <inheritdoc cref="CanPinProperty"/>
    public bool CanPin
    {
        get => GetValue(CanPinProperty);
        set => SetValue(CanPinProperty, value);
    }

    /// <summary>The column this row stands for, and where its rules come from.</summary>
    internal void Follow(GridColumns columns, GridColumn column)
    {
        owner = columns;
        settling = true;

        Column = column;
        Label = column.Header?.ToString() ?? column.Key ?? string.Empty;
        IsShown = column.IsVisible;
        IsPinned = column.IsPinned;

        // A column that cannot be turned off sits flat and disabled rather than leaving the
        // list, so the list is always the whole set of columns.
        CanHide = columns.CanHide(column) || !column.IsVisible;
        CanPin = columns.CanPin(column);

        settling = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (settling || Column is not { } column || owner is null)
        {
            return;
        }

        // Applied as it is made, so there is no apply button and closing the popover is not
        // a commit.
        if (change.Property == IsShownProperty)
        {
            column.IsVisible = change.GetNewValue<bool>();
        }
        else if (change.Property == IsPinnedProperty)
        {
            column.IsPinned = change.GetNewValue<bool>();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && this.GetVisualParent() is Visual list)
        {
            Pressed?.Invoke(this, e.GetPosition(list));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && this.GetVisualParent() is Visual list)
        {
            Moving?.Invoke(this, e.GetPosition(list));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Moved?.Invoke(this, EventArgs.Empty);
    }
}
