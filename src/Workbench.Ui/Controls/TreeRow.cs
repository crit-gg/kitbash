using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Workbench.Ui.Controls;

/// <summary>
/// One visible row of a tree, and the only thing a <see cref="Tree"/> is given.
/// </summary>
/// <remarks>
/// Depth is a value here rather than a nesting level in the tree of controls, which is
/// what lets a tree virtualise. <see cref="TreeRows"/> makes these and is the only thing
/// that may change one.
/// <para>
/// <see cref="Item"/> is what a view binds to. A row is what is selected, so a
/// <c>SelectedItem</c> comes back as one of these and the model is a step inside it.
/// </para>
/// </remarks>
public sealed class TreeRow : INotifyPropertyChanged
{
    private bool expanded;
    private bool hasChildren;

    internal TreeRow(object item, int level, bool hasChildren)
    {
        Item = item;
        Level = level;
        this.hasChildren = hasChildren;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The thing this row stands for.</summary>
    public object Item { get; }

    /// <summary>How deep it sits. A root is zero.</summary>
    public int Level { get; }

    /// <summary>Whether it draws a caret. A row with no children is a leaf and draws none.</summary>
    public bool HasChildren
    {
        get => hasChildren;
        internal set => Set(ref hasChildren, value);
    }

    /// <summary>
    /// Whether its children are in the list below it. Set this through
    /// <see cref="TreeRows"/>, which is what keeps the list and the flag agreeing.
    /// </summary>
    public bool IsExpanded
    {
        get => expanded;
        internal set => Set(ref expanded, value);
    }

    public override string ToString() => Item.ToString() ?? string.Empty;

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
