using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Controls.Primitives.PopupPositioning;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Places a control's popup under it, left aligned, whatever the control asked for.
/// Attach it to the control: <c>ui:Popups.Under="True"</c>. The one opt out is
/// <c>ui:Popups.AlignsRight</c>.
/// </summary>
public class Popups
{
    private const string PopupPart = "PART_Popup";

    /// <summary>The control the popup belongs to, held on the popup so the reopen knows it.</summary>
    private static readonly AttachedProperty<Control?> OwnerProperty =
        AvaloniaProperty.RegisterAttached<Popups, Popup, Control?>("Owner");

    /// <summary>
    /// What the theme asked for, read once before the control has overwritten anything.
    /// Restoring zero instead would take away the pull back that gives the shadow its room.
    /// </summary>
    private static readonly AttachedProperty<Point> WantedProperty =
        AvaloniaProperty.RegisterAttached<Popups, Popup, Point>("Wanted");

    public static readonly AttachedProperty<bool> UnderProperty =
        AvaloniaProperty.RegisterAttached<Popups, TemplatedControl, bool>("Under");

    /// <summary>
    /// Lines the popup up with this control's right edge rather than its left. Set it on
    /// the control the popup belongs to, not on the popup.
    /// </summary>
    public static readonly AttachedProperty<bool> AlignsRightProperty =
        AvaloniaProperty.RegisterAttached<Popups, Control, bool>("AlignsRight");

    public static bool GetAlignsRight(Control control) => control.GetValue(AlignsRightProperty);

    public static void SetAlignsRight(Control control, bool value) =>
        control.SetValue(AlignsRightProperty, value);

    static Popups()
    {
        UnderProperty.Changed.AddClassHandler<TemplatedControl, bool>(OnUnderChanged);
        InPopupProperty.Changed.AddClassHandler<Control, bool>(OnInPopupChanged);
        KeepsWheelProperty.Changed.AddClassHandler<Control, bool>(OnKeepsWheelChanged);
        MatchesTargetProperty.Changed.AddClassHandler<Control, bool>(OnMatchesTargetChanged);
        RoomProperty.Changed.AddClassHandler<Control, bool>(OnRoomChanged);
        PullsRoomProperty.Changed.AddClassHandler<Decorator, bool>(OnPullsRoomChanged);
    }

    /// <summary>
    /// This element is a popup's room, and the room is fitted to where the popup is placed so
    /// the popup window never lands over the control it belongs to. For a tooltip, whose
    /// offsets are bound from the control and cannot be reached from a theme.
    /// </summary>
    public static readonly AttachedProperty<bool> PullsRoomProperty =
        AvaloniaProperty.RegisterAttached<Popups, Decorator, bool>("PullsRoom");

    public static bool GetPullsRoom(Decorator room) => room.GetValue(PullsRoomProperty);

    public static void SetPullsRoom(Decorator room, bool value) => room.SetValue(PullsRoomProperty, value);

    /// <summary>The room the theme asked for, kept because a placement can take a side of it.</summary>
    private static readonly AttachedProperty<Thickness?> WantedRoomProperty =
        AvaloniaProperty.RegisterAttached<Popups, Decorator, Thickness?>("WantedRoom");

    private static void OnPullsRoomChanged(Decorator room, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        room.AttachedToVisualTree -= OnRoomAttached;

        if (change.GetNewValue<bool>())
        {
            room.AttachedToVisualTree += OnRoomAttached;
        }
    }

    // A tooltip binds its offsets from its control on every open, so the room is written each
    // time it is attached rather than once. A popup is positioned after its child is attached,
    // so this still reaches the open that is happening.
    private static void OnRoomAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Decorator room || Containing(room) is not { } popup)
        {
            return;
        }

        var wanted = WantedRoom(room);

        if (Facing(popup.Placement) is not { } facing)
        {
            // At the pointer. Avalonia opens the tip 20px below it and the room has none
            // above the card, so only the horizontal offset moves.
            room.SetCurrentValue(Decorator.PaddingProperty, wanted);
            popup.SetCurrentValue(Popup.HorizontalOffsetProperty, -wanted.Left);
            return;
        }

        // Beside a control, room facing it is popup window over the control, which takes the
        // pointer, so the control drops its hover and the tip closes itself. That side is the
        // standoff instead, and it is all the room the shadow gets there.
        var gap = room.FindResource("TooltipGap") as double? ?? 0;
        var middle = Middle(popup.Placement, wanted);

        room.SetCurrentValue(Decorator.PaddingProperty, Standoff(wanted, facing, gap));
        popup.SetCurrentValue(Popup.HorizontalOffsetProperty, middle.X);
        popup.SetCurrentValue(Popup.VerticalOffsetProperty, middle.Y);
    }

    private static Thickness WantedRoom(Decorator room)
    {
        if (room.GetValue(WantedRoomProperty) is { } wanted)
        {
            return wanted;
        }

        room.SetValue(WantedRoomProperty, room.Padding);

        return room.Padding;
    }

    /// <summary>Which side of the room faces the control, or none when the popup is at the pointer.</summary>
    private static Edge? Facing(PlacementMode placement) => placement switch
    {
        PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom => Edge.Left,
        PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom => Edge.Right,
        PlacementMode.Bottom or PlacementMode.BottomEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedRight => Edge.Top,
        PlacementMode.Top or PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight => Edge.Bottom,
        _ => null,
    };

    private static Thickness Standoff(Thickness room, Edge facing, double gap) => facing switch
    {
        Edge.Left => new Thickness(gap, room.Top, room.Right, room.Bottom),
        Edge.Top => new Thickness(room.Left, gap, room.Right, room.Bottom),
        Edge.Right => new Thickness(room.Left, room.Top, gap, room.Bottom),
        _ => new Thickness(room.Left, room.Top, room.Right, gap),
    };

    /// <summary>
    /// A popup beside a control is centred on it across the other axis, room and all, so a
    /// room that is deeper on one side carries the card off centre. This puts it back.
    /// </summary>
    private static Point Middle(PlacementMode placement, Thickness room) => placement switch
    {
        PlacementMode.Right or PlacementMode.Left => new Point(0, (room.Bottom - room.Top) / 2),
        PlacementMode.Top or PlacementMode.Bottom => new Point((room.Right - room.Left) / 2, 0),
        _ => default,
    };

    private enum Edge
    {
        Left,
        Top,
        Right,
        Bottom,
    }

    /// <summary>
    /// This element is the transparent room a popup's shadow falls into, and a press on it
    /// closes the popup.
    /// </summary>
    public static readonly AttachedProperty<bool> RoomProperty =
        AvaloniaProperty.RegisterAttached<Popups, Control, bool>("Room");

    public static bool GetRoom(Control control) => control.GetValue(RoomProperty);

    public static void SetRoom(Control control, bool value) => control.SetValue(RoomProperty, value);

    private static void OnRoomChanged(Control control, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        control.RemoveHandler(InputElement.PointerPressedEvent, OnRoomPressed);

        if (!change.GetNewValue<bool>())
        {
            return;
        }

        // Nothing with no fill is hit tested, and the room has none by definition, so it
        // takes the one fill that draws nothing.
        if (control is Border { Background: null } border)
        {
            border.SetCurrentValue(Border.BackgroundProperty, Brushes.Transparent);
        }

        control.AddHandler(InputElement.PointerPressedEvent, OnRoomPressed);
    }

    // Only a press that landed on the room itself. Anything inside the overlay is the
    // overlay's, and a handled press never reaches this at all.
    private static void OnRoomPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || !ReferenceEquals(e.Source, control))
        {
            return;
        }

        Containing(control)?.Close();
        e.Handled = true;
    }

    private static void OnMatchesTargetChanged(Control control, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        control.AttachedToVisualTree -= OnMeasureAgainstTarget;

        if (change.GetNewValue<bool>())
        {
            control.AttachedToVisualTree += OnMeasureAgainstTarget;
        }
    }

    private static void OnMeasureAgainstTarget(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control && Containing(control) is { PlacementTarget: { } target })
        {
            control.SetCurrentValue(Layoutable.MinWidthProperty, target.Bounds.Width);
        }
    }

    /// <summary>The popup this element is drawn inside, if it is inside one.</summary>
    private static Popup? Containing(StyledElement element)
    {
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is Popup popup)
            {
                return popup;
            }
        }

        return null;
    }

    private static void OnKeepsWheelChanged(Control control, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        control.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);

        if (change.GetNewValue<bool>())
        {
            control.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel);
        }
    }

    private static void OnWheel(object? sender, PointerWheelEventArgs e) => e.Handled = true;

    /// <summary>
    /// This overlay is at least as wide as whatever it opened from.
    /// </summary>
    public static readonly AttachedProperty<bool> MatchesTargetProperty =
        AvaloniaProperty.RegisterAttached<Popups, Control, bool>("MatchesTarget");

    public static bool GetMatchesTarget(Control control) => control.GetValue(MatchesTargetProperty);

    public static void SetMatchesTarget(Control control, bool value) =>
        control.SetValue(MatchesTargetProperty, value);

    /// <summary>
    /// A wheel that nothing inside this overlay wanted stops here rather than reaching the
    /// page behind it.
    /// </summary>
    public static readonly AttachedProperty<bool> KeepsWheelProperty =
        AvaloniaProperty.RegisterAttached<Popups, Control, bool>("KeepsWheel");

    public static bool GetKeepsWheel(Control control) => control.GetValue(KeepsWheelProperty);

    public static void SetKeepsWheel(Control control, bool value) =>
        control.SetValue(KeepsWheelProperty, value);

    /// <summary>
    /// This element is the child of a popup, and that popup should be placed under its
    /// target with room for its shadow. For a flyout's presenter, where the offsets live
    /// on the flyout and a flyout is not a control and cannot be themed.
    /// </summary>
    public static readonly AttachedProperty<bool> InPopupProperty =
        AvaloniaProperty.RegisterAttached<Popups, Control, bool>("InPopup");

    public static bool GetInPopup(Control control) => control.GetValue(InPopupProperty);

    public static void SetInPopup(Control control, bool value) => control.SetValue(InPopupProperty, value);

    public static bool GetUnder(TemplatedControl control) => control.GetValue(UnderProperty);

    public static void SetUnder(TemplatedControl control, bool value) => control.SetValue(UnderProperty, value);

    private static void OnUnderChanged(TemplatedControl control, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        control.TemplateApplied -= OnTemplateApplied;

        if (change.GetNewValue<bool>())
        {
            control.TemplateApplied += OnTemplateApplied;
        }
    }

    private static void OnInPopupChanged(Control control, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        control.AttachedToVisualTree -= OnAttached;

        if (change.GetNewValue<bool>())
        {
            control.AttachedToVisualTree += OnAttached;
        }
    }

    // A popup's child has the popup as its logical parent, which is the only way in from
    // here: the flyout that owns it is not a control and cannot be reached from a theme.
    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Control control || control.Parent is not Popup popup)
        {
            return;
        }

        Take(popup, control);
        Place(popup);

        popup.PropertyChanged -= OnPopupChanged;
        popup.PropertyChanged += OnPopupChanged;
    }

    // The pull that cancels the room, read off the tokens rather than written here.
    private static void Take(Popup popup, IResourceHost source)
    {
        var x = source.FindResource("OverlayRoomX") as double? ?? 0;
        var y = source.FindResource("OverlayRoomY") as double? ?? 0;

        popup.SetValue(WantedProperty, new Point(x, y));
    }

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender is not Control control || e.NameScope.Find(PopupPart) is not Popup popup)
        {
            return;
        }

        popup.SetValue(OwnerProperty, control);
        popup.SetValue(WantedProperty, new Point(popup.HorizontalOffset, popup.VerticalOffset));
        Place(popup);

        // The control rewrites its placement on every open, so setting this once at
        // template time is not enough. After the open is too late, since a popup does not
        // move once positioned, so it is restored the moment the control writes it.
        popup.PropertyChanged -= OnPopupChanged;
        popup.PropertyChanged += OnPopupChanged;
    }

    private static void OnPopupChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Popup popup)
        {
            return;
        }

        if (e.Property == Popup.PlacementProperty
            && e.GetNewValue<PlacementMode>() != Side(popup))
        {
            Place(popup);
        }
        else if (e.Property == Popup.HorizontalOffsetProperty
                 && e.GetNewValue<double>() != Pull(popup))
        {
            popup.SetCurrentValue(e.Property, Pull(popup));
        }
        else if (e.Property == Popup.VerticalOffsetProperty
                 && e.GetNewValue<double>() != popup.GetValue(WantedProperty).Y)
        {
            popup.SetCurrentValue(e.Property, popup.GetValue(WantedProperty).Y);
        }
    }

    /// <summary>Which edge this popup lines up with, read off the control it belongs to.</summary>
    private static PlacementMode Side(Popup popup) =>
        (popup.GetValue(OwnerProperty) ?? popup.PlacementTarget) is { } target && GetAlignsRight(target)
            ? PlacementMode.BottomEdgeAlignedRight
            : PlacementMode.BottomEdgeAlignedLeft;

    /// <summary>
    /// The pull that cancels the room. It is a leftward nudge, so lining up with the right
    /// edge needs it the other way or the popup hangs a room's width off the control.
    /// </summary>
    private static double Pull(Popup popup) =>
        Side(popup) is PlacementMode.BottomEdgeAlignedRight
            ? -popup.GetValue(WantedProperty).X
            : popup.GetValue(WantedProperty).X;

    private static void Place(Popup popup)
    {
        var wanted = popup.GetValue(WantedProperty);

        popup.Placement = Side(popup);
        popup.HorizontalOffset = Pull(popup);
        popup.VerticalOffset = wanted.Y;
        popup.PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX
            | PopupPositionerConstraintAdjustment.SlideY
            | PopupPositionerConstraintAdjustment.FlipY;

        // A flyout sets its own target and it is the right one, so it is only replaced
        // when this was attached to the control rather than to the popup's child.
        if (popup.GetValue(OwnerProperty) is { } owner)
        {
            popup.PlacementTarget = owner;
        }
    }
}
