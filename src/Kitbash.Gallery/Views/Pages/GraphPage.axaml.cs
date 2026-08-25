using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>
/// The node graph, over both the sample the design draws and a graph far larger than any
/// design page, since the second is the only way to read the first claim the control makes.
/// </summary>
public partial class GraphPage : GalleryPage
{
    /// <summary>
    /// What the add node menu offers. The library draws and filters it, so this is the whole
    /// of what an app has to say to get the menu the design draws.
    /// </summary>
    private static readonly NodeChoice[] Catalogue =
    [
        new NodeChoice("Add", "math", "Math").Wide(168).With("A", "Float", "0").With("B", "Float", "0").Gives("Out", "Float"),
        new NodeChoice("Multiply", "math", "Math").Wide(168).With("A", "Float", "1").With("B", "Float", "1").Gives("Out", "Float"),
        new NodeChoice("Clamp", "math", "Math").With("Value", "Float").With("Min", "Float", "0").With("Max", "Float", "100").Gives("Out", "Float"),
        new NodeChoice("Lerp", "math", "Math").With("A", "Float", "0").With("B", "Float", "1").With("Alpha", "Float", "0.5").Gives("Out", "Float"),
        new NodeChoice("Round", "math", "Math").Wide(168).With("Value", "Float").Gives("Out", "Int"),
        new NodeChoice("Compare", "logic", "Logic").With("A", "Float").With("B", "Float", "0").Gives("Result", "Bool"),
        new NodeChoice("And", "logic", "Logic").Wide(160).With("A", "Bool").With("B", "Bool").Gives("Result", "Bool"),
        new NodeChoice("Select", "flow", "Flow").Wide(186).With("Condition", "Bool").With("If true", "Float").With("If false", "Float").Gives("Out", "Float"),
        new NodeChoice("Switch on enum", "flow", "Flow").Wide(190).With("Enum", "Enum").With("Default", "Float", "0").Gives("Out", "Float"),
        new NodeChoice("Attribute", "data", "Data").Wide(172).Gives("Value", "Float"),
        new NodeChoice("Constant", "data", "Data").Wide(164).With("Set", "Float", "1").Gives("Value", "Float"),
        new NodeChoice("Machine stat", "data", "Data").Wide(180).Gives("Value", "Float"),
        new NodeChoice("Quality enum", "data", "Data").Gives("Quality", "Enum"),
        new NodeChoice("Tier index", "data", "Data").Wide(168).Gives("Tier", "Int"),
        new NodeChoice("Curve sample", "curve", "Curve").Wide(184).With("T", "Float").Gives("Value", "Float"),
        new NodeChoice("Tier table", "curve", "Curve").Wide(184).With("Tier", "Int").Gives("Value", "Float"),
        new NodeChoice("Watch value", "debug", "Debug").With("Value", "Float"),
    ];

    public GraphPage()
    {
        InitializeComponent();

        Board.Kinds = Kinds();
        Board.Ports = Palette();
        Board.Catalogue = Catalogue;

        // Int reaches Float and nothing else widens, which is the one rule a value graph
        // wants and the reason the strict default is replaceable.
        Board.Rules = new DelegatePortRules((from, to) =>
            from.Type == to.Type || (from.Type == "Int" && to.Type == "Float"));

        // The library raises this and the app decides what a menu on a node says, which is
        // the seam. Everything on it is a method the canvas already offers.
        Board.ContextAsked += OnContextAsked;

        Group.Click += (_, _) =>
        {
            Board.FrameSelection();
            Board.Focus();
        };

        Ungroup.Click += (_, _) =>
        {
            Board.UnframeSelection();
            Board.Focus();
        };

        Sample.Click += (_, _) => Load(SampleGraph());
        Huge.Click += (_, _) => Load(HugeGraph());

        Orthogonal.IsCheckedChanged += (_, _) =>
            Board.WireStyle = Orthogonal.IsChecked == true ? WireStyle.Orthogonal : WireStyle.Bezier;

        Snap.IsCheckedChanged += (_, _) => Board.SnapToGrid = Snap.IsChecked == true;
        Grid.IsCheckedChanged += (_, _) => Board.ShowGrid = Grid.IsChecked == true;
        Map.IsCheckedChanged += (_, _) => MapFrame.IsVisible = Map.IsChecked == true;

        In.Click += (_, _) => Board.ZoomIn();
        Out.Click += (_, _) => Board.ZoomOut();
        Reset.Click += (_, _) => Board.ZoomReset();
        FitAll.Click += (_, _) => Board.Fit();

        Board.View.Changed += (_, _) => Read();
        Board.Selection.Changed += (_, _) => Count();

        Load(SampleGraph());
    }

    /// <summary>The sample from the design, wired the way the design wires it.</summary>
    private static GraphModel SampleGraph()
    {
        var model = new GraphModel();

        var tier = Node(model, "n1", "Tier index", "data", 168, 40, 96, [], [("Tier", "Int")]);
        var basis = Node(model, "n2", "Constant: base", "data", 170, 40, 190, [("Set", "Float", "4.5")], [("Value", "Float")]);
        var table = Node(model, "n3", "Tier table", "curve", 184, 256, 110, [("Tier", "Int", null)], [("Value", "Float")]);
        var times = Node(model, "n4", "Multiply", "math", 168, 492, 158, [("A", "Float", "1"), ("B", "Float", "1")], [("Out", "Float")]);
        var test = Node(model, "n5", "Compare", "logic", 176, 492, 306, [("A", "Float", null), ("B", "Float", "12")], [("Result", "Bool")]);
        var pick = Node(model, "n6", "Select", "flow", 186, 730, 236, [("Condition", "Bool", null), ("If true", "Float", null), ("If false", "Float", null)], [("Out", "Float")]);
        var hold = Node(model, "n7", "Clamp", "math", 176, 980, 196, [("Value", "Float", null), ("Min", "Float", "0"), ("Max", "Float", "60")], [("Out", "Float")]);
        var result = Node(model, "n8", "Output: rate", "out", 176, 1216, 212, [("Value", "Float", null)], []);
        var quality = Node(model, "n9", "Quality enum", "data", 176, 40, 306, [], [("Quality", "Enum")]);
        var watch = Node(model, "n10", "Watch value", "debug", 176, 980, 392, [("Value", "Float", null)], []);

        result.Badge = "result";
        quality.IsCollapsed = true;
        watch.IsBypassed = true;
        watch.Badge = "bypassed";

        model.Add(new GraphLink(tier.Outputs[0], table.Inputs[0]));
        model.Add(new GraphLink(table.Outputs[0], times.Inputs[0]));
        model.Add(new GraphLink(basis.Outputs[0], times.Inputs[1]));
        model.Add(new GraphLink(times.Outputs[0], test.Inputs[0]));
        model.Add(new GraphLink(times.Outputs[0], pick.Inputs[1]));
        model.Add(new GraphLink(test.Outputs[0], pick.Inputs[0]));
        model.Add(new GraphLink(pick.Outputs[0], hold.Inputs[0]));
        model.Add(new GraphLink(hold.Outputs[0], result.Inputs[0]));
        model.Add(new GraphLink(hold.Outputs[0], watch.Inputs[0])).Reroute(new Point(1150, 448));

        model.Add(new GraphFrame("f1", "Tier scaling", Color.Parse("#a78bfa")) { X = 16, Y = 62, Width = 444, Height = 206 });
        model.Add(new GraphFrame("f2", "Quality override", Color.Parse("#569eff")) { X = 470, Y = 200, Width = 462, Height = 206 });

        model.Add(new GraphNote("c1", "The tier table is authored beside the machine list, so a change there moves every graph that samples it.", "Rowan")
        {
            X = 1216, Y = 392, Width = 230, Height = 96,
        });

        return model;
    }

    /// <summary>
    /// Far more nodes than any design page draws, which is the only way to read the claim.
    /// Fifty across and forty down, each wired to the one before it.
    /// </summary>
    private static GraphModel HugeGraph()
    {
        var model = new GraphModel();
        var kinds = new[] { "math", "logic", "data", "curve", "flow", "debug" };

        for (var down = 0; down < 40; down++)
        {
            for (var across = 0; across < 50; across++)
            {
                var node = Node(
                    model,
                    $"n{down}_{across}",
                    $"Step {down}.{across}",
                    kinds[(down + across) % kinds.Length],
                    172,
                    across * 240,
                    down * 170,
                    [("A", "Float", "0"), ("B", "Float", "1")],
                    [("Out", "Float")]);

                if (across > 0)
                {
                    model.Add(new GraphLink(model.Nodes[^2].Outputs[0], node.Inputs[0]));
                }
            }
        }

        return model;
    }

    private static GraphNode Node(
        GraphModel model,
        string id,
        string title,
        string kind,
        double width,
        double x,
        double y,
        (string Name, string Type, string? Value)[] inputs,
        (string Name, string Type)[] outputs)
    {
        var node = new GraphNode(id, title, kind, width);

        node.MoveTo(x, y);

        foreach (var (name, type, value) in inputs)
        {
            node.AddInput(name, type, value);
        }

        foreach (var (name, type) in outputs)
        {
            node.AddOutput(name, type);
        }

        return model.Add(node);
    }

    private static GraphKinds Kinds() =>
        new GraphKinds(new GraphKind(IconGlyph.Cube, new SolidColorBrush(Color.Parse("#a9aeb6"))))
            .Add("data", IconGlyph.Database, new SolidColorBrush(Color.Parse("#5bc8a8")))
            .Add("math", IconGlyph.Shapes, new SolidColorBrush(Color.Parse("#6ea8e8")))
            .Add("logic", IconGlyph.GitBranch, new SolidColorBrush(Color.Parse("#d97b7b")))
            .Add("curve", IconGlyph.NetworkChart, new SolidColorBrush(Color.Parse("#a78bfa")))
            .Add("flow", IconGlyph.Workflow, new SolidColorBrush(Color.Parse("#e0a94a")))
            .Add("out", IconGlyph.ArrowToBottom, new SolidColorBrush(Color.Parse("#8fbef5")))
            .Add("debug", IconGlyph.Message, new SolidColorBrush(Color.Parse("#7b8089")));

    private static PortPalette Palette() =>
        new PortPalette([new SolidColorBrush(Color.Parse("#dde3ea"))])
            .Add("Float", new SolidColorBrush(Color.Parse("#5bc8a8")))
            .Add("Int", new SolidColorBrush(Color.Parse("#6ea8e8")))
            .Add("Bool", new SolidColorBrush(Color.Parse("#d97b7b")))
            .Add("Enum", new SolidColorBrush(Color.Parse("#a78bfa")))
            .Add("Struct", new SolidColorBrush(Color.Parse("#dde3ea")));

    private void Load(GraphModel model)
    {
        Board.ClosePalette();

        if (Board.Model is { } old)
        {
            old.Changed -= OnGraphChanged;
        }

        Board.Model = model;
        model.Changed += OnGraphChanged;

        Board.UpdateLayout();
        Board.Fit();
        Count();
        Read();
    }

    private void OnContextAsked(object? sender, GraphHitEventArgs e)
    {
        var menu = new ContextMenu();
        var many = Board.Selection.Items.Count > 1;

        if (e.Hit.Node is { } node)
        {
            menu.Items.Add(Item("Rename", () => Board.BeginRename(node), IconGlyph.Pencil));
            menu.Items.Add(Item("Duplicate", Board.Duplicate, IconGlyph.Copy));
            menu.Items.Add(Item(node.IsBypassed ? "Enable" : "Bypass", Board.BypassSelection, IconGlyph.PlayCircle));
            menu.Items.Add(Item("Select linked", () => Board.SelectLinked(), IconGlyph.Sitemap));
            menu.Items.Add(new Separator());
        }

        if (many)
        {
            menu.Items.Add(Item("Align left", () => Board.AlignSelection(GraphEdges.Left), IconGlyph.Columns));
            menu.Items.Add(Item("Align top", () => Board.AlignSelection(GraphEdges.Top), IconGlyph.Columns));
            menu.Items.Add(Item("Space across", () => Board.SpreadSelection(true), IconGlyph.Columns));
            menu.Items.Add(Item("Space down", () => Board.SpreadSelection(false), IconGlyph.Columns));
            menu.Items.Add(Item("Straighten wires", Board.StraightenSelection, IconGlyph.GitCommit));
            menu.Items.Add(Item("Group in a frame", () => Board.FrameSelection(), IconGlyph.GroupAlt));
            menu.Items.Add(new Separator());
        }

        if (e.Hit.Frame is not null)
        {
            menu.Items.Add(Item("Rename frame", () => Board.BeginRename(e.Hit.Frame), IconGlyph.Pencil));
            menu.Items.Add(Item("Ungroup", Board.UnframeSelection, IconGlyph.GroupAlt));
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(Item("Delete", Board.DeleteSelection, IconGlyph.Trash));

        if (menu.Items.Count == 0)
        {
            return;
        }

        menu.PlacementTarget = Board;
        menu.Placement = PlacementMode.Pointer;
        menu.Open(Board);
    }

    private static MenuItem Item(string words, Action run, IconGlyph glyph)
    {
        var item = new MenuItem { Header = words, Icon = new Icon { Glyph = glyph, Size = 14 } };

        item.Click += (_, _) => run();
        return item;
    }

    private void OnGraphChanged(object? sender, GraphChangedEventArgs e) => Count();

    private void Count()
    {
        var model = Board.Model;

        Counts.Text = model is null
            ? "no graph"
            : $"{model.Nodes.Count} nodes, {model.Links.Count} links, {Board.Selection.Count} picked";
    }

    private void Read()
    {
        Zoom.Text = $"{Math.Round(Board.View.Zoom * 100)}%";
        Realised.Text = $"{Board.RealisedCount} drawn, {Board.View.Lod.ToString().ToLowerInvariant()}";
    }
}
