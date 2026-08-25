using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Textures;

/// <summary>
/// What fills a node's body: the map that node cooked, in a well. This is the composed path
/// the library leaves open, so the card draws the frame, the header and the pins and a real
/// control fills the middle.
/// </summary>
public class TexturePane : Control
{
    /// <summary>Whether this is an output card, which is a thumbnail beside two lines.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<TexturePane, bool>(nameof(IsCompact));

    private IBrush _well = Brushes.Black;
    private IPen _edge = new Pen(Brushes.Gray);
    private IBrush _ink = Brushes.White;
    private IBrush _quiet = Brushes.Gray;
    private FontFamily _mono = FontFamily.Default;
    private FontFamily _ui = FontFamily.Default;

    public TextureCook? Cook { get; set; }

    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (DataContext is not GraphNode node || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var bake = Cook?.Bake(node);

        if (IsCompact)
        {
            Compact(context, node, bake);
            return;
        }

        var well = new Rect(Bounds.Size).Deflate(6);

        Well(context, well, bake, node);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Read();

        if (Cook is not null)
        {
            Cook.Changed += OnCooked;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (Cook is not null)
        {
            Cook.Changed -= OnCooked;
        }
    }

    private void OnCooked(object? sender, EventArgs e) => InvalidateVisual();

    private void Read()
    {
        _well = Brush("SurfaceWell", Brushes.Black);
        _edge = new Pen(Brush("LineSeam", Brushes.Gray)).ToImmutable();
        _ink = Brush("InkSecondary", Brushes.White);
        _quiet = Brush("InkDisabled", Brushes.Gray);
        _mono = Family("FontFamilyMono");
        _ui = Family("FontFamilyUi");
    }

    private FontFamily Family(string key) =>
        this.TryFindResource(key, out var found) && found is FontFamily family ? family : FontFamily.Default;

    private IBrush Brush(string key, IBrush fallback) =>
        this.TryFindResource(key, out var found) && found is IBrush brush ? brush : fallback;

    /// <summary>The preview in its well, cropped to fill rather than squashed to fit.</summary>
    private void Well(DrawingContext context, Rect well, TextureBake? bake, GraphNode node)
    {
        context.DrawRectangle(_well, _edge, new RoundedRect(well, 4));

        if (bake is null || node.IsBypassed)
        {
            var says = node.IsBypassed ? "Passes through" : "Nothing connected yet";

            context.DrawText(Words(says, _quiet, well.Width - 12), new Point(well.X + 6, well.Center.Y - 8));
            return;
        }

        using var _ = context.PushClip(new RoundedRect(well.Deflate(1), 3));

        // The map is square and the well is not, so the shorter side fills and the longer one
        // takes the crop. Squashing a texture preview is how a person reads the wrong thing.
        var scale = Math.Max(well.Width / TextureMap.Side, well.Height / TextureMap.Side);
        var wide = TextureMap.Side * scale;
        var tall = TextureMap.Side * scale;

        context.DrawImage(
            bake.Bitmap,
            new Rect(well.Center.X - wide / 2, well.Center.Y - tall / 2, wide, tall));
    }

    /// <summary>An output card: a small square of what it writes, then the slot and the format.</summary>
    private void Compact(DrawingContext context, GraphNode node, TextureBake? bake)
    {
        var own = node.Tag as TextureNode;
        var thumb = new Rect(9, Bounds.Center.Y - 15, 30, 30);

        context.DrawRectangle(_well, _edge, new RoundedRect(thumb, 4));

        if (bake is not null)
        {
            using var _ = context.PushClip(new RoundedRect(thumb.Deflate(1), 3));

            context.DrawImage(bake.Bitmap, thumb);
        }

        var slot = Mono(own?.Slot ?? string.Empty, _ink);
        var format = Mono(own?.Format ?? string.Empty, _quiet);
        var at = thumb.Right + 9;

        context.DrawText(slot, new Point(at, Bounds.Center.Y - slot.Height - 1));
        context.DrawText(format, new Point(at, Bounds.Center.Y + 1));
    }

    private FormattedText Words(string text, IBrush ink, double width) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(_ui), 11, ink)
        {
            MaxTextWidth = Math.Max(1, width),
            TextAlignment = TextAlignment.Center,
        };

    private FormattedText Mono(string text, IBrush ink) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(_mono), 11, ink);
}
