using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>What a family of node looks like. The app owns the mapping, since only it knows
/// what a Math node is.</summary>
public sealed record GraphKind(IconGlyph? Icon, IBrush Ink);

/// <summary>
/// Kind name to icon and colour. A kind nothing was said about falls back to the plain one,
/// so a graph draws before an app has described anything in it.
/// </summary>
public sealed class GraphKinds
{
    private readonly Dictionary<string, GraphKind> _kinds = new(StringComparer.Ordinal);

    public GraphKinds(GraphKind fallback)
    {
        Fallback = fallback;
    }

    public GraphKind Fallback { get; set; }

    public GraphKind this[string kind] => _kinds.GetValueOrDefault(kind) ?? Fallback;

    public GraphKinds Add(string kind, IconGlyph? icon, IBrush ink)
    {
        _kinds[kind] = new GraphKind(icon, ink);
        return this;
    }
}
