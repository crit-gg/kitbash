using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Textures;

/// <summary>
/// The strip under a node's body, saying what it cost to cook. It is the library's footer
/// slot, which exists so a node can carry a readout without the body giving up room for it.
/// </summary>
public class TextureCost : Control
{
    private IBrush _ground = Brushes.Black;
    private IPen _seam = new Pen(Brushes.Gray);
    private IBrush _ink = Brushes.Gray;
    private IBrush _warn = Brushes.Orange;
    private FontFamily _mono = FontFamily.Default;

    public TextureCook? Cook { get; set; }

    /// <summary>Whether the time is shown at all, which the toolbar switches.</summary>
    public bool ShowsTimings { get; set; } = true;

    public override void Render(DrawingContext context)
    {
        if (DataContext is not GraphNode node || Bounds.Height <= 0)
        {
            return;
        }

        var box = new Rect(Bounds.Size);

        context.DrawRectangle(_ground, null, box);
        context.DrawLine(_seam, box.TopLeft, box.TopRight);

        var bake = Cook?.Bake(node);
        var own = node.Tag as TextureNode;

        if (ShowsTimings && bake is not null)
        {
            // Anything over a millisecond on a preview this size is worth a second look, so
            // it says so in the warning tint rather than in the same grey as the rest.
            var took = Mono(
                bake.Milliseconds.ToString("0.00", CultureInfo.InvariantCulture) + " ms",
                bake.Milliseconds > 1 ? _warn : _ink);

            context.DrawText(took, new Point(8, box.Center.Y - took.Height / 2));
        }

        if (own is { Note.Length: > 0 })
        {
            var note = Mono(own.Note, _ink);

            context.DrawText(note, new Point(box.Width - 8 - note.Width, box.Center.Y - note.Height / 2));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _ground = Brush("SurfaceRoot", Brushes.Black);
        _seam = new Pen(Brush("LineSeam", Brushes.Gray)).ToImmutable();
        _ink = Brush("InkMuted", Brushes.Gray);
        _warn = Brush("Warn", Brushes.Orange);
        _mono = this.TryFindResource("FontFamilyMono", out var found) && found is FontFamily family
            ? family
            : FontFamily.Default;

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

    private IBrush Brush(string key, IBrush fallback) =>
        this.TryFindResource(key, out var found) && found is IBrush brush ? brush : fallback;

    private FormattedText Mono(string text, IBrush ink) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(_mono), 11, ink);
}
