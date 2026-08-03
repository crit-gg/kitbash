using Avalonia;
using Avalonia.Controls;

namespace Workbench.Ui.Controls;

/// <summary>
/// Lays toasts out as a deck: the newest card whole, and the ones behind it showing a
/// strip of their top edge.
/// </summary>
public class ToastDeck : Panel
{
    /// <summary>How much of a card behind shows, in pixels. One value for all of them.</summary>
    public static readonly StyledProperty<double> PeekProperty =
        AvaloniaProperty.Register<ToastDeck, double>(nameof(Peek));

    /// <summary>
    /// How far a card behind runs on under the one in front of it, past the strip that
    /// shows. It is the surface radius, so the front card's corner curves have a card
    /// behind them rather than the page.
    /// </summary>
    public static readonly StyledProperty<double> TuckProperty =
        AvaloniaProperty.Register<ToastDeck, double>(nameof(Tuck));

    static ToastDeck()
    {
        AffectsMeasure<ToastDeck>(PeekProperty, TuckProperty);
        AffectsArrange<ToastDeck>(PeekProperty, TuckProperty);
    }

    /// <inheritdoc cref="TuckProperty"/>
    public double Tuck
    {
        get => GetValue(TuckProperty);
        set => SetValue(TuckProperty, value);
    }

    /// <inheritdoc cref="PeekProperty"/>
    public double Peek
    {
        get => GetValue(PeekProperty);
        set => SetValue(PeekProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return default;
        }

        var width = 0d;

        foreach (var child in Children)
        {
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
        }

        return new Size(width, Front.DesiredSize.Height + (Children.Count - 1) * Peek);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }

        // A card behind is arranged as the strip of it that shows, plus the tuck that
        // carries it under the card in front. Sizing it to the strip rather than stepping
        // the tops or the bottoms is what makes a deck of mixed heights work, and it also
        // keeps a faded card's own text out from under the card in front.
        var front = Front.DesiredSize.Height;

        for (var at = 0; at < Children.Count; at++)
        {
            var back = Back(at);

            if (back == 0)
            {
                Children[at].Arrange(new Rect(0, finalSize.Height - front, finalSize.Width, front));
                continue;
            }

            // The full width, so a card places itself across it by its own alignment and
            // a deck under a left anchored region reads the same as one under a right.
            Children[at].Arrange(new Rect(
                0,
                finalSize.Height - front - back * Peek,
                finalSize.Width,
                Peek + Tuck));
        }

        return finalSize;
    }

    /// <summary>
    /// The newest card, which is the one drawn whole. The region hands them over oldest
    /// first, so it is always the last of them.
    /// </summary>
    private Control Front => Children[^1];

    /// <summary>How many cards back from the newest this one is.</summary>
    private int Back(int at) => Children.Count - 1 - at;
}
