using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The row that opens a group in a flat grid. It spans every column, which is what tells
/// it apart from a tree grid branch row: this one is grouping and that one is hierarchy.
/// </summary>
public class GridGroupRow : ListBoxItem
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<GridGroupRow, string?>(nameof(Label));

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<GridGroupRow, int>(nameof(Count));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<GridGroupRow, bool>(nameof(IsExpanded), true);

    private GridRow? row;

    public GridGroupRow()
    {
        // A group opens as open, and a property that never changes raises nothing, so the
        // caret would point the wrong way until the first time it was clicked.
        PseudoClasses.Set(":expanded", IsExpanded);
    }

    /// <summary>What the group is grouped by, written out.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>How many rows the group holds, whether or not they are shown.</summary>
    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public GridRow? Row => row;

    internal void Follow(GridRow? next)
    {
        if (row is not null)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        row = next;

        if (row is not null)
        {
            row.PropertyChanged += OnRowChanged;
        }

        Label = row?.Key?.ToString();
        Count = row?.Count ?? 0;
        IsExpanded = row?.IsExpanded ?? true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GridGroupRowAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(":expanded", change.GetNewValue<bool>());
        }
    }

    /// <summary>
    /// The whole row opens and closes the group, since a group header is a heading rather
    /// than a row of data and there is nothing on it to pick.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Toggle();

            // Handled, so the list never selects a heading.
            e.Handled = true;
            return;
        }

        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space or Key.Left or Key.Right)
        {
            Toggle();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void Toggle()
    {
        if (row is not null)
        {
            this.FindAncestorOfType<DataGrid>()?.Toggle(row);
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is GridRow changed && ReferenceEquals(changed, row))
        {
            IsExpanded = changed.IsExpanded;
        }
    }
}
