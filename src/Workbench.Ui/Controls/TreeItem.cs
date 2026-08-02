using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Workbench.Ui.Controls;

/// <summary>
/// One row of a <see cref="Tree"/>. A list row that also knows how deep it is.
/// </summary>
/// <remarks>
/// Depth is a number here rather than a place in the tree of controls, so the indent and
/// the guides are arithmetic and a row can be recycled onto any other row.
/// <para>
/// Everything a row is told arrives through <see cref="Follow"/> and is taken back there
/// as well. A virtualising panel reuses a container for a different row, so anything set
/// when one is prepared has to be unset in the same place or a scrolled row wears the
/// state of the row it used to be.
/// </para>
/// </remarks>
public class TreeItem : ListBoxItem
{
    public static readonly StyledProperty<int> LevelProperty =
        AvaloniaProperty.Register<TreeItem, int>(nameof(Level));

    public static readonly StyledProperty<bool> HasChildrenProperty =
        AvaloniaProperty.Register<TreeItem, bool>(nameof(HasChildren));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<TreeItem, bool>(nameof(IsExpanded));

    /// <summary>How far one level indents. The theme sets it from the token.</summary>
    public static readonly StyledProperty<double> IndentSizeProperty =
        AvaloniaProperty.Register<TreeItem, double>(nameof(IndentSize));

    /// <summary>Where in a level's band the guide falls. The theme sets it from the token.</summary>
    public static readonly StyledProperty<double> GuideOffsetProperty =
        AvaloniaProperty.Register<TreeItem, double>(nameof(GuideOffset));

    public static readonly StyledProperty<IBrush?> GuideBrushProperty =
        AvaloniaProperty.Register<TreeItem, IBrush?>(nameof(GuideBrush));

    public static readonly DirectProperty<TreeItem, Thickness> IndentProperty =
        AvaloniaProperty.RegisterDirect<TreeItem, Thickness>(nameof(Indent), item => item.indent);

    private Thickness indent;
    private Control? caret;
    private TreeRow? row;

    static TreeItem()
    {
        AffectsRender<TreeItem>(LevelProperty, IndentSizeProperty, GuideOffsetProperty, GuideBrushProperty);
    }

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

    public double GuideOffset
    {
        get => GetValue(GuideOffsetProperty);
        set => SetValue(GuideOffsetProperty, value);
    }

    public IBrush? GuideBrush
    {
        get => GetValue(GuideBrushProperty);
        set => SetValue(GuideBrushProperty, value);
    }

    /// <summary>
    /// What the row's own frame is pushed in by. The frame is inset rather than the whole
    /// row, so a fill starts where the row starts and the guides to its left stay visible.
    /// </summary>
    public Thickness Indent => indent;

    /// <summary>The row this container is standing in for, or null while it is spare.</summary>
    public TreeRow? Row => row;

    /// <summary>
    /// Takes on a row, or gives up the one it had. Null releases the container, which is
    /// what stops a recycled row carrying the last one's depth.
    /// </summary>
    internal void Follow(TreeRow? next)
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

        Level = row?.Level ?? 0;
        HasChildren = row?.HasChildren ?? false;
        IsExpanded = row?.IsExpanded ?? false;

        // The model rather than the row, so a view's template binds to the thing the tree
        // is showing and never has to reach through a row to find it.
        if (row is not null)
        {
            Content = row.Item;
        }

        Update();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (caret is not null)
        {
            caret.PointerPressed -= OnCaretPressed;
        }

        caret = e.NameScope.Find<Control>("PART_Caret");

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
            Update();
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

    /// <summary>
    /// The indent guides, one per level above this row.
    /// </summary>
    /// <remarks>
    /// Drawn rather than laid out, because a row is realised and thrown away as the tree
    /// scrolls and one element per level would be built and discarded with it.
    /// <para>
    /// A guide runs past the bottom of the row and across the gap under it, so the line a
    /// person sees is continuous. The gap belongs to the row's margin, which is outside
    /// its bounds, and this reaches into it. The theme leaves the row unclipped for the
    /// focus halo, which is what lets that draw.
    /// </para>
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Level <= 0 || GuideBrush is not { } brush || IndentSize <= 0)
        {
            return;
        }

        var pen = new Pen(brush);
        var height = Bounds.Height + Margin.Bottom;

        for (var level = 0; level < Level; level++)
        {
            // Half a pixel over, so a one pixel line lands on a pixel rather than across
            // the seam between two.
            var x = Math.Round(level * IndentSize + GuideOffset) + 0.5;

            context.DrawLine(pen, new Point(x, 0), new Point(x, height));
        }
    }

    private void OnCaretPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!HasChildren)
        {
            return;
        }

        Toggle();

        // Handled, so the press never reaches the row. The design asks for a click on the
        // caret to open a row without also selecting it.
        e.Handled = true;
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TreeRow changed || !ReferenceEquals(changed, row))
        {
            return;
        }

        HasChildren = changed.HasChildren;
        IsExpanded = changed.IsExpanded;
    }

    internal void Toggle() => this.FindAncestorOfType<Tree>()?.Toggle(this);

    private void Update()
    {
        var next = new Thickness(Math.Max(0, Level) * IndentSize, 0, 0, 0);

        SetAndRaise(IndentProperty, ref indent, next);
    }
}
