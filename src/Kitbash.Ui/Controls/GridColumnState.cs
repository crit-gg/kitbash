namespace Kitbash.Ui.Controls;

/// <summary>
/// What a person changed about one column. Only what differs from the column as it was
/// declared is kept, so a grid whose declaration moves is not held to an old layout and a
/// column nobody touched takes whatever the tool now says.
/// </summary>
/// <param name="Key">The column's own name, which is what ties this to it across runs.</param>
/// <param name="Width">How wide it was left, or null when it was never resized.</param>
/// <param name="IsVisible">Whether it was taken off, or null when it was left alone.</param>
/// <param name="IsPinned">Whether it was pinned, or null when it was left alone.</param>
/// <param name="Order">Where it was moved to, or null when it was never moved.</param>
public sealed record GridColumnState(
    string Key,
    double? Width = null,
    bool? IsVisible = null,
    bool? IsPinned = null,
    int? Order = null)
{
    /// <summary>Whether this says anything at all, so nothing is written for a column left alone.</summary>
    public bool Matters => Width is not null || IsVisible is not null || IsPinned is not null || Order is not null;
}
