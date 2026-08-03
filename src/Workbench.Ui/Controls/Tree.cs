using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Workbench.Ui.Controls;

/// <summary>
/// A tree that virtualises, built on the list that already does.
/// </summary>
public class Tree : ListBox
{
    public Tree()
    {
        AddHandler(DoubleTappedEvent, OnDoubleTapped);
    }

    private TreeRows? Rows => ItemsSource as TreeRows;

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<TreeItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new TreeItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);

        if (container is TreeItem row)
        {
            row.Follow(item as TreeRow);
        }
    }

    /// <summary>
    /// A container kept but moved. Everything it was told is read again from whatever row
    /// now sits at its index, since this is the one place a recycled container is reused
    /// without being cleared first.
    /// </summary>
    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);

        if (container is TreeItem row)
        {
            row.Follow(newIndex >= 0 && newIndex < ItemsView.Count ? ItemsView[newIndex] as TreeRow : null);
        }
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);

        if (container is TreeItem row)
        {
            row.Follow(null);
        }
    }

    /// <summary>Opens a row that is closed, closes one that is open.</summary>
    internal void Toggle(TreeItem container)
    {
        if (container.Row is { } row)
        {
            Rows?.Toggle(row);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && Rows is { } rows && SelectedItem is TreeRow row)
        {
            switch (e.Key)
            {
                // Right opens a closed row and steps into an open one, which is what
                // every tree does and what a person arrowing through one expects.
                case Key.Right when row.HasChildren && !row.IsExpanded:
                    rows.Expand(row);
                    e.Handled = true;
                    break;

                case Key.Right when row.IsExpanded:
                    Select(SelectedIndex + 1);
                    e.Handled = true;
                    break;

                case Key.Left when row.IsExpanded:
                    rows.Collapse(row);
                    e.Handled = true;
                    break;

                // Left on a row that is already closed goes out to the one it hangs
                // under, so held down it walks back up to the root.
                case Key.Left when rows.ParentOf(row) is { } parent:
                    Select(rows.IndexOf(parent));
                    e.Handled = true;
                    break;
            }
        }

        // Do not call base once this has answered the key. It restores the selection to
        // the focused row by the time the press returns, which undoes left and right.
        if (e.Handled)
        {
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Picks a row and puts focus on it, since the two travel together when a key moves
    /// the selection. Focus arrives as though arrowed to, which is what it was.
    /// </summary>
    private void Select(int index)
    {
        if (index < 0 || index >= ItemCount)
        {
            return;
        }

        SelectedIndex = index;
        UpdateLayout();
        ContainerFromIndex(index)?.Focus(NavigationMethod.Directional);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source
            || source.FindAncestorOfType<TreeItem>(includeSelf: true) is not { } container)
        {
            return;
        }

        // The strip in front of the name answers a single click and nothing else. A double
        // click there is two toggles and then this one, which lands the row back where it
        // started after flickering through the other state twice.
        if (!TreeItem.InCaret(source, container))
        {
            Toggle(container);
        }

        e.Handled = true;
    }
}
