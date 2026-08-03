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
    ReadOnlyObservableCollection<Toast> Visible { get; }

    /// <summary>
    /// How many are held back behind the limit. A stack draws this as a count rather
    /// than growing, and zero draws nothing.
    /// </summary>
    int Waiting { get; }

    /// <summary>
    /// Stops every timer in this region. A stack sets it while a pointer is over it, so
    /// the other regions keep counting down.
    /// </summary>
    bool IsPaused { get; set; }

    /// <summary>Clears the region, exit and all.</summary>
    void DismissAll();
}
