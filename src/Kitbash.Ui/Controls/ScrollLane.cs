using Avalonia;
using Avalonia.Controls;

namespace Kitbash.Ui.Controls;

/// <summary>Which of the four lanes a scrollbar draws.</summary>
public enum ScrollLaneKind
{
    /// <summary>Twelve pixels on a panel. Trees, lists and sidebars.</summary>
    Panel,

    /// <summary>Fourteen, with the lane drawn. Only a surface that scrolls both ways.</summary>
    Tracked,

    /// <summary>Eight, for a pane too narrow to give twelve to a lane.</summary>
    Dense,

    /// <summary>Ten, a rung brighter, for a well.</summary>
    Well,
}

/// <summary>
/// Which lane the scrollbars under here draw. It is inherited, so setting it on a panel
/// reaches every bar inside it, and a scrollbar is otherwise unreachable from a view since
/// it belongs to a scroll viewer's template.
/// </summary>
public class ScrollLane
{
    public static readonly AttachedProperty<ScrollLaneKind> KindProperty =
        AvaloniaProperty.RegisterAttached<ScrollLane, Control, ScrollLaneKind>("Kind", inherits: true);

    public static ScrollLaneKind GetKind(Control control) => control.GetValue(KindProperty);

    public static void SetKind(Control control, ScrollLaneKind value) => control.SetValue(KindProperty, value);
}
