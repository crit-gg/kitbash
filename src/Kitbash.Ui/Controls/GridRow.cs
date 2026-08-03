using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One row of a grid, and the only thing a <see cref="DataGrid"/> is given. It is either
/// a row of data or the header that opens a group.
/// </summary>
public sealed class GridRow : INotifyPropertyChanged
{
    private bool expanded = true;
    private bool alternate;

    internal GridRow(object item)
    {
        Item = item;
    }

    internal GridRow(object? key, int count)
    {
        IsGroup = true;
        Key = key;
        Count = count;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The thing this row stands for, or null on a group header.</summary>
    public object? Item { get; }

    /// <summary>Whether this row opens a group rather than holding data.</summary>
    public bool IsGroup { get; }

    /// <summary>What the group is grouped by. Null on a data row.</summary>
    public object? Key { get; }

    /// <summary>How many rows the group holds, whether or not they are shown.</summary>
    public int Count { get; }

    /// <summary>
    /// Whether a group's rows are in the list under it. Set this through
    /// <see cref="GridRows"/>, which is what keeps the list and the flag agreeing.
    /// </summary>
    public bool IsExpanded
    {
        get => expanded;
        internal set => Set(ref expanded, value);
    }

    /// <summary>
    /// Whether this row takes the alternate tone. Counted over data rows alone, so a
    /// group header dropped in the middle does not flip the stripe.
    /// </summary>
    public bool IsAlternate
    {
        get => alternate;
        internal set => Set(ref alternate, value);
    }

    public override string ToString() => IsGroup ? Key?.ToString() ?? string.Empty : Item?.ToString() ?? string.Empty;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
