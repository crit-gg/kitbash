using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// One cell. It takes its look from the column and its content from the row, so nothing a
/// caller writes in a cell template has to name a brush or a family.
/// </summary>
public class DataGridCell : ContentControl
{
    public static readonly StyledProperty<GridColumn?> ColumnProperty =
        AvaloniaProperty.Register<DataGridCell, GridColumn?>(nameof(Column));

    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<DataGridCell, bool>(nameof(IsEditing));

    /// <summary>Whether this is where the keyboard is. One cell in a grid carries it.</summary>
    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<DataGridCell, bool>(nameof(IsCurrent));

    /// <summary>Whether this cell is inside a range, which is what wears the wash.</summary>
    public static readonly StyledProperty<bool> IsInRangeProperty =
        AvaloniaProperty.Register<DataGridCell, bool>(nameof(IsInRange));

    /// <summary>
    /// Which of this cell's edges are on the edge of the block it belongs to. It is the
    /// border's thickness and its inset at once, so the block's outline runs unbroken
    /// across the cells along it and still sits inside the block.
    /// </summary>
    public static readonly StyledProperty<Thickness> RangeEdgesProperty =
        AvaloniaProperty.Register<DataGridCell, Thickness>(nameof(RangeEdges));

    public GridColumn? Column
    {
        get => GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    /// <inheritdoc cref="IsCurrentProperty"/>
    public bool IsCurrent
    {
        get => GetValue(IsCurrentProperty);
        set => SetValue(IsCurrentProperty, value);
    }

    /// <inheritdoc cref="IsInRangeProperty"/>
    public bool IsInRange
    {
        get => GetValue(IsInRangeProperty);
        set => SetValue(IsInRangeProperty, value);
    }

    /// <inheritdoc cref="RangeEdgesProperty"/>
    public Thickness RangeEdges
    {
        get => GetValue(RangeEdgesProperty);
        set => SetValue(RangeEdgesProperty, value);
    }

    /// <summary>
    /// Whether the cell is showing its column's edit template. Start and end an edit
    /// through <see cref="DataGrid"/> rather than here, so only one cell is ever editing.
    /// </summary>
    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    /// <summary>Whether this cell could be edited at all.</summary>
    public bool CanEdit => Column?.EditTemplate is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColumnProperty)
        {
            Apply();
        }
        else if (change.Property == IsCurrentProperty)
        {
            PseudoClasses.Set(":current", change.GetNewValue<bool>());
        }
        else if (change.Property == IsInRangeProperty)
        {
            PseudoClasses.Set(":range", change.GetNewValue<bool>());
        }
        else if (change.Property == IsEditingProperty)
        {
            var editing = change.GetNewValue<bool>();

            PseudoClasses.Set(":editing", editing);
            Apply();

            if (editing)
            {
                // After the template has been swapped and laid out, or there is nothing
                // in the cell yet to take the focus.
                Dispatcher.InvokeAsync(FocusEditor, DispatcherPriority.Loaded);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsEditing && this.FindAncestorOfType<IGridHost>() is { } host)
        {
            switch (e.Key)
            {
                // An editor that takes a return keeps it. A multi line field is the whole
                // reason the key cannot simply belong to the grid.
                case Key.Enter when e.Source is not TextBox { AcceptsReturn: true }:
                    host.Body.CommitAndStepDown();
                    e.Handled = true;
                    return;

                case Key.Escape:
                    host.Body.CancelEdit();
                    e.Handled = true;
                    return;
            }
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// An edit ends when the pointer goes elsewhere, which is what every grid does and
    /// what stops a cell being left open behind a person's back.
    /// </summary>
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);

        if (IsEditing && !this.IsKeyboardFocusWithin && Left(e.NewFocusedElement)
            && this.FindAncestorOfType<IGridHost>() is { } host)
        {
            host.Body.CommitEdit();
        }
    }

    /// <summary>
    /// Whether the focus landed somewhere that ends the edit. Where it went decides,
    /// never what opened it, because the things that can take a focus away cannot be
    /// listed: a dropdown, a menu, a dialog and a native file picker all do it.
    /// </summary>
    private bool Left(IInputElement? landed)
    {
        // Nowhere at all. The app lost the focus, which is what a native picker does
        // without ever building a window Avalonia can see.
        if (landed is not Visual visual)
        {
            return false;
        }

        // Inside the cell, including a popup the cell opened, since a popup's content
        // keeps the control that owns it as its logical parent whether the popup is its
        // own window or an overlay.
        for (ILogical? node = landed as ILogical; node is not null; node = node.LogicalParent)
        {
            if (ReferenceEquals(node, this))
            {
                return false;
            }
        }

        // Another window, so a dialog opened over the grid. The edit waits for it.
        return ReferenceEquals(TopLevel.GetTopLevel(visual), TopLevel.GetTopLevel(this));
    }

    /// <summary>
    /// A press puts the keyboard here, so clicking a cell and then arrowing carries on from
    /// the cell that was clicked rather than from wherever the keyboard was left.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Column is not { } column || this.FindAncestorOfType<IGridHost>() is not { } host)
        {
            return;
        }

        // Shift extends the block from the anchor, which is what it does in every grid and
        // in every spreadsheet.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            host.Body.ExtendTo(this, column);
            e.Handled = true;
            return;
        }

        host.Body.SetCurrent(column);
        host.Body.BeginDrag();
    }

    /// <summary>
    /// A drag over the cells takes the block with it. Each cell answers for itself, so the
    /// pointer does not have to be captured to know which one it is over.
    /// </summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (Column is not { } column
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || this.FindAncestorOfType<IGridHost>() is not { Body.Dragging: true } host)
        {
            return;
        }

        host.Body.ExtendTo(this, column);

        // The list drags out a row selection of its own otherwise, so a drag would pick
        // rows and cells at once.
        e.Handled = true;
    }

    private void Apply()
    {
        var column = Column;

        PseudoClasses.Set(":mono", column?.IsMono == true);
        PseudoClasses.Set(":strong", column?.IsStrong == true);
        PseudoClasses.Set(":editable", column?.EditTemplate is not null);

        if (column is null)
        {
            ContentTemplate = null;
            return;
        }

        HorizontalContentAlignment = column.Alignment;
        ContentTemplate = IsEditing ? column.EditTemplate ?? column.CellTemplate : column.CellTemplate;
    }

    private void FocusEditor()
    {
        if (!IsEditing)
        {
            return;
        }

        foreach (var child in this.GetVisualDescendants().OfType<InputElement>())
        {
            if (child.Focusable && child.IsEffectivelyEnabled && child.IsEffectivelyVisible)
            {
                child.Focus(NavigationMethod.Tab);
                return;
            }
        }
    }
}
