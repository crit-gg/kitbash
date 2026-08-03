namespace Workbench.Gallery.Views;

/// <summary>
/// What the gallery's tree grid is made of. Every cell is already written out, because a
/// branch row shows what its children add up to and a leaf shows its own value, and the
/// grid is not the thing that decides which.
/// </summary>
public sealed class Aggregate(
    string name,
    string kind,
    string value,
    string tier,
    string state,
    IReadOnlyList<Aggregate>? children = null)
{
    public string Name { get; } = name;

    public string Kind { get; } = kind;

    public string Value { get; } = value;

    public string Tier { get; } = tier;

    public string State { get; } = state;

    public IReadOnlyList<Aggregate> Children { get; } = children ?? [];

    public override string ToString() => Name;
}
