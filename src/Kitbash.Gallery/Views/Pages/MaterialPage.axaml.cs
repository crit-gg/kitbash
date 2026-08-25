using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Kitbash.Gallery.Views.Textures;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>
/// The same canvas as a material graph, from the Rime design. It is the second use the node
/// graph was built against and the one that decided the seam: the library owns the canvas,
/// the wires and the pins, and a node's body is whatever the app puts there.
///
/// Every preview is cooked for real, so this is also the worked example of the other half of
/// that seam. The graph evaluates nothing, the app walks the model and does.
/// </summary>
public partial class MaterialPage : GalleryPage
{
    private const double Header = 28;

    /// <summary>The three node sizes the design offers, and the body follows the width.</summary>
    private static readonly double[] Widths = [104, 138, 184];

    private readonly GraphModel _model = new();
    private readonly TextureCook _cook;
    private readonly List<TexturePane> _panes = [];
    private readonly List<TextureCost> _strips = [];
    private readonly Dictionary<GraphFrame, GraphNode[]> _holds = [];

    private double _width = Widths[1];

    public MaterialPage()
    {
        InitializeComponent();

        _cook = new TextureCook(_model);

        Build();

        Board.Kinds = Kinds();
        Board.Ports = Ports();
        Board.Model = _model;
        Board.NodeTemplate = new FuncDataTemplate<GraphNode>((node, _) => Body(node));
        Board.FooterTemplate = new FuncDataTemplate<GraphNode>((_, _) => CostStrip());

        Preview.Cook = _cook;

        _model.Changed += (_, e) =>
        {
            // Only a change to what is there can change what anything cooks. Moving a node
            // raises a box change and is left alone.
            if (e.Change == GraphChange.Set)
            {
                Recook();
            }
        };

        Board.Selection.Changed += (_, _) => Chose();
        Board.View.Changed += (_, _) => Zoom.Text = $"{Math.Round(Board.View.Zoom * 100)}%";

        Sizes0.IsCheckedChanged += (_, _) => Resize(0);
        Sizes1.IsCheckedChanged += (_, _) => Resize(1);
        Sizes2.IsCheckedChanged += (_, _) => Resize(2);

        Timings.IsCheckedChanged += (_, _) =>
        {
            foreach (var strip in _strips)
            {
                strip.ShowsTimings = Timings.IsChecked == true;
                strip.InvalidateVisual();
            }
        };

        Strip.IsCheckedChanged += (_, _) =>
        {
            foreach (var node in _model.Nodes.Where(node => node.Kind != "out"))
            {
                node.FooterHeight = Strip.IsChecked == true ? 20 : 0;
            }

            foreach (var frame in _model.Frames)
            {
                Fit(frame);
            }
        };

        In.Click += (_, _) => Board.ZoomIn();
        Out.Click += (_, _) => Board.ZoomOut();
        Reset.Click += (_, _) => Board.ZoomReset();
        FitAll.Click += (_, _) => Board.Fit();

        Recook();
        Chose();
    }

    private RadioButton Sizes0 => (RadioButton)Sizes.Items[0]!;

    private RadioButton Sizes1 => (RadioButton)Sizes.Items[1]!;

    private RadioButton Sizes2 => (RadioButton)Sizes.Items[2]!;

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        Board.UpdateLayout();
        Board.Fit();

        // Something is picked from the start, so the panel opens with a preview and a set of
        // sliders rather than with an empty box. After the load, since the rows it builds
        // read brushes off the tree.
        if (Board.Selection.Count == 0)
        {
            Board.Selection.Set(_model.Nodes[0]);
        }
    }

    private Control Body(GraphNode? node)
    {
        var pane = new TexturePane
        {
            Cook = _cook,
            IsCompact = node?.Kind == "out",
        };

        _panes.Add(pane);
        return pane;
    }

    private Control CostStrip()
    {
        var strip = new TextureCost { Cook = _cook, ShowsTimings = Timings.IsChecked == true };

        _strips.Add(strip);
        return strip;
    }

    private static GraphKinds Kinds() =>
        new GraphKinds(new GraphKind(IconGlyph.Cube, new SolidColorBrush(Color.Parse("#a9aeb6"))))
            .Add("gen", IconGlyph.Shapes, new SolidColorBrush(Color.Parse("#5bc8a8")))
            .Add("adj", IconGlyph.Filter, new SolidColorBrush(Color.Parse("#6ea8e8")))
            .Add("flt", IconGlyph.RefreshCw, new SolidColorBrush(Color.Parse("#e0a94a")))
            .Add("mesh", IconGlyph.Cube, new SolidColorBrush(Color.Parse("#a78bfa")))
            .Add("out", IconGlyph.ArrowToBottom, new SolidColorBrush(Color.Parse("#569eff")));

    private static PortPalette Ports() =>
        new PortPalette([new SolidColorBrush(Color.Parse("#dde3ea"))])
            .Add("Gray", new SolidColorBrush(Color.Parse("#5bc8a8")))
            .Add("Color", new SolidColorBrush(Color.Parse("#dde3ea")))
            .Add("Normal", new SolidColorBrush(Color.Parse("#a78bfa")));

    /// <summary>The rusted plate graph the design draws, wired the way it wires it.</summary>
    private void Build()
    {
        var tile = Node("tile", "Tile Generator", "gen", 40, 68, TextureOp.Tile, [], [("Output", "Gray")]);
        var noise = Node("noise", "Perlin Noise", "gen", 40, 300, TextureOp.Perlin, [], [("Output", "Gray")]);
        var warp = Node("warp", "Directional Warp", "flt", 230, 300, TextureOp.Warp, [("Input", "Gray"), ("Intensity", "Gray")], [("Output", "Gray")]);
        var height = Node("height", "Blend", "adj", 420, 68, TextureOp.Blend, [("Foreground", "Gray"), ("Background", "Gray"), ("Mask", "Gray")], [("Output", "Gray")]);
        var rust = Node("rust", "Levels", "adj", 420, 300, TextureOp.Levels, [("Input", "Gray")], [("Output", "Gray")]);
        var normal = Node("normal", "Normal", "mesh", 640, 44, TextureOp.Normal, [("Height", "Gray")], [("Output", "Normal")]);
        var ao = Node("ao", "Ambient Occlusion", "mesh", 640, 260, TextureOp.Occlusion, [("Height", "Gray")], [("Output", "Gray")]);
        var grad = Node("grad", "Gradient Map", "adj", 640, 476, TextureOp.GradientMap, [("Input", "Gray")], [("Output", "Color")]);
        var rough = Node("rough", "Histogram Scan", "adj", 860, 260, TextureOp.HistogramScan, [("Input", "Gray")], [("Output", "Gray")]);
        var metal = Node("metal", "Invert", "adj", 860, 476, TextureOp.Invert, [("Input", "Gray")], [("Output", "Gray")]);

        Own(tile, TextureOp.Tile)
            .With("across", 4).With("down", 4).With("bevel", 0.09).With("gap", 0.03).With("rivets", 1).With("seed", 7)
            .Knob("X amount", "across", 2, 8, true, "Plates across the tile")
            .Knob("Y amount", "down", 2, 8, true, "Plates down the tile")
            .Knob("Bevel", "bevel", 0.02, 0.2, false, "Width of the rounded plate edge")
            .Knob("Gap", "gap", 0.005, 0.09, false, "Seam between plates")
            .Knob("Rivets", "rivets", 0, 1, true, "Adds four rivet domes to every plate")
            .Knob("Seed", "seed", 1, 40, true, "Reshuffles the per plate variation")
            .Note = "gen";

        Own(noise, TextureOp.Perlin)
            .With("scale", 6).With("seed", 3)
            .Knob("Scale", "scale", 2, 16, true, "Lattice cells across the tile")
            .Knob("Seed", "seed", 1, 40, true, "Reshuffles the lattice")
            .Note = "4 oct";

        Own(warp, TextureOp.Warp)
            .With("strength", 0.5)
            .Knob("Intensity", "strength", 0, 1, false, "How far the noise is pushed along the gradient")
            .Note = "wrap";

        Own(height, TextureOp.Blend).Note = "add sub";

        Own(rust, TextureOp.Levels)
            .With("coverage", 0.55).With("contrast", 0.5)
            .Knob("Coverage", "coverage", 0, 1, false, "How much of the plate the rust has taken")
            .Knob("Contrast", "contrast", 0, 0.98, false, "Hardness of the edge between paint and rust")
            .Note = "mask";

        Own(normal, TextureOp.Normal)
            .With("strength", 2.2)
            .Knob("Intensity", "strength", 0.4, 5, false, "Slope multiplier before the vectors are normalised")
            .Note = "OpenGL";

        Own(ao, TextureOp.Occlusion).Note = "r 4 px";

        var paint = Own(grad, TextureOp.GradientMap);

        paint.Paint = Color.Parse("#3d5a80");
        paint.Note = "sRGB";

        Own(rough, TextureOp.HistogramScan)
            .With("position", 0.44).With("contrast", 0.3)
            .Knob("Position", "position", 0, 1, false, "Where the window sits on the histogram")
            .Knob("Contrast", "contrast", 0, 0.95, false, "How wide the window is")
            .Note = "linear";

        Own(metal, TextureOp.Invert).Note = "clamp";

        Join(tile, 0, height, 0);
        Join(noise, 0, height, 1);
        Join(rust, 0, height, 2);
        Join(noise, 0, warp, 0);
        Join(tile, 0, warp, 1);
        Join(warp, 0, rust, 0);
        Join(height, 0, normal, 0);
        Join(height, 0, ao, 0);
        Join(rust, 0, grad, 0);
        Join(rust, 0, rough, 0);
        Join(rust, 0, metal, 0);

        Outputs(grad, normal, rough, metal, height, ao);

        Frame("f1", "Plate and rust", "#5bc8a8", tile, noise, warp, height, rust);
        Frame("f2", "Channel synthesis", "#a78bfa", normal, ao, grad, rough, metal);
        Frame("f3", "Material outputs", "#569eff", [.. _model.Nodes.Where(n => n.Kind == "out")]);

        _model.Add(new GraphNote("c1", "Rust drives four channels at once. Widen the coverage and the whole material ages together.", "Rowan")
        {
            X = 40,
            Y = 540,
            Width = 250,
            Height = 88,
        });
    }

    /// <summary>
    /// A frame drawn round a run of nodes, and kept round them. The nodes it holds are
    /// remembered, so switching the node size grows the box rather than leaving it cutting
    /// through what it was drawn around.
    /// </summary>
    private void Frame(string id, string label, string colour, params GraphNode[] held)
    {
        var frame = new GraphFrame(id, label, Color.Parse(colour));

        _model.Add(frame);
        _holds[frame] = held;
        Fit(frame);
    }

    private void Fit(GraphFrame frame)
    {
        if (!_holds.TryGetValue(frame, out var held) || held.Length == 0)
        {
            return;
        }

        var pad = Board.Metrics.FramePadding;
        var lead = Board.Metrics.FrameLabelHeight + Board.Metrics.FrameLabelGap;

        var left = held.Min(node => node.X);
        var top = held.Min(node => node.Y);

        frame.MoveTo(left - pad, top - pad - lead);
        frame.Width = held.Max(node => node.X + node.Width) - left + pad * 2;
        frame.Height = held.Max(node => node.Y + node.Height) - top + pad * 2 + lead;
    }

    private void Outputs(params GraphNode[] from)
    {
        (string Title, string Slot, string Format, string Type)[] slots =
        [
            ("Base Color", "baseColor", "sRGB 8 bit", "Color"),
            ("Normal", "normal", "linear 16 bit", "Normal"),
            ("Roughness", "roughness", "linear 8 bit", "Gray"),
            ("Metallic", "metallic", "linear 8 bit", "Gray"),
            ("Height", "height", "linear 16 bit", "Gray"),
            ("Ambient Occlusion", "ambientOcclusion", "linear 8 bit", "Gray"),
        ];

        for (var step = 0; step < slots.Length; step++)
        {
            var (title, slot, format, type) = slots[step];
            var node = new GraphNode("o_" + slot, title, "out", 190)
            {
                BodyHeight = 46,
                PortLayout = PortLayout.Edge,
            };

            node.MoveTo(1052, 40 + step * 80);
            node.Add(Pin("Input", type, PortDirection.Input));

            var own = new TextureNode(TextureOp.Output) { Slot = slot, Format = format };

            node.Tag = own;
            _model.Add(node);
            _model.Add(new GraphLink(from[step].Outputs[0], node.Inputs[0]));
        }
    }

    private GraphNode Node(
        string id,
        string title,
        string kind,
        double x,
        double y,
        TextureOp op,
        (string Name, string Type)[] ins,
        (string Name, string Type)[] outs)
    {
        var node = new GraphNode(id, title, kind, _width)
        {
            BodyHeight = _width - 16,
            FooterHeight = 20,
            PortLayout = PortLayout.Edge,
        };

        node.MoveTo(x, y);

        foreach (var (name, type) in ins)
        {
            node.Add(Pin(name, type, PortDirection.Input));
        }

        foreach (var (name, type) in outs)
        {
            node.Add(Pin(name, type, PortDirection.Output));
        }

        node.Tag = new TextureNode(op);
        return _model.Add(node);
    }

    /// <summary>
    /// A pin, shaped by what it carries. Colour says the type and so does the shape, which is
    /// what anyone who cannot tell two colours apart has to read instead.
    /// </summary>
    private static GraphPort Pin(string name, string type, PortDirection direction) =>
        new(name, type, direction)
        {
            Shape = type switch
            {
                "Normal" => PortShape.Diamond,
                "Color" => PortShape.Square,
                _ => PortShape.Round,
            },
        };

    private static TextureNode Own(GraphNode node, TextureOp op)
    {
        var own = new TextureNode(op);

        node.Tag = own;
        return own;
    }

    private void Join(GraphNode from, int output, GraphNode to, int input) =>
        _model.Add(new GraphLink(from.Outputs[output], to.Inputs[input]));

    private void Resize(int which)
    {
        var wanted = Widths[which];

        if (Math.Abs(_width - wanted) < 0.5)
        {
            return;
        }

        _width = wanted;

        foreach (var node in _model.Nodes)
        {
            if (node.Kind == "out")
            {
                continue;
            }

            node.Width = wanted;
            node.BodyHeight = wanted - 16;
        }

        foreach (var frame in _model.Frames)
        {
            Fit(frame);
        }
    }

    private void Recook()
    {
        _cook.CookAll();

        Cost.Text = $"{_cook.Took:0.0} ms";
        State.Text = "up to date";
    }

    /// <summary>What the panel shows, which is whatever one node is picked.</summary>
    private void Chose()
    {
        Knobs.Children.Clear();

        var node = Board.Selection.Nodes.FirstOrDefault();

        Preview.DataContext = node;
        Preview.IsCompact = false;
        Preview.InvalidateVisual();

        if (node is null)
        {
            Chosen.Text = "Nothing picked";
            ChosenKind.Text = string.Empty;
            NoKnobs.IsVisible = false;
            return;
        }

        Chosen.Text = node.Title;
        ChosenKind.Text = node.Kind;

        if (node.Tag is not TextureNode own || own.Knobs.Count == 0)
        {
            NoKnobs.IsVisible = true;
            return;
        }

        NoKnobs.IsVisible = false;

        foreach (var knob in own.Knobs)
        {
            Knobs.Children.Add(Row(own, knob));
        }
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, out var found) && found is IBrush brush ? brush : Brushes.Gray;

    /// <summary>One parameter: a label, a slider and what it reads. Moving it cooks the graph.</summary>
    private Control Row(TextureNode own, TextureKnob knob)
    {
        var readout = new TextBlock
        {
            FontFamily = this.TryFindResource("FontFamilyMono", out var mono) && mono is FontFamily family
                ? family
                : FontFamily.Default,
            FontSize = 11,
            MinWidth = 44,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("InkSecondary"),
        };

        var slider = new Slider
        {
            Minimum = knob.Least,
            Maximum = knob.Most,
            Value = own.Value(knob.Key, knob.Least),
            TickFrequency = knob.Whole ? 1 : 0,
            IsSnapToTickEnabled = knob.Whole,
        };

        void Show() =>
            readout.Text = knob.Whole
                ? ((int)Math.Round(slider.Value)).ToString()
                : slider.Value.ToString("0.00");

        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty)
            {
                return;
            }

            own.Values[knob.Key] = knob.Whole ? Math.Round(slider.Value) : slider.Value;
            Show();
            Recook();
        };

        Show();

        var head = new DockPanel();

        head.Children.Add(readout);
        DockPanel.SetDock(readout, Avalonia.Controls.Dock.Right);
        head.Children.Add(new TextBlock
        {
            Text = knob.Label,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("InkMuted"),
        });

        var stack = new StackPanel { Spacing = 2 };

        stack.Children.Add(head);
        stack.Children.Add(slider);

        if (knob.Tip.Length > 0)
        {
            ToolTip.SetTip(stack, knob.Tip);
        }

        return stack;
    }
}
