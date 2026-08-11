using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// What a grid sounds like. Without these a screen reader hears a list box of content
/// controls and a cell reads as its text alone, with nothing saying which column it is in.
/// </summary>
public class DataGridAutomationPeer(ListBox owner) : ListBoxAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.DataGrid;

    /// <summary>How much is on screen, what it is sorted by and what is blocking a save.</summary>
    protected override string GetHelpTextCore() => Owner is not DataGrid grid
        ? base.GetHelpTextCore() ?? string.Empty
        : GridSpeech.Join(grid.ShownText, grid.SortText, grid.ErrorText);
}

/// <summary>A row, read out as the cells in it rather than as one blob of text.</summary>
public class DataGridRowAutomationPeer(ListBoxItem owner) : ListItemAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.DataItem;

    protected override string? GetNameCore() => Owner is IGridRowLayout row && row.Cells.Count > 0
        ? GridSpeech.Join([.. row.Cells.Select(GridSpeech.Read)])
        : base.GetNameCore();
}

/// <summary>
/// A cell, read out as its column and then its value, so moving sideways says where the
/// keyboard landed rather than only what is there.
/// </summary>
public class DataGridCellAutomationPeer(DataGridCell owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Custom;

    protected override string GetLocalizedControlTypeCore() => "cell";

    protected override string? GetNameCore() => GridSpeech.Read((DataGridCell)Owner);

    /// <summary>What is wrong with the value, which is never drawn in the cell itself.</summary>
    protected override string GetHelpTextCore() =>
        ((DataGridCell)Owner).Error ?? base.GetHelpTextCore() ?? string.Empty;
}

/// <summary>A column title, with the sort it is carrying, since the caret alone says nothing.</summary>
public class DataGridHeaderCellAutomationPeer(DataGridHeaderCell owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.HeaderItem;

    protected override string? GetNameCore() =>
        ((DataGridHeaderCell)Owner).Column?.Header?.ToString() ?? base.GetNameCore();

    protected override string GetHelpTextCore()
    {
        var cell = (DataGridHeaderCell)Owner;

        var sorted = cell.Column?.SortDirection switch
        {
            GridSortDirection.Ascending => "sorted ascending",
            GridSortDirection.Descending => "sorted descending",
            _ => string.Empty,
        };

        // The ordinal is drawn only for a sort of more than one key, and it is the whole
        // reason a person can tell the second key from the first.
        return cell.HasOrdinal && sorted.Length > 0
            ? $"{sorted}, key {cell.Ordinal}"
            : sorted;
    }
}

/// <summary>A group heading, which is a row in the list but not a record.</summary>
public class GridGroupRowAutomationPeer(ListBoxItem owner) : ListItemAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    protected override string? GetNameCore() => Owner is GridGroupRow group
        ? GridSpeech.Join(group.Label, group.Count == 1 ? "1 row" : $"{group.Count:N0} rows")
        : base.GetNameCore();
}

/// <summary>Turning what a grid draws into what it says. Shared by every peer above.</summary>
internal static class GridSpeech
{
    /// <summary>Joins the parts that have something in them, so an empty one leaves no gap.</summary>
    public static string Join(params string?[] parts) =>
        string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>
    /// A cell as its column and its value. The column's own reader is asked first, since a
    /// template can draw a pill or a swatch that no text walk would find words for.
    /// </summary>
    public static string Read(DataGridCell cell)
    {
        var title = cell.Column?.Header?.ToString();

        // Content, not DataContext. A cell is handed the row's item and the row itself holds
        // the wrapper, so the DataContext is a GridRow that no column reader takes.
        var value = cell.Column is { } column && column.HasValue && cell.Content is { } item
            ? column.ValueOf(item)?.ToString()
            : Words(cell);

        return Join(title, value);
    }

    /// <summary>
    /// What a cell has drawn, for a column that names no value. Every label in it, since a
    /// template is free to draw more than one.
    /// </summary>
    private static string Words(DataGridCell cell) => Join(
        [.. cell.GetVisualDescendants().OfType<TextBlock>().Select(label => label.Text)]);
}
