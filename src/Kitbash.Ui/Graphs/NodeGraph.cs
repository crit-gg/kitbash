using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Media.Immutable;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A canvas of nodes joined by wires, built for many hundreds of them at once. The geometry
/// is data rather than layout, only what meets the viewport is realised, and a pan moves a
/// transform rather than running a layout pass.
/// </summary>
[TemplatePart(PartBackdrop, typeof(GraphBackdrop))]
[TemplatePart(PartWires, typeof(GraphWires))]
[TemplatePart(PartNodes, typeof(NodeGraphPanel))]
[TemplatePart(PartMarquee, typeof(GraphMarquee))]
[TemplatePart(PartPalette, typeof(NodePalette))]
[TemplatePart(PartRename, typeof(TextBox))]
public class NodeGraph : TemplatedControl
{
    public const string PartBackdrop = "PART_Backdrop";
    public const string PartWires = "PART_Wires";
    public const string PartNodes = "PART_Nodes";
    public const string PartMarquee = "PART_Marquee";
    public const string PartPalette = "PART_Palette";
    public const string PartRename = "PART_Rename";

    public static readonly StyledProperty<GraphModel?> ModelProperty =
        AvaloniaProperty.Register<NodeGraph, GraphModel?>(nameof(Model));

    /// <summary>The geometry every node and wire is worked out from. The density set.</summary>
    public static readonly StyledProperty<GraphMetrics> MetricsProperty =
        AvaloniaProperty.Register<NodeGraph, GraphMetrics>(nameof(Metrics), GraphMetrics.Dense);

    public static readonly StyledProperty<WireStyle> WireStyleProperty =
        AvaloniaProperty.Register<NodeGraph, WireStyle>(nameof(WireStyle));

    /// <summary>Whether a dragged node lands on the grid.</summary>
    public static readonly StyledProperty<bool> SnapToGridProperty =
        AvaloniaProperty.Register<NodeGraph, bool>(nameof(SnapToGrid));

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<NodeGraph, bool>(nameof(ShowGrid), true);

    /// <summary>
    /// What fills a node's body. Null is the drawn node, which is one control per node and
    /// the fast path. A template is the composed node, for a body holding real controls.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> NodeTemplateProperty =
        AvaloniaProperty.Register<NodeGraph, IDataTemplate?>(nameof(NodeTemplate));

    /// <summary>
    /// What fills a node's footer strip, the room <see cref="GraphNode.FooterHeight"/> leaves
    /// under the body. A node that asks for no footer never builds one.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<NodeGraph, IDataTemplate?>(nameof(FooterTemplate));

    /// <summary>What the app puts over the canvas: a zoom bar, a minimap, a status strip.</summary>
    public static readonly StyledProperty<object?> OverlayProperty =
        AvaloniaProperty.Register<NodeGraph, object?>(nameof(Overlay));

    /// <summary>
    /// What the add node menu offers. Given one, the graph opens its own menu on Tab, on a
    /// right click over nothing and on a wire let go over nothing. Left empty it raises
    /// <see cref="PaletteAsked"/> instead and an app opens whatever it likes.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<NodeChoice>> CatalogueProperty =
        AvaloniaProperty.Register<NodeGraph, IReadOnlyList<NodeChoice>>(nameof(Catalogue), []);

    private readonly Dictionary<IconGlyph, Geometry?> _glyphs = [];
    private readonly Dictionary<IBrush, IPen> _pinPens = [];
    private readonly Dictionary<Color, IPen> _framePens = [];
    private readonly List<GraphItem> _found = [];
    private readonly List<GraphItem> _caught = [];
    private readonly List<GraphLink> _wiresNear = [];
    private readonly Dictionary<GraphItem, Point> _moveFrom = [];

    private GraphBackdrop? _backdrop;
    private GraphWires? _wires;
    private NodeGraphPanel? _panel;
    private GraphMarquee? _marquee;
    private NodePalette? _palette;
    private TextBox? _rename;
    private GraphItem? _renaming;
    private bool _ending;

    private GraphKinds? _kinds;
    private IPortPalette? _ports;
    private bool _kindsGiven;
    private bool _portsGiven;
    private IPen? _fatWire;

    private static readonly Cursor Plain = new(StandardCursorType.Arrow);
    private static readonly Cursor Crosshair = new(StandardCursorType.Cross);
    private static readonly Cursor Grabbing = new(StandardCursorType.SizeAll);
    private static readonly Cursor Pointing = new(StandardCursorType.Hand);
    private static readonly Cursor Across = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor Down = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor UpLeft = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor UpRight = new(StandardCursorType.TopRightCorner);
    private static readonly Cursor DownLeft = new(StandardCursorType.BottomLeftCorner);
    private static readonly Cursor DownRight = new(StandardCursorType.BottomRightCorner);

    private GraphLod _lod = GraphLod.Full;
    private Grab _grab;
    private Point _grabScreen;
    private Point _grabGraph;
    private Vector _grabOffset;
    private GraphItem[] _kept = [];
    private GraphPort? _linkFrom;
    private GraphLink? _rerouteLink;
    private int _rerouteAt;
    private Point _rerouteFrom;
    private GraphFrame? _resizing;
    private GraphEdges _resizedEdges;
    private Rect _resizedFrom;
    private bool _dragged;
    private Point _askedAt;
    private Point _askedScreen;
    private int _made;
    private GraphItem[] _before = [];
    private GraphPort? _detachedFrom;
    private GraphPort? _detachedTo;
    private Point[] _detachedRun = [];

    static NodeGraph()
    {
        FocusableProperty.OverrideDefaultValue<NodeGraph>(true);
        ClipToBoundsProperty.OverrideDefaultValue<NodeGraph>(true);
    }

    public NodeGraph()
    {
        View.Changed += (_, _) => OnViewChanged();
        Selection.Changed += (_, _) => Repaint();
        AddHandler(DoubleTappedEvent, OnDoubleTap);
        ActualThemeVariantChanged += (_, _) => Reread();

        // Resources are only reachable in a tree, so this run finds none and stands the
        // brushes up as something rather than null. OnAttachedToVisualTree does it properly.
        Reread();
    }

    /// <summary>Raised when a person asks for a node to be added, by Tab, by a right click on
    /// empty space, or by dropping a wire on nothing. What opens is the app's own.</summary>
    public event EventHandler<GraphPaletteEventArgs>? PaletteAsked;

    /// <summary>Raised on a double click that did not land on a wire.</summary>
    public event EventHandler<GraphHitEventArgs>? Activated;

    /// <summary>Raised on a right click, saying what was under it.</summary>
    public event EventHandler<GraphHitEventArgs>? ContextAsked;

    /// <summary>Raised when the model changed, so a minimap knows to draw itself again.</summary>
    public event EventHandler<GraphChangedEventArgs>? GraphChanged;

    public GraphModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public GraphMetrics Metrics
    {
        get => GetValue(MetricsProperty);
        set => SetValue(MetricsProperty, value);
    }

    public WireStyle WireStyle
    {
        get => GetValue(WireStyleProperty);
        set => SetValue(WireStyleProperty, value);
    }

    public bool SnapToGrid
    {
        get => GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public IDataTemplate? NodeTemplate
    {
        get => GetValue(NodeTemplateProperty);
        set => SetValue(NodeTemplateProperty, value);
    }

    public IDataTemplate? FooterTemplate
    {
        get => GetValue(FooterTemplateProperty);
        set => SetValue(FooterTemplateProperty, value);
    }

    public object? Overlay
    {
        get => GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    public IReadOnlyList<NodeChoice> Catalogue
    {
        get => GetValue(CatalogueProperty);
        set => SetValue(CatalogueProperty, value);
    }

    /// <summary>Where the graph is looked at from.</summary>
    public GraphView View { get; } = new();

    /// <summary>What is picked.</summary>
    public GraphSelection Selection { get; } = new();

    /// <summary>The laid out text, kept across pans, zooms and recycled containers.</summary>
    public GraphText Text { get; } = new();

    /// <summary>What colour a family of node is drawn in.</summary>
    public GraphKinds Kinds
    {
        get => _kinds ??= new GraphKinds(new GraphKind(IconGlyph.Cube, Colours.Label));
        set
        {
            _kinds = value;
            _kindsGiven = true;
            Repaint();
        }
    }

    /// <summary>What colour a port type is drawn in.</summary>
    public IPortPalette Ports
    {
        get => _ports ??= new PortPalette(Colours.Pins);
        set
        {
            _ports = value;
            _portsGiven = true;
            Repaint();
        }
    }

    /// <summary>Whether two ports may be joined. Strict by default.</summary>
    public IPortRules Rules { get; set; } = PortRules.Strict;

    /// <summary>Every brush the canvas draws with, off the tokens.</summary>
    public GraphColours Colours { get; private set; } = null!;

    /// <summary>How many containers are realised. What a graph's size is read against.</summary>
    public int RealisedCount => _panel?.RealisedCount ?? 0;

    internal LiveWire? LiveWire { get; private set; }

    internal Rect? Marquee { get; private set; }

    /// <summary>The stroke being drawn to cut wires with, in canvas pixels, or none.</summary>
    public (Point From, Point To)? CutStroke { get; private set; }

    internal GraphPort? HoverPort { get; private set; }

    /// <summary>The wire under the pointer, which is the one the break mark belongs to.</summary>
    internal GraphLink? HoverLink { get; private set; }

    /// <summary>The frame whose edge is under the pointer, which is the one drawing handles.</summary>
    internal GraphFrame? HoverFrame { get; private set; }

    internal IPen PickedHalo { get; private set; } = new ImmutablePen(Brushes.Transparent, 3);

    internal IBrush PinGlowOk { get; private set; } = Brushes.Transparent;

    internal IBrush PinGlowRefused { get; private set; } = Brushes.Transparent;

    /// <summary>Whether the add node menu is up.</summary>
    public bool IsPaletteOpen => _palette is { IsVisible: true };

    /// <summary>What is being renamed, or none.</summary>
    public GraphItem? Renaming => _renaming;

    /// <summary>
    /// Opens the rename field over an item's own label. A node edits its title, a frame its
    /// name and a note its prose, since all three answer <see cref="GraphItem.Label"/>.
    /// </summary>
    public void BeginRename(GraphItem item)
    {
        if (_rename is null || item.Label is null)
        {
            return;
        }

        CommitRename();

        _renaming = item;

        var prose = item.LabelIsProse;
        var box = item.LabelBox(Metrics);
        var at = View.ToScreen(box.TopLeft);

        // The field is kept legible rather than scaled with the graph, since a person zoomed
        // out is renaming a thing they can see rather than reading it at its drawn size.
        var width = Math.Max(140, Math.Min(box.Width * View.Zoom, 360));
        var height = prose ? Math.Max(84, Math.Min(box.Height * View.Zoom, 220)) : 28;

        _rename.AcceptsReturn = prose;
        _rename.TextWrapping = prose ? Avalonia.Media.TextWrapping.Wrap : Avalonia.Media.TextWrapping.NoWrap;
        _rename.Width = width;
        _rename.Height = height;
        _rename.Text = item.Label;

        Canvas.SetLeft(_rename, Math.Max(2, Math.Min(at.X, Bounds.Width - width - 2)));
        Canvas.SetTop(_rename, Math.Max(2, Math.Min(at.Y, Bounds.Height - height - 2)));

        _rename.IsVisible = true;
        _rename.Focus();
        _rename.SelectAll();
    }

    /// <summary>Keeps what was typed and closes the field.</summary>
    public void CommitRename()
    {
        if (_renaming is not { } item || _rename is null || _ending)
        {
            return;
        }

        _ending = true;

        var typed = _rename.Text ?? string.Empty;

        // An empty name is a name nobody can see, so it is refused rather than kept and the
        // old one stands. A note is prose and is allowed to be emptied.
        if (item.LabelIsProse || typed.Trim().Length > 0)
        {
            item.Label = typed;
        }

        Close();
    }

    /// <summary>Puts the old name back and closes the field.</summary>
    public void CancelRename()
    {
        if (_renaming is null || _ending)
        {
            return;
        }

        _ending = true;
        Close();
    }

    private void Close()
    {
        _renaming = null;

        if (_rename is not null)
        {
            _rename.IsVisible = false;
        }

        _ending = false;
        Focus();
    }

    private void OnRenameKey(object? sender, KeyEventArgs e)
    {
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        switch (e.Key)
        {
            case Key.Escape:
                CancelRename();
                e.Handled = true;
                break;

            // Prose takes Enter as a line, so it is committed with the modifier instead.
            case Key.Enter when !_rename!.AcceptsReturn || command:
                CommitRename();
                e.Handled = true;
                break;
        }
    }

    // Clicking away keeps what was typed, which is what every field in the app does.
    private void OnRenameLost(object? sender, RoutedEventArgs e) => CommitRename();

    /// <summary>
    /// Opens the add node menu at a point on the canvas, wired to a pin or to nothing. With
    /// no catalogue it raises <see cref="PaletteAsked"/> and opens nothing.
    /// </summary>
    public void AskForNode(Point at, Point screen, GraphPort? from = null)
    {
        _askedAt = at;

        if (_palette is null || Catalogue.Count == 0)
        {
            PaletteAsked?.Invoke(this, new GraphPaletteEventArgs(at, screen, from));
            return;
        }

        _palette.Choices = Catalogue;
        _palette.Kinds = Kinds;
        _palette.Ports = Ports;
        _palette.Rules = Rules;
        _palette.Open(from);

        Place(screen);
        PaletteAsked?.Invoke(this, new GraphPaletteEventArgs(at, screen, from));
    }

    /// <summary>Closes the add node menu, if it is up.</summary>
    public void ClosePalette() => _palette?.Close();

    /// <summary>Puts the whole graph in view.</summary>
    public void Fit()
    {
        if (Model is not { } model)
        {
            return;
        }

        View.Fit(model.ContentBounds, Bounds.Size, Metrics.FitMargin);
    }

    /// <summary>
    /// Puts what is picked in view, or the whole graph when nothing is. A graph outgrows its
    /// window long before it outgrows a person's patience for finding one corner of it again.
    /// </summary>
    public void FitSelection()
    {
        if (Selection.Items.Count == 0)
        {
            Fit();
            return;
        }

        View.Fit(GraphBox.Around(Selection.Items.SelectMany(Corners)), Bounds.Size, Metrics.FitMargin);
    }

    private static IEnumerable<Point> Corners(GraphItem item)
    {
        yield return item.Bounds.TopLeft;
        yield return item.Bounds.BottomRight;
    }

    /// <summary>
    /// Lines the picked nodes up with the last one picked. Selection order is what says
    /// which, since that is the one a person just clicked and the one they are looking at.
    /// </summary>
    public void AlignSelection(GraphEdges edge)
    {
        if (Selection.Anchor is not { } anchor || Selection.Items.Count < 2)
        {
            return;
        }

        var box = anchor.Bounds;

        foreach (var item in Selection.Items)
        {
            if (ReferenceEquals(item, anchor))
            {
                continue;
            }

            var x = item.X;
            var y = item.Y;

            if (edge.HasFlag(GraphEdges.Left))
            {
                x = box.X;
            }
            else if (edge.HasFlag(GraphEdges.Right))
            {
                x = box.Right - item.Width;
            }

            if (edge.HasFlag(GraphEdges.Top))
            {
                y = box.Y;
            }
            else if (edge.HasFlag(GraphEdges.Bottom))
            {
                y = box.Bottom - item.Height;
            }

            item.MoveTo(x, y);
        }
    }

    /// <summary>Puts an even gap between the picked nodes, along one axis.</summary>
    public void SpreadSelection(bool across)
    {
        var picked = Selection.Items
            .OrderBy(item => across ? item.X : item.Y)
            .ToArray();

        if (picked.Length < 3)
        {
            return;
        }

        // The two on the ends stay where they are and the rest are spaced between them, so
        // the run keeps the width a person already gave it.
        var first = picked[0];
        var last = picked[^1];

        // The room left over once every node has taken its own width out of the run the two
        // on the ends already span.
        var room = across
            ? last.X + last.Width - first.X - picked.Sum(item => item.Width)
            : last.Y + last.Height - first.Y - picked.Sum(item => item.Height);

        var gap = room / (picked.Length - 1);
        var at = across ? first.X + first.Width : first.Y + first.Height;

        for (var step = 1; step < picked.Length - 1; step++)
        {
            var item = picked[step];

            at += gap;

            if (across)
            {
                item.MoveTo(Math.Round(at), item.Y);
                at += item.Width;
            }
            else
            {
                item.MoveTo(item.X, Math.Round(at));
                at += item.Height;
            }
        }
    }

    /// <summary>
    /// Moves the picked nodes so every wire between two of them runs level. The node a wire
    /// goes into is the one that moves, so a chain straightens out along the way it flows.
    /// </summary>
    public void StraightenSelection()
    {
        if (Model is not { } model || Selection.Items.Count < 2)
        {
            return;
        }

        var picked = Selection.Nodes.ToHashSet();

        foreach (var link in model.Links)
        {
            if (!picked.Contains(link.FromNode) || !picked.Contains(link.ToNode) ||
                ReferenceEquals(link.FromNode, link.ToNode))
            {
                continue;
            }

            var from = link.FromNode.PortPoint(link.From);
            var to = link.ToNode.PortPoint(link.To);

            link.ToNode.MoveTo(link.ToNode.X, Math.Round(link.ToNode.Y + (from.Y - to.Y)));
        }
    }

    /// <summary>
    /// Puts a node into a wire it was dropped across, so the wire runs through it. Dragging
    /// a node onto a link is how every node editor inserts one, and doing it by hand is
    /// three gestures: break, wire, wire.
    /// </summary>
    /// <returns>Whether it went in.</returns>
    public bool Splice(GraphNode node, GraphLink link)
    {
        if (Model is not { } model || ReferenceEquals(link.FromNode, node) || ReferenceEquals(link.ToNode, node))
        {
            return false;
        }

        var input = node.Inputs.FirstOrDefault(port => model.LinkInto(port) is null && Rules.CanJoin(link.From, port));
        var output = node.Outputs.FirstOrDefault(port => Rules.CanJoin(port, link.To));

        if (input is null || output is null)
        {
            return false;
        }

        var run = link.Reroutes.ToArray();

        model.Remove(link);
        model.Add(new GraphLink(link.From, input)).SetReroutes(run);
        model.Add(new GraphLink(output, link.To));

        return true;
    }

    /// <summary>The wire a node is lying across, or none. The first one found wins.</summary>
    public GraphLink? Crossing(GraphNode node)
    {
        if (Model is not { } model)
        {
            return null;
        }

        var box = node.Bounds;
        var fat = FatWire();

        _wiresNear.Clear();
        model.QueryLinks(box, _wiresNear);

        foreach (var link in _wiresNear)
        {
            if (ReferenceEquals(link.FromNode, node) || ReferenceEquals(link.ToNode, node) ||
                !link.Box(Metrics).Intersects(box))
            {
                continue;
            }

            // The middle of the node, since a wire clipping a corner is a wire the node
            // happens to be beside rather than one it was dropped onto.
            if (link.Route(WireStyle, Metrics).StrokeContains(fat, box.Center))
            {
                return link;
            }
        }

        return null;
    }

    /// <summary>
    /// Adds everything the picked nodes reach, following the wires one way or both. What a
    /// person asks for when they want the run a node feeds rather than the node.
    /// </summary>
    public void SelectLinked(bool downstream = true, bool upstream = true)
    {
        if (Model is not { } model)
        {
            return;
        }

        var walking = new Queue<GraphNode>(Selection.Nodes);
        var seen = walking.ToHashSet();

        while (walking.Count > 0)
        {
            var node = walking.Dequeue();

            foreach (var link in model.LinksTouching(node))
            {
                var forward = ReferenceEquals(link.FromNode, node);
                var next = forward ? link.ToNode : link.FromNode;

                // The seen set is what stops a graph with a loop in it walking for ever, so
                // it is tested whichever way the wire was followed.
                if (!(forward ? downstream : upstream) || !seen.Add(next))
                {
                    continue;
                }

                Selection.Add(next);
                walking.Enqueue(next);
            }
        }
    }

    /// <summary>Puts one item in the middle and picks it.</summary>
    public void Reveal(GraphItem item) => Reveal(item, keep: false);

    /// <summary>
    /// Brings one item into view. Keeping the selection is what the arrow keys ask for, since
    /// they have already said what is picked, and the view only moves when the item is not
    /// already on screen, so walking a row of nodes does not swing the canvas about.
    /// </summary>
    public void Reveal(GraphItem item, bool keep)
    {
        if (!keep)
        {
            Selection.Set(item);
        }

        var seen = View.Viewport(Bounds.Size).Deflate(Metrics.FitMargin);

        if (!seen.Intersects(item.Bounds) || !seen.Contains(item.Bounds.Center))
        {
            View.CentreOn(item.Bounds.Center, Bounds.Size);
        }
    }

    public void ZoomIn() => View.ZoomAt(Middle(), 1.2);

    public void ZoomOut() => View.ZoomAt(Middle(), 1 / 1.2);

    public void ZoomReset() => View.ZoomAt(Middle(), 1 / View.Zoom);

    /// <summary>Takes everything picked off the graph, wires included.</summary>
    public void DeleteSelection()
    {
        if (Model is not { } model)
        {
            return;
        }

        foreach (var link in Selection.Links.ToArray())
        {
            model.Remove(link);
        }

        foreach (var item in Selection.Items.ToArray())
        {
            switch (item)
            {
                case GraphNode node:
                    model.Remove(node);
                    break;

                case GraphFrame frame:
                    model.Remove(frame);
                    break;

                case GraphNote note:
                    model.Remove(note);
                    break;
            }
        }

        Selection.Clear();
    }

    /// <summary>
    /// Copies every picked node beside itself, along with the wires that ran between them,
    /// and picks the copies. A wire coming in from outside the selection is not copied,
    /// since a duplicate that quietly reads someone else's output is a surprise.
    /// </summary>
    public void Duplicate()
    {
        if (Model is not { } model)
        {
            return;
        }

        var picked = Selection.Nodes.ToArray();

        if (picked.Length == 0)
        {
            return;
        }

        var copies = new Dictionary<GraphNode, GraphNode>();

        foreach (var node in picked)
        {
            var copy = new GraphNode(NextId(), node.Title, node.Kind, node.Width)
            {
                IsCollapsed = node.IsCollapsed,
                IsBypassed = node.IsBypassed,
                Badge = node.Badge,
                BodyHeight = node.BodyHeight,
                FooterHeight = node.FooterHeight,
                PortLayout = node.PortLayout,
                Tag = node.Tag,
            };

            foreach (var port in node.Inputs)
            {
                copy.AddInput(port.Name, port.Type, port.Value);
            }

            foreach (var port in node.Outputs)
            {
                copy.AddOutput(port.Name, port.Type);
            }

            copy.MoveTo(node.X + Metrics.GridStep, node.Y + Metrics.GridStep);
            model.Add(copy);
            copies[node] = copy;
        }

        foreach (var link in model.Links.ToArray())
        {
            if (!copies.TryGetValue(link.FromNode, out var from) ||
                !copies.TryGetValue(link.ToNode, out var to))
            {
                continue;
            }

            var output = from.Port(PortDirection.Output, link.From.Index);
            var input = to.Port(PortDirection.Input, link.To.Index);

            if (output is not null && input is not null)
            {
                model.Add(new GraphLink(output, input)).SetReroutes(link.Reroutes);
            }
        }

        Selection.Clear();

        foreach (var copy in copies.Values)
        {
            Selection.Add(copy);
        }
    }

    /// <summary>Turns every picked node off, or back on when they are all off already.</summary>
    public void BypassSelection()
    {
        var picked = Selection.Nodes.ToArray();

        if (picked.Length == 0)
        {
            return;
        }

        var wanted = picked.Any(node => !node.IsBypassed);

        foreach (var node in picked)
        {
            node.IsBypassed = wanted;
        }
    }

    /// <summary>
    /// Moves what is picked with the arrow keys. Plain arrows walk from node to node in that
    /// direction, Shift takes the next one as well, and Control nudges what is picked instead
    /// of moving off it. Without this the canvas cannot be read at all without a pointer.
    /// </summary>
    public void Steer(Key key, KeyModifiers keys)
    {
        var by = key switch
        {
            Key.Left => new Vector(-1, 0),
            Key.Right => new Vector(1, 0),
            Key.Up => new Vector(0, -1),
            _ => new Vector(0, 1),
        };

        if (keys.HasFlag(KeyModifiers.Control) || keys.HasFlag(KeyModifiers.Meta))
        {
            Nudge(by * Metrics.SnapStep);
            return;
        }

        if (Nearest(by) is not { } wanted)
        {
            return;
        }

        if (keys.HasFlag(KeyModifiers.Shift))
        {
            Selection.Add(wanted);
        }
        else
        {
            Selection.Set(wanted);
        }

        Reveal(wanted, keep: true);
    }

    /// <summary>Moves everything picked by a step, which is what Control and an arrow does.</summary>
    public void Nudge(Vector by)
    {
        foreach (var item in Selection.Items)
        {
            item.MoveTo(item.X + by.X, item.Y + by.Y);
        }
    }

    /// <summary>
    /// The node nearest the selection in a direction. Scored the way directional navigation
    /// is anywhere: how far along the way it lies, plus a heavy penalty for how far off to
    /// the side, so a node straight ahead beats a nearer one well off the line.
    /// </summary>
    private GraphNode? Nearest(Vector way)
    {
        if (Model is not { } model)
        {
            return null;
        }

        var holding = Selection.Anchor;
        var from = holding?.Bounds.Center ?? View.Viewport(Bounds.Size).Center;

        GraphNode? best = null;
        var score = double.MaxValue;

        foreach (var node in model.Nodes)
        {
            if (ReferenceEquals(node, holding))
            {
                continue;
            }

            var away = node.Bounds.Center - from;
            var along = away.X * way.X + away.Y * way.Y;

            if (along <= 1)
            {
                continue;
            }

            var aside = Math.Abs(away.X * way.Y - away.Y * way.X);
            var reading = along + aside * 3;

            if (reading < score)
            {
                score = reading;
                best = node;
            }
        }

        return best;
    }

    /// <summary>
    /// Puts a frame around everything picked and picks the frame. The colour is cycled, so
    /// two frames made one after another do not come out the same, which is the whole reason
    /// a frame carries one.
    /// </summary>
    public GraphFrame? FrameSelection(string label = "New frame")
    {
        if (Model is not { } model || Selection.Items.Count == 0)
        {
            return null;
        }

        var known = false;
        var box = default(Rect);

        foreach (var item in Selection.Items)
        {
            box = known ? box.Union(item.Bounds) : item.Bounds;
            known = true;
        }

        if (!known)
        {
            return null;
        }

        var metrics = Metrics;
        var pad = metrics.FramePadding;
        var lead = metrics.FrameLabelHeight + metrics.FrameLabelGap;

        var frame = new GraphFrame(NextId("frame"), label, NextFrameColour())
        {
            X = box.X - pad,

            // Room above for the tab, since a frame's own label sits outside its box and
            // would otherwise land on whatever it was drawn around.
            Y = box.Y - pad - lead,
            Width = box.Width + pad * 2,
            Height = box.Height + pad * 2 + lead,
        };

        model.Add(frame);
        Selection.Set(frame);

        return frame;
    }

    /// <summary>
    /// Takes the picked frames off, leaving what stood on them. Deleting a frame does the
    /// same thing, and both are deliberate: a group is a box drawn behind a run of nodes and
    /// removing the box has never meant removing the nodes.
    /// </summary>
    public void UnframeSelection()
    {
        if (Model is not { } model)
        {
            return;
        }

        foreach (var frame in Selection.Items.OfType<GraphFrame>().ToArray())
        {
            Selection.Remove(frame);
            model.Remove(frame);
        }
    }

    private Color NextFrameColour()
    {
        var wheel = Colours.Frames;

        return wheel.Count == 0 ? AccentColour : wheel[(Model?.Frames.Count ?? 0) % wheel.Count];
    }

    /// <summary>Picks everything that has a box.</summary>
    public void SelectAll()
    {
        if (Model is not { } model)
        {
            return;
        }

        Selection.Clear();

        foreach (var item in model.Items())
        {
            Selection.Add(item);
        }
    }

    /// <summary>What is under a point in graph units. The model is tested, never the tree.</summary>
    public GraphHit HitTest(Point at)
    {
        if (Model is not { } model)
        {
            return GraphHit.Nothing;
        }

        var metrics = Metrics;
        var zoom = Math.Max(View.Zoom, 0.05);
        var reach = metrics.PinReach / zoom;

        // The wires are asked for over the widest of the three reaches, since the index
        // must never answer with less than what the tests below could match.
        var wide = Math.Max(reach, Math.Max(metrics.WireReach, metrics.RerouteRadius + 2));

        Around(model, at, reach);

        // Pins first. They sit on a node's edge and overlap whatever is beside it, so a
        // reach that lands on both has to answer with the pin.
        var pin = Nearest(at, reach);

        if (pin is not null)
        {
            return new GraphHit(GraphHitKind.Port, pin.Node, pin);
        }

        for (var layer = 2; layer >= 1; layer--)
        {
            foreach (var item in _found)
            {
                if (item.Layer != layer || !item.Bounds.Contains(at))
                {
                    continue;
                }

                if (item is GraphNote)
                {
                    return new GraphHit(GraphHitKind.Note, item);
                }

                if (item is not GraphNode node)
                {
                    continue;
                }

                if (at.Y - node.Y > metrics.HeaderHeight)
                {
                    return new GraphHit(GraphHitKind.Node, node);
                }

                return node.X + node.Width - at.X <= 24
                    ? new GraphHit(GraphHitKind.NodeCaret, node)
                    : new GraphHit(GraphHitKind.NodeHeader, node);
            }
        }

        // The wires near the point rather than every wire in the graph, so a pointer move
        // over a large one costs what is under it.
        _wiresNear.Clear();
        model.QueryLinks(new Rect(at.X - wide, at.Y - wide, wide * 2, wide * 2), _wiresNear);

        foreach (var link in _wiresNear)
        {
            for (var step = 0; step < link.Reroutes.Count; step++)
            {
                if (Near(link.Reroutes[step], at, metrics.RerouteRadius + 2))
                {
                    return new GraphHit(GraphHitKind.Reroute, null, null, link, GraphEdges.None, step);
                }
            }
        }

        var fat = FatWire();

        foreach (var link in _wiresNear)
        {
            // The route is asked for rather than read, since a node that moved threw its
            // cached path away and only a draw would have built it again.
            if (!link.Box(metrics).Contains(at))
            {
                continue;
            }

            if (link.Route(WireStyle, metrics).StrokeContains(fat, at))
            {
                return new GraphHit(GraphHitKind.Link, null, null, link);
            }
        }

        foreach (var frame in model.Frames)
        {
            if (frame.LabelBox(metrics).Contains(at))
            {
                return new GraphHit(GraphHitKind.FrameLabel, frame);
            }
        }

        // The edge answers after the wires, since a wire crossing a frame is thin and a
        // frame's border is long. Alt clicking a wire where it leaves a frame still works.
        var band = metrics.FrameEdgeReach / zoom;

        foreach (var frame in model.Frames)
        {
            var edges = Edges(frame.Bounds, at, band);

            if (edges != GraphEdges.None)
            {
                return new GraphHit(GraphHitKind.FrameEdge, frame, null, null, edges);
            }
        }

        return GraphHit.Nothing;
    }

    /// <summary>
    /// Takes off every wire a straight stroke crosses. Drawn rather than clicked, since
    /// tidying a graph usually means cutting a run of wires at once and Alt clicking each is
    /// the slow way to do it.
    /// </summary>
    /// <returns>How many were cut.</returns>
    public int Cut(Point from, Point to)
    {
        if (Model is not { } model)
        {
            return 0;
        }

        _wiresNear.Clear();
        model.QueryLinks(GraphBox.Between(from, to).Inflate(Metrics.WireBow), _wiresNear);

        var fat = FatWire();
        var cut = 0;

        foreach (var link in _wiresNear.ToArray())
        {
            if (!Crosses(link.Route(WireStyle, Metrics), fat, from, to))
            {
                continue;
            }

            model.Remove(link);
            cut++;
        }

        return cut;
    }

    /// <summary>
    /// Whether a stroke meets a wire. The stroke is walked in short steps and each one asked
    /// of the path, which is exact enough at this size and needs no curve intersection.
    /// </summary>
    private static bool Crosses(Geometry path, IPen fat, Point from, Point to)
    {
        var away = to - from;
        var length = Math.Sqrt(away.X * away.X + away.Y * away.Y);
        var steps = Math.Clamp((int)(length / 3), 1, 512);

        for (var step = 0; step <= steps; step++)
        {
            var at = from + away * ((double)step / steps);

            if (path.StrokeContains(fat, at))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Which sides of a box a point is holding, or none when it is not near one.</summary>
    public static GraphEdges Edges(Rect box, Point at, double reach)
    {
        if (!box.Inflate(reach).Contains(at))
        {
            return GraphEdges.None;
        }

        var edges = GraphEdges.None;

        if (Math.Abs(at.X - box.X) <= reach)
        {
            edges |= GraphEdges.Left;
        }
        else if (Math.Abs(at.X - box.Right) <= reach)
        {
            edges |= GraphEdges.Right;
        }

        if (Math.Abs(at.Y - box.Y) <= reach)
        {
            edges |= GraphEdges.Top;
        }
        else if (Math.Abs(at.Y - box.Bottom) <= reach)
        {
            edges |= GraphEdges.Bottom;
        }

        return edges;
    }

    /// <summary>
    /// Which length of a wire a point is on, counting from the output pin. A wire with no
    /// reroutes has one length and the answer is always zero.
    /// </summary>
    public int Length(GraphLink link, Point at)
    {
        var run = link.Run().ToArray();
        var fat = FatWire();
        var closest = 0;
        var away = double.MaxValue;

        for (var step = 0; step < run.Length - 1; step++)
        {
            var path = WireRouter.Build(run[step], run[step + 1], WireStyle, Metrics);

            if (path.StrokeContains(fat, at))
            {
                return step;
            }

            // Nothing was struck, so the nearest length takes it. A double click lands a
            // pixel or two off the line often enough that refusing is worse than guessing.
            var middle = new Point((run[step].X + run[step + 1].X) / 2, (run[step].Y + run[step + 1].Y) / 2);
            var gap = Math.Abs(middle.X - at.X) + Math.Abs(middle.Y - at.Y);

            if (gap < away)
            {
                away = gap;
                closest = step;
            }
        }

        return closest;
    }

    /// <summary>What a pin is saying while a wire is being dragged.</summary>
    public PortState PortState(GraphPort port)
    {
        if (_grab != Grab.Link || !ReferenceEquals(HoverPort, port) || _linkFrom is null)
        {
            return Controls.PortState.Rest;
        }

        return CanJoin(_linkFrom, port) ? Controls.PortState.Allowed : Controls.PortState.Refused;
    }

    /// <summary>One icon's geometry, resolved once.</summary>
    public Geometry? Glyph(IconGlyph glyph)
    {
        if (_glyphs.TryGetValue(glyph, out var found))
        {
            return found;
        }

        var key = "Icon" + glyph;

        return _glyphs[glyph] = this.TryFindResource(key, out var value) ? value as Geometry : null;
    }

    internal IPen PinPen(IBrush ink)
    {
        if (_pinPens.TryGetValue(ink, out var found))
        {
            return found;
        }

        return _pinPens[ink] = new ImmutablePen(ink.ToImmutable(), Metrics.PinRing);
    }

    internal IPen FramePen(Color colour)
    {
        if (_framePens.TryGetValue(colour, out var found))
        {
            return found;
        }

        return _framePens[colour] = new ImmutablePen(new ImmutableSolidColorBrush(colour), 1);
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new NodeGraphAutomationPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _backdrop = e.NameScope.Find<GraphBackdrop>(PartBackdrop);
        _wires = e.NameScope.Find<GraphWires>(PartWires);
        _panel = e.NameScope.Find<NodeGraphPanel>(PartNodes);
        _marquee = e.NameScope.Find<GraphMarquee>(PartMarquee);

        if (_backdrop is not null)
        {
            _backdrop.Graph = this;
        }

        if (_wires is not null)
        {
            _wires.Graph = this;
        }

        if (_panel is not null)
        {
            _panel.Graph = this;
        }

        if (_marquee is not null)
        {
            _marquee.Graph = this;
        }

        if (_palette is not null)
        {
            _palette.Chosen -= OnChosen;
            _palette.Dismissed -= OnDismissed;
            _palette.PropertyChanged -= OnPaletteChanged;
        }

        _palette = e.NameScope.Find<NodePalette>(PartPalette);

        if (_palette is not null)
        {
            _palette.Chosen += OnChosen;
            _palette.Dismissed += OnDismissed;
            _palette.PropertyChanged += OnPaletteChanged;
        }

        if (_rename is not null)
        {
            _rename.KeyDown -= OnRenameKey;
            _rename.LostFocus -= OnRenameLost;
        }

        _rename = e.NameScope.Find<TextBox>(PartRename);

        if (_rename is not null)
        {
            _rename.KeyDown += OnRenameKey;
            _rename.LostFocus += OnRenameLost;
        }

        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Reread();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ModelProperty)
        {
            if (change.OldValue is GraphModel old)
            {
                old.Changed -= OnModelChanged;
            }

            if (change.NewValue is GraphModel model)
            {
                model.Metrics = Metrics;
                model.Changed += OnModelChanged;
            }

            Selection.Clear();
            _panel?.Drop();
            Sync();
        }
        else if (change.Property == MetricsProperty)
        {
            if (Model is { } model)
            {
                model.Metrics = Metrics;
            }

            _pinPens.Clear();
            Sync();
        }
        else if (change.Property == NodeTemplateProperty || change.Property == FooterTemplateProperty)
        {
            _panel?.Drop();
            Sync();
        }
        else if (change.Property == WireStyleProperty)
        {
            foreach (var link in Model?.Links ?? [])
            {
                link.Invalidate();
            }

            Repaint();
        }
        else if (change.Property == BoundsProperty)
        {
            Sync();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            Reread();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Model is null)
        {
            return;
        }

        // A press anywhere on the canvas closes the menu and does nothing else. Trying to
        // click away is the first thing a person does to get out of one, and having it start
        // a box select behind the menu instead is what makes it feel inescapable.
        if (IsPaletteOpen)
        {
            ClosePalette();
            e.Handled = true;
            return;
        }

        Focus();

        var point = e.GetCurrentPoint(this);
        var screen = point.Position;
        var at = View.ToGraph(screen);
        var hit = HitTest(at);

        _grabScreen = screen;
        _grabGraph = at;
        _dragged = false;

        if (point.Properties.IsRightButtonPressed)
        {
            OnRightPress(hit, screen, at);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(this);
        e.Handled = true;

        // Alt is a pan everywhere except over a wire, where it is the break gesture.
        if (point.Properties.IsMiddleButtonPressed ||
            (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && hit.Kind != GraphHitKind.Link))
        {
            _grab = Grab.Pan;
            _grabOffset = View.Offset;
            Cursor = Grabbing;
            return;
        }

        // Control and a drag draws a stroke that takes off every wire it crosses. Tidying a
        // graph means cutting a run of them at once, and taking them off one at a time is
        // the slow way to do it.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && hit.Kind is GraphHitKind.None or GraphHitKind.Link)
        {
            _grab = Grab.Cut;
            CutStroke = (screen, screen);
            _marquee?.InvalidateVisual();
            return;
        }

        switch (hit.Kind)
        {
            case GraphHitKind.Port:
                StartLink(hit.Port!);
                return;

            case GraphHitKind.NodeCaret:
                hit.Node!.IsCollapsed = !hit.Node.IsCollapsed;
                Sync();
                return;

            case GraphHitKind.Node or GraphHitKind.NodeHeader or GraphHitKind.Note or GraphHitKind.FrameLabel:
                StartMove(hit.Item!, e.KeyModifiers);
                return;

            case GraphHitKind.FrameEdge:
                StartResize(hit.Frame!, hit.Edge);
                return;

            case GraphHitKind.Reroute:
                _grab = Grab.Reroute;
                _rerouteLink = hit.Link;
                _rerouteAt = hit.Index;
                _rerouteFrom = hit.Link!.Reroutes[hit.Index];
                Selection.Set(hit.Link);
                return;

            case GraphHitKind.Link when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                Model.Remove(hit.Link!);
                HoverLink = null;
                return;

            case GraphHitKind.Link:
                Selection.Set(hit.Link!);
                return;

            default:
                _grab = Grab.Marquee;
                _before = Selection.Items.ToArray();
                _kept = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? _before : [];

                if (_kept.Length == 0)
                {
                    Selection.Clear();
                }

                Marquee = new Rect(screen, screen);
                _marquee?.InvalidateVisual();
                return;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (Model is not { } model)
        {
            return;
        }

        var screen = e.GetPosition(this);
        var at = View.ToGraph(screen);

        if (_grab == Grab.None)
        {
            Hover(at);
            return;
        }

        if (!_dragged && Distance(screen, _grabScreen) > 3)
        {
            _dragged = true;
        }

        switch (_grab)
        {
            case Grab.Pan:
                View.Offset = _grabOffset + (screen - _grabScreen);
                break;

            case Grab.Move:
                Move(at);
                break;

            case Grab.Marquee:
                Band(screen, at);
                break;

            case Grab.Link:
                Hover(at);
                Live(at);
                break;

            case Grab.Reroute when _rerouteLink is not null:
                _rerouteLink.MoveReroute(_rerouteAt, _rerouteFrom + (at - _grabGraph));
                break;

            case Grab.Resize:
                Resize(at);
                break;

            case Grab.Cut:
                CutStroke = (_grabScreen, screen);
                _marquee?.InvalidateVisual();
                break;
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var was = _grab;
        var from = _linkFrom;
        var screen = e.GetPosition(this);
        var at = View.ToGraph(screen);

        // Everything the release needs is read before the capture goes, since dropping it
        // raises a capture lost and that is the same path a cancel takes.
        _grab = Grab.None;
        _linkFrom = null;
        LiveWire = null;
        Cursor = Plain;
        e.Pointer.Capture(null);

        if (was == Grab.Marquee)
        {
            Marquee = null;
            _marquee?.InvalidateVisual();
        }

        if (was == Grab.Cut)
        {
            CutStroke = null;
            _marquee?.InvalidateVisual();
            Cut(_grabGraph, at);
        }

        // A node let go across a wire goes into it. Only one, since a run of them dropped on
        // a wire has no order anybody could have meant.
        if (was == Grab.Move && _dragged && Selection.Items.Count == 1 &&
            Selection.Items[0] is GraphNode dropped && Crossing(dropped) is { } across)
        {
            Splice(dropped, across);
        }

        if (was != Grab.Link)
        {
            return;
        }

        Repaint();

        if (from is null || Model is not { } model)
        {
            return;
        }

        if (HoverPort is { } target)
        {
            // Let go over a pin that refuses it, the wire is simply dropped. The design opens
            // the menu here and it should not: a person aimed at a pin, and answering a
            // refusal with a menu they did not ask for reads as the pin having been accepted.
            if (CanJoin(from, target))
            {
                model.TryJoin(from, target, Rules);
            }

            return;
        }

        // A wire let go over nothing is an ask for a node already wired to it, which is the
        // gesture every node editor has and the one worth keeping.
        AskForNode(at, screen, from);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        View.ZoomAt(e.GetPosition(this), e.Delta.Y > 0 ? 1.1 : 1 / 1.1);
        e.Handled = true;
    }

    private void OnDoubleTap(object? sender, TappedEventArgs e)
    {
        if (Model is null)
        {
            return;
        }

        var at = View.ToGraph(e.GetPosition(this));
        var hit = HitTest(at);

        // A frame's tab has nothing else a double click could mean, so it renames.
        if (hit.Kind is GraphHitKind.FrameLabel or GraphHitKind.Note && hit.Item is { } named)
        {
            BeginRename(named);
            e.Handled = true;
            return;
        }

        if (hit.Kind == GraphHitKind.Reroute && hit.Link is { } carried)
        {
            // The inverse of the gesture that made it. A reroute added by mistake comes off
            // the same way it went on, and the rest of the run stays.
            carried.DropReroute(hit.Index);
            Selection.Set(carried);
            e.Handled = true;
            return;
        }

        if (hit.IsLink && hit.Link is { } link)
        {
            // Put in on the length of wire that was clicked rather than at the end, so a
            // second point added before a first one does not double the wire back on itself.
            link.Reroute(Length(link, at), at);
            Selection.Set(link);
            e.Handled = true;
            return;
        }

        Activated?.Invoke(this, new GraphHitEventArgs(hit, at));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        switch (e.Key)
        {
            case Key.D when command:
                Duplicate();
                e.Handled = true;
                break;

            case Key.A when command:
                SelectAll();
                e.Handled = true;
                break;

            case Key.Delete or Key.Back:
                DeleteSelection();
                e.Handled = true;
                break;

            case Key.F:
                // What is picked, or the whole graph when nothing is. Reaching one corner of
                // a graph that has outgrown its window is the thing this is for.
                FitSelection();
                e.Handled = true;
                break;

            case Key.Q:
                StraightenSelection();
                e.Handled = true;
                break;

            case Key.L when command:
                SelectLinked();
                e.Handled = true;
                break;

            case Key.Left or Key.Right or Key.Up or Key.Down:
                Steer(e.Key, e.KeyModifiers);
                e.Handled = true;
                break;

            case Key.B:
                BypassSelection();
                e.Handled = true;
                break;

            case Key.F2 when Selection.Count == 1:
                BeginRename(Selection.Items.First());
                e.Handled = true;
                break;

            case Key.G when command && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                UnframeSelection();
                e.Handled = true;
                break;

            case Key.G when command:
                FrameSelection();
                e.Handled = true;
                break;

            case Key.Tab:
                AskPalette();
                e.Handled = true;
                break;

            // Escape gives back the most recent thing first: the menu, then a drag that is
            // under way, then the selection. One key, one step back each time.
            case Key.Escape:
                if (IsPaletteOpen)
                {
                    ClosePalette();
                }
                else if (_grab != Grab.None)
                {
                    Cancel();
                }
                else
                {
                    Selection.Clear();
                }

                e.Handled = true;
                break;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        // Something took the pointer away mid drag. Left alone the gesture would still be
        // running with nothing driving it, so it is put back the way Escape puts it back.
        Cancel();
    }

    /// <summary>
    /// Abandons whatever is being dragged and puts back what it had already changed. A drag
    /// that cannot be taken back is a drag a person is afraid to start.
    /// </summary>
    public void Cancel()
    {
        var was = _grab;

        // Releasing the pointer drops the capture, which arrives here as a capture lost. With
        // nothing being dragged there is nothing to put back, and undoing a gesture that has
        // just finished properly would take its result with it.
        if (was == Grab.None)
        {
            return;
        }

        _grab = Grab.None;

        switch (was)
        {
            case Grab.Pan:
                View.Offset = _grabOffset;
                break;

            case Grab.Move:
                foreach (var (item, from) in _moveFrom)
                {
                    item.MoveTo(from.X, from.Y);
                }

                break;

            case Grab.Reroute when _rerouteLink is not null:
                _rerouteLink.MoveReroute(_rerouteAt, _rerouteFrom);
                break;

            case Grab.Resize when _resizing is not null:
                Put(_resizing, _resizedFrom);
                break;

            case Grab.Marquee:
                Marquee = null;
                _marquee?.InvalidateVisual();
                Selection.SetRun([], _before);
                break;

            case Grab.Cut:
                CutStroke = null;
                _marquee?.InvalidateVisual();
                break;

            case Grab.Link:
                Reattach();
                break;
        }

        LiveWire = null;
        _linkFrom = null;
        _rerouteLink = null;
        _resizing = null;
        Repaint();
    }

    /// <summary>Puts back a wire that was picked up off an input and never landed.</summary>
    private void Reattach()
    {
        if (Model is not { } model || _detachedFrom is null || _detachedTo is null)
        {
            return;
        }

        var back = model.Add(new GraphLink(_detachedFrom, _detachedTo));

        back.SetReroutes(_detachedRun);

        _detachedFrom = null;
        _detachedTo = null;
        _detachedRun = [];
    }

    private static bool Near(Point a, Point b, double reach) =>
        Math.Abs(a.X - b.X) <= reach && Math.Abs(a.Y - b.Y) <= reach;

    private static double Distance(Point a, Point b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private Point Middle() => new(Bounds.Width / 2, Bounds.Height / 2);

    private void AskPalette()
    {
        var screen = Middle();

        AskForNode(View.ToGraph(screen), screen);
    }

    /// <summary>
    /// Puts the menu on the canvas and keeps the whole of it inside, so it never opens with
    /// its list or its footer past an edge. The footer is what says how to leave.
    /// </summary>
    private void Place(Point screen)
    {
        _askedScreen = screen;
        Clamp();
    }

    /// <summary>
    /// Holds the menu inside the canvas. The list grows and shrinks as a query narrows it, so
    /// this runs again on every size the menu takes rather than once when it opens.
    /// </summary>
    private void Clamp()
    {
        if (_palette is not { IsVisible: true } palette)
        {
            return;
        }

        var size = palette.Bounds.Size;

        if (size.Width <= 0 || size.Height <= 0)
        {
            palette.Measure(new Size(double.PositiveInfinity, Bounds.Height));
            size = palette.DesiredSize;
        }

        const double Room = 8;

        Canvas.SetLeft(palette, Math.Max(Room, Math.Min(_askedScreen.X, Bounds.Width - size.Width - Room)));
        Canvas.SetTop(palette, Math.Max(Room, Math.Min(_askedScreen.Y, Bounds.Height - size.Height - Room)));
    }

    private void OnPaletteChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            Clamp();
        }
    }

    /// <summary>An id nothing in the model is using.</summary>
    private string NextId(string prefix = "node")
    {
        var model = Model;

        while (true)
        {
            var id = prefix + "-" + ++_made;

            if (model?.Find(id) is null)
            {
                return id;
            }
        }
    }

    /// <summary>
    /// Makes the picked node and wires it to whatever the menu was opened from. The node is
    /// placed so that pin lands where the wire was let go, which is the whole point of the
    /// gesture: a person aims at a spot and the node arrives already joined at it.
    /// </summary>
    private void OnChosen(object? sender, NodeChoiceEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }

        var node = e.Choice.Make(NextId());
        var from = _palette?.From;
        var target = Landing(node, from);

        if (target is not null)
        {
            var offset = Metrics.PortOffset(node, target);

            node.MoveTo(Math.Round(_askedAt.X - offset.X), Math.Round(_askedAt.Y - offset.Y));
        }
        else
        {
            node.MoveTo(Math.Round(_askedAt.X), Math.Round(_askedAt.Y));
        }

        model.Add(node);

        if (from is not null && target is not null)
        {
            model.TryJoin(from, target, Rules);
        }

        Selection.Set(node);
        _palette?.Close();
    }

    /// <summary>The first port on a new node that a wire off this pin could land on.</summary>
    private GraphPort? Landing(GraphNode node, GraphPort? from)
    {
        if (from is null)
        {
            return null;
        }

        var wanted = from.Direction == PortDirection.Output ? node.Inputs : node.Outputs;

        foreach (var port in wanted)
        {
            var (output, input) = from.Direction == PortDirection.Output ? (from, port) : (port, from);

            if (Rules.CanJoin(output, input))
            {
                return port;
            }
        }

        return null;
    }

    // The canvas takes the keyboard back, or the next key press would go nowhere.
    private void OnDismissed(object? sender, EventArgs e) => Focus();

    /// <summary>
    /// A right click does one thing, not two. Over nothing it asks for a node, over anything
    /// it asks for that thing's menu, and it never fires both.
    /// </summary>
    private void OnRightPress(GraphHit hit, Point screen, Point at)
    {
        if (hit.Kind == GraphHitKind.None)
        {
            AskForNode(at, screen);
            return;
        }

        if (hit.Item is { } item && !Selection.Contains(item))
        {
            Selection.Set(item);
        }

        ContextAsked?.Invoke(this, new GraphHitEventArgs(hit, at, screen));
    }

    private void StartLink(GraphPort port)
    {
        _grab = Grab.Link;

        _detachedFrom = null;
        _detachedTo = null;
        _detachedRun = [];

        // Pressing a wired input carries that wire rather than starting a second one, so a
        // link is moved by picking it up at the end a person can see. What it was is kept, so
        // letting go over nothing or pressing Escape puts it back rather than losing it.
        if (port.Direction == PortDirection.Input && Model?.LinkInto(port) is { } already)
        {
            _linkFrom = already.From;
            _detachedFrom = already.From;
            _detachedTo = already.To;
            _detachedRun = [.. already.Reroutes];
            Model.Remove(already);
        }
        else
        {
            _linkFrom = port;
        }

        Live(_grabGraph);
    }

    private void StartMove(GraphItem item, KeyModifiers keys)
    {
        if (keys.HasFlag(KeyModifiers.Shift))
        {
            Selection.Toggle(item);
        }
        else if (!Selection.Contains(item))
        {
            Selection.Set(item);
        }

        _grab = Grab.Move;
        _moveFrom.Clear();

        foreach (var picked in Selection.Items)
        {
            _moveFrom[picked] = new Point(picked.X, picked.Y);
        }

        // A frame is a box drawn behind a run of nodes, and dragging it without them would
        // leave the label pointing at nothing. So it carries whatever is standing on it, and
        // those nodes are moved without being picked, since the frame is what was grabbed.
        foreach (var frame in _moveFrom.Keys.OfType<GraphFrame>().ToArray())
        {
            Standing(frame);
        }
    }

    /// <summary>
    /// What a frame carries. Membership is where a thing is rather than anything recorded, so
    /// a node dragged into a frame belongs to it from then on with nothing to keep in step
    /// and nothing to go stale. The test is the middle of the box, which is forgiving enough
    /// that a node overhanging an edge still comes along.
    ///
    /// A smaller frame inside this one is carried too, and everything inside that one is
    /// already inside this one, so nesting needs no walk.
    /// </summary>
    private void Standing(GraphFrame frame)
    {
        if (Model is not { } model)
        {
            return;
        }

        _found.Clear();
        model.Query(frame.Bounds, _found);

        var box = frame.Bounds;
        var room = box.Width * box.Height;

        foreach (var item in _found)
        {
            if (ReferenceEquals(item, frame) || _moveFrom.ContainsKey(item) ||
                !box.Contains(item.Bounds.Center))
            {
                continue;
            }

            if (item is GraphFrame other && other.Width * other.Height >= room)
            {
                continue;
            }

            _moveFrom[item] = new Point(item.X, item.Y);
        }
    }

    private void Move(Point at)
    {
        var step = SnapToGrid ? Metrics.SnapStep : 0;
        var by = at - _grabGraph;

        foreach (var (item, from) in _moveFrom)
        {
            var x = from.X + by.X;
            var y = from.Y + by.Y;

            if (step > 0)
            {
                x = Math.Round(x / step) * step;
                y = Math.Round(y / step) * step;
            }

            item.MoveTo(x, y);
        }
    }

    private void StartResize(GraphFrame frame, GraphEdges edges)
    {
        _grab = Grab.Resize;
        _resizing = frame;
        _resizedEdges = edges;
        _resizedFrom = frame.Bounds;

        if (!Selection.Contains(frame))
        {
            Selection.Set(frame);
        }
    }

    /// <summary>
    /// Drags one side or corner of a frame. Nothing inside it moves: a frame carries what is
    /// standing on it when the frame itself is moved, and resizing is how what is standing on
    /// it is chosen.
    /// </summary>
    private void Resize(Point at)
    {
        if (_resizing is not { } frame)
        {
            return;
        }

        var by = at - _grabGraph;
        var box = _resizedFrom;
        var left = box.X;
        var top = box.Y;
        var width = box.Width;
        var height = box.Height;

        if (_resizedEdges.HasFlag(GraphEdges.Left))
        {
            left += by.X;
            width -= by.X;
        }
        else if (_resizedEdges.HasFlag(GraphEdges.Right))
        {
            width += by.X;
        }

        if (_resizedEdges.HasFlag(GraphEdges.Top))
        {
            top += by.Y;
            height -= by.Y;
        }
        else if (_resizedEdges.HasFlag(GraphEdges.Bottom))
        {
            height += by.Y;
        }

        if (SnapToGrid)
        {
            var step = Metrics.SnapStep;

            left = Math.Round(left / step) * step;
            top = Math.Round(top / step) * step;
            width = Math.Round(width / step) * step;
            height = Math.Round(height / step) * step;
        }

        // A frame never turns inside out. The edge being dragged is the one that stops, so
        // the opposite one stays where a person put it.
        var floor = Metrics.FrameSmallest;

        if (width < floor)
        {
            if (_resizedEdges.HasFlag(GraphEdges.Left))
            {
                left = box.Right - floor;
            }

            width = floor;
        }

        if (height < floor)
        {
            if (_resizedEdges.HasFlag(GraphEdges.Top))
            {
                top = box.Bottom - floor;
            }

            height = floor;
        }

        Put(frame, new Rect(left, top, width, height));
    }

    private static void Put(GraphFrame frame, Rect box)
    {
        frame.MoveTo(box.X, box.Y);
        frame.Width = box.Width;
        frame.Height = box.Height;
    }

    private void Band(Point screen, Point at)
    {
        Marquee = GraphBox.Between(_grabScreen, screen);
        _marquee?.InvalidateVisual();

        if (Model is not { } model)
        {
            return;
        }

        _found.Clear();
        model.Query(GraphBox.Between(_grabGraph, at), _found);

        _caught.Clear();

        // A frame is not picked by a band, since a band is usually drawn inside one.
        foreach (var item in _found)
        {
            if (item is not GraphFrame)
            {
                _caught.Add(item);
            }
        }

        Selection.SetRun(_caught, _kept);
    }

    private void Live(Point at)
    {
        if (_linkFrom is not { Node: { } node } from)
        {
            return;
        }

        var anchor = node.PortPoint(from);
        var target = HoverPort is { Node: { } other } hover && CanJoin(from, hover)
            ? other.PortPoint(hover)
            : at;

        var forward = from.Direction == PortDirection.Output;

        LiveWire = new LiveWire(
            forward ? anchor : target,
            forward ? target : anchor,
            Ports.Brush(from.Type),
            HoverPort is not null && CanJoin(from, HoverPort));

        Repaint();
    }

    private bool CanJoin(GraphPort from, GraphPort to)
    {
        if (from.Direction == to.Direction || from.Node is null || to.Node is null ||
            ReferenceEquals(from.Node, to.Node))
        {
            return false;
        }

        var (output, input) = from.Direction == PortDirection.Output ? (from, to) : (to, from);

        return Rules.CanJoin(output, input);
    }

    private void Hover(Point at)
    {
        if (Model is null)
        {
            return;
        }

        var hit = HitTest(at);
        var port = hit.Kind == GraphHitKind.Port ? hit.Port : null;
        var link = hit.IsLink ? hit.Link : null;
        var frame = hit.Kind == GraphHitKind.FrameEdge ? hit.Frame : null;

        Cursor = Reading(hit);

        if (ReferenceEquals(port, HoverPort) && ReferenceEquals(link, HoverLink) &&
            ReferenceEquals(frame, HoverFrame))
        {
            return;
        }

        var was = HoverPort;
        var wasFrame = HoverFrame;

        HoverPort = port;
        HoverLink = link;
        HoverFrame = frame;

        Repaint(wasFrame);
        Repaint(frame);

        // Only the two cards involved changed, so the rest are left alone. On a graph this
        // size a full repaint per pointer move is the whole budget.
        Repaint(was?.Node);
        Repaint(port?.Node);
        _wires?.InvalidateVisual();
    }

    /// <summary>
    /// What the pointer says a press would do. It is the only thing that tells a person a pin
    /// starts a wire before they have tried it.
    /// </summary>
    private static Cursor Reading(GraphHit hit) =>
        hit.Kind switch
        {
            GraphHitKind.Port => Crosshair,
            GraphHitKind.Reroute or GraphHitKind.FrameLabel => Grabbing,
            GraphHitKind.FrameEdge => Pulling(hit.Edge),
            _ => Plain,
        };

    private static Cursor Pulling(GraphEdges edges) =>
        edges switch
        {
            GraphEdges.Left | GraphEdges.Top => UpLeft,
            GraphEdges.Right | GraphEdges.Top => UpRight,
            GraphEdges.Left | GraphEdges.Bottom => DownLeft,
            GraphEdges.Right | GraphEdges.Bottom => DownRight,
            GraphEdges.Left or GraphEdges.Right => Across,
            GraphEdges.Top or GraphEdges.Bottom => Down,
            _ => Plain,
        };

    /// <summary>Fills the candidate list with everything within a reach of a point.</summary>
    private void Around(GraphModel model, Point at, double reach)
    {
        _found.Clear();
        model.Query(new Rect(at.X - reach, at.Y - reach, reach * 2, reach * 2), _found);
    }

    /// <summary>
    /// The pin closest to a point and inside the reach, or none. It reads the candidates
    /// already found rather than asking again, so a hit test is one query.
    /// </summary>
    private GraphPort? Nearest(Point at, double reach)
    {
        GraphPort? best = null;
        var closest = reach * reach;

        foreach (var item in _found)
        {
            if (item is not GraphNode node)
            {
                continue;
            }

            if (node.IsCollapsed)
            {
                Consider(node.CollapsedPort(PortDirection.Input));
                Consider(node.CollapsedPort(PortDirection.Output));
                continue;
            }

            foreach (var port in node.Inputs)
            {
                Consider(port);
            }

            foreach (var port in node.Outputs)
            {
                Consider(port);
            }

            void Consider(GraphPort? port)
            {
                if (port is null)
                {
                    return;
                }

                var pin = node.PortPoint(port);
                var away = (pin.X - at.X) * (pin.X - at.X) + (pin.Y - at.Y) * (pin.Y - at.Y);

                if (away > closest)
                {
                    return;
                }

                closest = away;
                best = port;
            }
        }

        return best;
    }

    private IPen FatWire() =>
        _fatWire ??= new ImmutablePen(Brushes.Black, Metrics.WireReach * 2);

    private void OnModelChanged(object? sender, GraphChangedEventArgs e)
    {
        GraphChanged?.Invoke(this, e);

        if (e.Change == GraphChange.Look)
        {
            Repaint();
            return;
        }

        Sync();
    }

    private void OnViewChanged()
    {
        _panel?.Sync();

        var lod = View.Lod;
        var stepped = lod != _lod;

        _lod = lod;

        // A card's own drawing does not change when the view moves, only where it is put, so
        // a pan invalidates the two drawn layers and leaves every card alone. Only crossing
        // a level of detail makes what a card draws stale.
        _backdrop?.InvalidateVisual();
        _wires?.InvalidateVisual();
        _panel?.InvalidateVisual();

        if (stepped)
        {
            Cards();
        }
    }

    private void Sync()
    {
        _panel?.Sync();
        _panel?.Reslot();
        Repaint();
    }

    private void Repaint()
    {
        _backdrop?.InvalidateVisual();
        _wires?.InvalidateVisual();
        _panel?.InvalidateVisual();
        Cards();
    }

    private void Cards() => _panel?.RedrawAll();

    /// <summary>Redraws one item's card alone, which is what a hover over a pin needs.</summary>
    private void Repaint(GraphItem? item)
    {
        if (item is not null)
        {
            _panel?.Redraw(item);
        }
    }

    /// <summary>Reads the tokens and the fonts again, which a theme change makes stale.</summary>
    private void Reread()
    {
        Colours = GraphColours.Resolve(this);

        var mono = this.TryFindResource("FontFamilyMono", out var found) && found is FontFamily family
            ? family
            : FontFamily.Default;

        Text.Describe(
            new Typeface(FontFamily),
            new Typeface(mono),
            FontSize > 0 ? FontSize : 11.5,
            FontSize > 0 ? FontSize - 0.5 : 11);

        PickedHalo = new ImmutablePen(new ImmutableSolidColorBrush(AccentColour, 0.22), 3);
        PinGlowOk = new ImmutableSolidColorBrush(Ink(Colours.Ok), 0.25);
        PinGlowRefused = new ImmutableSolidColorBrush(Ink(Colours.Error), 0.22);

        _glyphs.Clear();
        _pinPens.Clear();
        _framePens.Clear();
        _wires?.Forget();
        _backdrop?.Forget();
        _marquee?.Forget();

        // Only the ones built here are thrown away. What an app handed over is its own and
        // holds its own brushes, and taking it back would be silently losing its colours.
        if (!_kindsGiven)
        {
            _kinds = null;
        }

        if (!_portsGiven)
        {
            _ports = null;
        }

        Repaint();
    }

    /// <summary>The accent as a colour, for the few places a brush cannot be mixed.</summary>
    public Color AccentColour => Ink(Colours.Accent);

    private static Color Ink(IBrush brush) =>
        brush is ISolidColorBrush solid ? solid.Color : Colors.White;

    private enum Grab
    {
        None,
        Pan,
        Marquee,
        Move,
        Link,
        Reroute,
        Resize,
        Cut,
    }
}

/// <summary>Where a node was asked for, and the pin it should come wired to.</summary>
public sealed class GraphPaletteEventArgs(Point at, Point screen, GraphPort? from) : EventArgs
{
    /// <summary>Where the node goes, in graph units.</summary>
    public Point At { get; } = at;

    /// <summary>Where the ask came from, in canvas pixels, so an overlay can open there.</summary>
    public Point Screen { get; } = screen;

    /// <summary>The pin the new node should be wired to, or none.</summary>
    public GraphPort? From { get; } = from;
}

/// <summary>What was under a gesture.</summary>
public sealed class GraphHitEventArgs(GraphHit hit, Point at, Point screen = default) : EventArgs
{
    public GraphHit Hit { get; } = hit;

    public Point At { get; } = at;

    public Point Screen { get; } = screen;
}
