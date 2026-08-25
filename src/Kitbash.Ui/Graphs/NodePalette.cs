using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The add node menu. It opens on Tab, on a right click over nothing and on a wire let go
/// over nothing, and the last of those is what it is really for: dropped off a pin it lists
/// only the nodes that pin could reach and wires the one picked as it makes it.
///
/// One cursor serves the keyboard and the pointer, so hovering a row is the same as arrowing
/// to it and Enter always adds the row a person is looking at.
/// </summary>
[TemplatePart(PartSearch, typeof(TextBox))]
[TemplatePart(PartRows, typeof(ListBox))]
[TemplatePart(PartFilter, typeof(Control))]
[TemplatePart(PartEmpty, typeof(Control))]
[TemplatePart(PartCancel, typeof(Button))]
public class NodePalette : TemplatedControl
{
    public const string PartSearch = "PART_Search";
    public const string PartRows = "PART_Rows";
    public const string PartFilter = "PART_Filter";
    public const string PartEmpty = "PART_Empty";
    public const string PartCancel = "PART_Cancel";

    public static readonly StyledProperty<string> FilterTextProperty =
        AvaloniaProperty.Register<NodePalette, string>(nameof(FilterText), string.Empty);

    public static readonly StyledProperty<bool> IsFilteredProperty =
        AvaloniaProperty.Register<NodePalette, bool>(nameof(IsFiltered));

    public static readonly StyledProperty<bool> IsEmptyProperty =
        AvaloniaProperty.Register<NodePalette, bool>(nameof(IsEmpty));

    private readonly List<object> _rows = [];
    private readonly List<NodeChoiceRow> _offered = [];

    private TextBox? _search;
    private ListBox? _rowList;
    private Button? _cancel;

    public NodePalette()
    {
        // Nothing inside the menu reaches the canvas. A press would otherwise start a box
        // select behind it and a wheel would zoom the graph out from under it.
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Raised when a person picks an entry.</summary>
    public event EventHandler<NodeChoiceEventArgs>? Chosen;

    /// <summary>Raised when the menu is closed without picking anything.</summary>
    public event EventHandler? Dismissed;

    /// <summary>Everything the app offers.</summary>
    public IReadOnlyList<NodeChoice> Choices { get; set; } = [];

    /// <summary>Where a family's icon and colour come from.</summary>
    public GraphKinds? Kinds { get; set; }

    /// <summary>Where a port type's colour comes from.</summary>
    public IPortPalette? Ports { get; set; }

    /// <summary>Whether two ports may be joined, which is what the pin filter reads.</summary>
    public IPortRules Rules { get; set; } = PortRules.Strict;

    /// <summary>The pin it was opened from, which narrows the list to what could land on it.</summary>
    public GraphPort? From { get; private set; }

    /// <summary>The line that says the list is narrowed, or nothing.</summary>
    public string FilterText
    {
        get => GetValue(FilterTextProperty);
        private set => SetValue(FilterTextProperty, value);
    }

    public bool IsFiltered
    {
        get => GetValue(IsFilteredProperty);
        private set => SetValue(IsFilteredProperty, value);
    }

    /// <summary>Whether the query names nothing at all.</summary>
    public bool IsEmpty
    {
        get => GetValue(IsEmptyProperty);
        private set => SetValue(IsEmptyProperty, value);
    }

    /// <summary>What the cursor is on, or none while the list is empty.</summary>
    public NodeChoice? Current =>
        _rowList?.SelectedItem is NodeChoiceRow row ? row.Choice : null;

    /// <summary>Opens it, empties the query and puts the cursor on the first entry.</summary>
    public void Open(GraphPort? from)
    {
        From = from;
        IsFiltered = from is not null;
        FilterText = from is null ? string.Empty : $"Showing nodes that take {from.Type}";

        if (_search is not null)
        {
            _search.Text = string.Empty;
        }

        Offer();
        IsVisible = true;

        // The query takes the focus, so a person can type straight away and the arrows still
        // reach here rather than moving a caret through a single line field.
        _search?.Focus();
    }

    public void Close()
    {
        if (!IsVisible)
        {
            return;
        }

        IsVisible = false;
        From = null;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves the cursor by a number of entries, stopping at either end.</summary>
    public void Step(int by)
    {
        if (_rowList is null || _offered.Count == 0)
        {
            return;
        }

        var at = _rowList.SelectedItem is NodeChoiceRow row ? _offered.IndexOf(row) : -1;
        var wanted = Math.Clamp(at + by, 0, _offered.Count - 1);

        _rowList.SelectedItem = _offered[wanted];
        _rowList.ScrollIntoView(_offered[wanted]);
    }

    /// <summary>Adds whatever the cursor is on.</summary>
    public void Commit()
    {
        if (Current is { } choice)
        {
            Chosen?.Invoke(this, new NodeChoiceEventArgs(choice));
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_search is not null)
        {
            _search.KeyDown -= OnSearchKey;
            _search.TextChanged -= OnQueryChanged;
        }

        if (_cancel is not null)
        {
            _cancel.Click -= OnCancel;
        }

        if (_rowList is not null)
        {
            _rowList.ContainerPrepared -= OnContainerPrepared;
            _rowList.ContainerIndexChanged -= OnContainerMoved;
            _rowList.RemoveHandler(PointerMovedEvent, OnRowsMoved);
            _rowList.RemoveHandler(PointerReleasedEvent, OnRowsReleased);
        }

        _search = e.NameScope.Find<TextBox>(PartSearch);
        _rowList = e.NameScope.Find<ListBox>(PartRows);
        _cancel = e.NameScope.Find<Button>(PartCancel);

        if (_cancel is not null)
        {
            _cancel.Click += OnCancel;
        }

        if (_search is not null)
        {
            _search.KeyDown += OnSearchKey;
            _search.TextChanged += OnQueryChanged;
        }

        if (_rowList is not null)
        {
            _rowList.ItemsSource = _rows;
            _rowList.ContainerPrepared += OnContainerPrepared;
            _rowList.ContainerIndexChanged += OnContainerMoved;
            _rowList.AddHandler(PointerMovedEvent, OnRowsMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            _rowList.AddHandler(PointerReleasedEvent, OnRowsReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        Offer();
    }

    /// <summary>
    /// Builds the list. A heading is a row rather than a container around one, so the list
    /// stays a flat list and virtualises the way every other list here does.
    /// </summary>
    private void Offer()
    {
        var query = (_search?.Text ?? string.Empty).Trim();
        var kinds = Kinds;
        var ports = Ports;

        _rows.Clear();
        _offered.Clear();

        var heading = string.Empty;
        var first = true;

        foreach (var choice in Choices)
        {
            if (!choice.Matches(query) || (From is { } from && !choice.Takes(from, Rules)))
            {
                continue;
            }

            if (first || !string.Equals(choice.Group, heading, StringComparison.Ordinal))
            {
                heading = choice.Group;
                first = false;

                if (heading.Length > 0)
                {
                    _rows.Add(new NodeChoiceHeading(heading));
                }
            }

            var kind = kinds?[choice.Kind];
            var row = new NodeChoiceRow(
                choice,
                kind?.Icon,
                kind?.Ink ?? Foreground ?? Brushes.White,
                ports?.Brush(choice.Type) ?? Foreground ?? Brushes.White);

            _rows.Add(row);
            _offered.Add(row);
        }

        IsEmpty = _offered.Count == 0;

        if (_rowList is null)
        {
            return;
        }

        _rowList.ItemsSource = null;
        _rowList.ItemsSource = _rows;
        _rowList.SelectedItem = _offered.Count > 0 ? _offered[0] : null;
    }

    private void OnQueryChanged(object? sender, TextChangedEventArgs e) => Offer();

    private void OnSearchKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Step(1);
                e.Handled = true;
                break;

            case Key.Up:
                Step(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                Commit();
                e.Handled = true;
                break;

            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// A heading cannot be picked and does not react to a pointer. It is told here and
    /// untold here, since a container is recycled onto another row.
    /// </summary>
    private static void Follow(Control container, object? item) =>
        container.IsEnabled = item is not NodeChoiceHeading;

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e) =>
        Follow(e.Container, _rows.Count > e.Index ? _rows[e.Index] : null);

    private void OnContainerMoved(object? sender, ContainerIndexChangedEventArgs e) =>
        Follow(e.Container, _rows.Count > e.NewIndex ? _rows[e.NewIndex] : null);

    /// <summary>
    /// The pointer moves the same cursor the arrows do. One cursor rather than a hover and a
    /// selection means Enter always adds the row a person is looking at.
    /// </summary>
    private void OnRowsMoved(object? sender, PointerEventArgs e)
    {
        if (Row(e.Source) is { } row && _rowList is not null && !ReferenceEquals(_rowList.SelectedItem, row))
        {
            _rowList.SelectedItem = row;
        }
    }

    private void OnRowsReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || Row(e.Source) is not { } row)
        {
            return;
        }

        Chosen?.Invoke(this, new NodeChoiceEventArgs(row.Choice));
        e.Handled = true;
    }

    private static NodeChoiceRow? Row(object? source) =>
        source is Visual visual
            ? visual.FindAncestorOfType<ListBoxItem>(true)?.DataContext as NodeChoiceRow
            : null;

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnWheel(object? sender, PointerWheelEventArgs e) => e.Handled = true;
}

/// <summary>Which entry was picked.</summary>
public sealed class NodeChoiceEventArgs(NodeChoice choice) : EventArgs
{
    public NodeChoice Choice { get; } = choice;
}
