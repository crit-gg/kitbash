using Avalonia;
using Avalonia.Controls;

namespace Workbench.Ui.Controls;

/// <summary>
/// Lays toasts out as a deck: the newest card whole, and the ones behind it showing a
/// strip of their top edge.
/// </summary>
/// <remarks>
/// A panel rather than a spaced column, because a stack of three full cards is most of a
/// window and reads as a list rather than as something passing through. The design draws
/// the deck and nothing in Avalonia lays one out.
/// <para>
/// Every region stacks the same way, whichever edge it is anchored to: the newest card is
/// at the foot of the deck and the ones behind it pile up above it, each one a
/// <see cref="Peek"/> higher. What the anchor decides is where the deck sits and which way
/// it grows as cards arrive, not which end the newest is at. The region hands the cards
/// over in that order, oldest first.
/// </para>
/// <para>
/// Which card is in front is not this panel's to say. A card sets its own
/// <see cref="Visual.ZIndex"/> from its depth, so the newest draws over the rest wherever
/// it sits in the order.
/// </para>
/// </remarks>
public class ToastDeck : Panel
{
    /// <summary>
    /// How much of the card behind shows. The design draws 20 and 26 for the two cards
    /// behind the newest, and one value is taken for both.
    /// </summary>
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
        // carries it under the card in front.
        //
        // Cards are not all the same height, and a deck of mixed heights cannot both put
        // the newest at the foot and show an even strip of each card behind it. Stepping
        // the tops leaves a short newest card floating clear of the foot, and stepping the
        // bottoms hides a short card behind a tall one completely, which is the design's
        // own illustration where the newest is the tallest of the three.
        //
        // Giving each card behind its own strip settles both, and holding it to that strip
        // alone is what stops a faded card reading through to the text of the one under
        // it. Measured, and it did. The tuck is the smallest overlap that still puts a
        // card behind the front one's corner curves rather than the page.
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
