namespace Kitbash.Gallery.Views;

/// <summary>
/// What the gallery's trees are made of. Structure, not content: a name, a number beside
/// it and whatever hangs under it, which is everything a row has to draw.
/// </summary>
public sealed class Node(string name, string? count = null, IReadOnlyList<Node>? children = null)
{
    public string Name { get; } = name;

    public string? Count { get; } = count;

    public IReadOnlyList<Node> Children { get; } = children ?? [];

    public override string ToString() => Name;
}
