using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Workbench.Ui.Toasts;

/// <summary>
/// One of the eight stacks. It owns its own toasts, its own timers and its own limit,
/// so nothing that happens in one region moves anything in another.
/// </summary>
public interface IToastRegion : INotifyPropertyChanged
{
    /// <summary>Which corner or edge this stack is anchored to.</summary>
    ToastAnchor Anchor { get; }

    /// <summary>
    /// The cards that are showing, in the order they are drawn, top to bottom.
    /// </summary>
    /// <remarks>
    /// Oldest first, so the newest is last and sits at the foot of the deck. Every region
    /// stacks the same way whichever edge it is anchored to: the cards behind pile up
    /// above the newest one and tuck down under it. What the anchor decides is where the
    /// deck sits and which way it grows as cards arrive, not which end the newest is.
    /// </remarks>
    ReadOnlyObservableCollection<Toast> Visible { get; }

    /// <summary>
    /// How many are held back behind the limit. A stack draws this as a count rather
    /// than growing, and zero draws nothing.
    /// </summary>
    int Waiting { get; }

    /// <summary>
    /// Every timer in this region is stopped. A stack sets it while a pointer is over
    /// it, which is why hovering one region leaves the other seven counting down.
    /// </summary>
    bool IsPaused { get; set; }

    /// <summary>Clears the region, exit and all.</summary>
    void DismissAll();
}
