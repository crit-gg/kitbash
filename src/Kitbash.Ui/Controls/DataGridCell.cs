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

    public GridColumn? Column
    {
        get => GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
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
        if (IsEditing && this.FindAncestorOfType<DataGrid>() is { } grid)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    grid.CommitEdit();
                    e.Handled = true;
                    return;

                case Key.Escape:
                    grid.CancelEdit();
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
            && this.FindAncestorOfType<DataGrid>() is { } grid)
        {
            grid.CommitEdit();
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

    private void Apply()
    {
        var column = Column;

        PseudoClasses.Set(":mono", column?.IsMono == true);
        PseudoClasses.Set(":strong", column?.IsStrong == true);

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
