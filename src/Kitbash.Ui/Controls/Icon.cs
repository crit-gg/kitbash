using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Draws one glyph from the Box Icons set at a given size.
/// </summary>
public class Icon : TemplatedControl
{
    /// <summary>Which glyph. The enum is generated beside the geometry, so it cannot name one that does not exist.</summary>
    public static readonly StyledProperty<IconGlyph> GlyphProperty =
        AvaloniaProperty.Register<Icon, IconGlyph>(nameof(Glyph));

    /// <summary>
    /// The drawn size, in both directions. 16 in tables, trees, the status bar and
    /// inline chips. 20 in the activity rail, tool cards and empty states.
    /// </summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(Size), 16d);

    public static readonly DirectProperty<Icon, Geometry?> DataProperty =
        AvaloniaProperty.RegisterDirect<Icon, Geometry?>(nameof(Data), o => o.Data);

    private Geometry? _data;

    public IconGlyph Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>The geometry the template draws. Resolved from <see cref="Glyph"/>.</summary>
    public Geometry? Data
    {
        get => _data;
        private set => SetAndRaise(DataProperty, ref _data, value);
    }

    // Resources are only reachable once the control is in a tree, so the first resolve
    // happens here rather than in the constructor.
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        Resolve();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GlyphProperty)
        {
            Resolve();
        }
    }

    private void Resolve() =>
        Data = this.TryFindResource(GeometryKey(Glyph), out var found) ? found as Geometry : null;

    /// <summary>
    /// The geometry key for a glyph. Both sides of this come from the same list in
    /// tools/icons/generate.py, so a member always has a geometry.
    /// </summary>
    private static string GeometryKey(IconGlyph glyph) => "Icon" + glyph;
}
