using System.Diagnostics;
using Avalonia.Media.Imaging;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Textures;

/// <summary>What one node came out as.</summary>
public sealed record TextureBake(TextureMap Map, WriteableBitmap Bitmap, double Milliseconds);

/// <summary>
/// Walks the graph and works out what every node produces. The library evaluates nothing, so
/// this is the whole of the other half: it reads the model, follows the wires and keeps the
/// answers, and it runs again whenever the set of nodes or wires changes.
/// </summary>
public sealed class TextureCook(GraphModel model)
{
    private readonly Dictionary<GraphNode, TextureBake> _baked = [];
    private readonly HashSet<GraphNode> _walking = [];

    /// <summary>Raised when everything has been worked out again.</summary>
    public event EventHandler? Changed;

    /// <summary>How long the whole graph took, in milliseconds.</summary>
    public double Took { get; private set; }

    public TextureBake? Bake(GraphNode node) => _baked.GetValueOrDefault(node);

    /// <summary>
    /// Cooks every node. A graph this size is a fraction of a millisecond, so there is no
    /// dirty tracking: a parameter moving costs the whole thing and nobody can tell.
    /// </summary>
    public void CookAll()
    {
        var clock = Stopwatch.StartNew();

        _baked.Clear();
        _walking.Clear();

        foreach (var node in model.Nodes)
        {
            Cook(node);
        }

        clock.Stop();
        Took = clock.Elapsed.TotalMilliseconds;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private TextureMap Cook(GraphNode node)
    {
        if (_baked.TryGetValue(node, out var already))
        {
            return already.Map;
        }

        // A graph with a loop in it would otherwise never come back. The node that closes the
        // loop is handed a flat map and the cook carries on.
        if (!_walking.Add(node))
        {
            return TextureMap.Flat(0.5f);
        }

        var clock = Stopwatch.StartNew();
        var passes = node.IsBypassed || (node.Tag as TextureNode)?.Op == TextureOp.Output;
        var map = passes ? Feed(node, 0) : Run(node);

        clock.Stop();
        _walking.Remove(node);

        // A node that passes its input straight through shows the picture that came in, so it
        // shows that one rather than making a second copy of it.
        var shown = passes && Upstream(node) is { } came ? came.Bitmap : map.ToBitmap();

        _baked[node] = new TextureBake(map, shown, clock.Elapsed.TotalMilliseconds);

        return map;
    }

    /// <summary>What is already baked on the other end of this node's first input.</summary>
    private TextureBake? Upstream(GraphNode node) =>
        node.Port(PortDirection.Input, 0) is { } port && model.LinkInto(port) is { } link
            ? _baked.GetValueOrDefault(link.FromNode)
            : null;

    private TextureMap Run(GraphNode node)
    {
        var own = node.Tag as TextureNode;

        return own?.Op switch
        {
            TextureOp.Tile => TextureMaths.Tile(
                (int)own.Value("across", 4),
                (int)own.Value("down", 4),
                own.Value("bevel", 0.09),
                own.Value("gap", 0.03),
                own.Value("rivets", 1) > 0.5,
                (int)own.Value("seed", 7)),

            TextureOp.Perlin => TextureMaths.Perlin((int)own.Value("scale", 6), (int)own.Value("seed", 3)),

            TextureOp.Warp => TextureMaths.Warp(Feed(node, 0), Feed(node, 1), own.Value("strength", 0.5)),

            TextureOp.Levels => TextureMaths.Levels(Feed(node, 0), own.Value("coverage", 0.5), own.Value("contrast", 0.5)),

            TextureOp.Blend => TextureMaths.Blend(Feed(node, 0), Feed(node, 1), Feed(node, 2, 0.5f)),

            TextureOp.Normal => TextureMaths.Normal(Feed(node, 0), own.Value("strength", 2.2)),

            TextureOp.Occlusion => TextureMaths.Occlusion(Feed(node, 0)),

            TextureOp.GradientMap => TextureMaths.GradientMap(Feed(node, 0), own.Paint),

            TextureOp.HistogramScan => TextureMaths.HistogramScan(
                Feed(node, 0),
                own.Value("position", 0.44),
                own.Value("contrast", 0.3)),

            TextureOp.Invert => TextureMaths.Invert(Feed(node, 0)),

            _ => Feed(node, 0),
        };
    }

    /// <summary>
    /// What is wired into an input, or a flat map when nothing is. An unwired input is a
    /// perfectly ordinary state while a graph is being built, so it is a value rather than a
    /// hole and the rest of the graph keeps cooking.
    /// </summary>
    private TextureMap Feed(GraphNode node, int index, float missing = 0f)
    {
        if (node.Port(PortDirection.Input, index) is not { } port ||
            model.LinkInto(port) is not { } link)
        {
            return TextureMap.Flat(missing);
        }

        return Cook(link.FromNode);
    }
}
