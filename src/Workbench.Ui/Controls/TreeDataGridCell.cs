using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Workbench.Ui.Controls;

/// <summary>
/// The cell that carries the hierarchy. It is the only one in a tree grid that indents,
/// which is the whole difference between a tree grid and a flat one.
/// </summary>
public class TreeDataGridCell : DataGridCell
{
    public static readonly StyledProperty<int> LevelProperty =
        AvaloniaProperty.Register<TreeDataGridCell, int>(nameof(Level));

    public static readonly StyledProperty<bool> HasChildrenProperty =
        AvaloniaProperty.Register<TreeDataGridCell, bool>(nameof(HasChildren));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<TreeDataGridCell, bool>(nameof(IsExpanded));

    /// <summary>How far one level indents. The theme sets it from the token.</summary>
    public static readonly StyledProperty<double> IndentSizeProperty =
        AvaloniaProperty.Register<TreeDataGridCell, double>(nameof(IndentSize));

    public static readonly DirectProperty<TreeDataGridCell, Thickness> IndentProperty =
        AvaloniaProperty.RegisterDirect<TreeDataGridCell, Thickness>(nameof(Indent), cell => cell.indent);

    private const string CaretPart = "PART_Caret";

    private Thickness indent;
    private Control? caret;

    public int Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool HasChildren
    {
        get => GetValue(HasChildrenProperty);
        set => SetValue(HasChildrenProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public double IndentSize
    {
        get => GetValue(IndentSizeProperty);
        set => SetValue(IndentSizeProperty, value);
    }

    /// <summary>What the caret and the content are pushed in by.</summary>
    public Thickness Indent => indent;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (caret is not null)
        {
            caret.PointerPressed -= OnCaretPressed;
        }

        caret = e.NameScope.Find<Control>(CaretPart);

        if (caret is not null)
        {
            caret.PointerPressed += OnCaretPressed;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LevelProperty || change.Property == IndentSizeProperty)
        {
            SetAndRaise(IndentProperty, ref indent, new Thickness(Math.Max(0, Level) * IndentSize, 0, 0, 0));
        }
        else if (change.Property == HasChildrenProperty)
        {
            PseudoClasses.Set(":branch", change.GetNewValue<bool>());
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(":expanded", change.GetNewValue<bool>());
        }
    }

    private void OnCaretPressed(object? sender, PointerPressedEventArgs e)
    {
        // Left only. A right click belongs to whatever context menu the row carries, and
        // a middle click is not an open anywhere else either, so both are left to bubble.
        if (!HasChildren || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (this.FindAncestorOfType<TreeItem>() is { } row && this.FindAncestorOfType<Tree>() is { } tree)
        {
            tree.Toggle(row);
        }

        // Handled, so the press never reaches the row and the caret opens without selecting.
        e.Handled = true;
    }
}
