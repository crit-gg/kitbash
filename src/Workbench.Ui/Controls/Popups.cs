using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Controls.Primitives.PopupPositioning;

namespace Workbench.Ui.Controls;

/// <summary>
/// Places a control's popup under it, left aligned, whatever the control asked for.
/// Attach it to the control: <c>ui:Popups.Under="True"</c>.
/// </summary>
/// <remarks>
/// For a control that configures its own popup in code and gets it wrong.
/// <para>
/// Measured on 12.1.1, three popups opened against fields 66px apart in one window. A
/// combo box and a date picker both place with <c>BottomEdgeAlignedLeft</c> against
/// themselves and land 4px under their field, which is the gap the theme asks for. A time
/// picker sets <c>AnchorAndGravity</c> with an anchor and a gravity of Bottom, targets its
/// own flyout button, and lands 445px above the field at the very top of the screen.
/// </para>
/// <para>
/// The placement cannot be fixed from a theme. The control writes it as a local value in
/// <c>OnApplyTemplate</c>, and a style setter binds below that however the selector is
/// written, so the only way to win is to write it later. <c>TemplateApplied</c> is raised
/// after the control's own override has run, which is what this waits for.
/// </para>
/// <para>
/// The offset is the part that actually moved it. Measured: the time picker sets a
/// vertical offset of negative infinity, which pushes the popup off the top of the screen
/// and leaves it clamped there. Placement alone does not undo that, so both are put back.
/// </para>
/// </remarks>
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

    static Popups()
    {
        UnderProperty.Changed.AddClassHandler<TemplatedControl, bool>(OnUnderChanged);
        InPopupProperty.Changed.AddClassHandler<Control, bool>(OnInPopupChanged);
        KeepsWheelProperty.Changed.AddClassHandler<Control, bool>(OnKeepsWheelChanged);
        MatchesTargetProperty.Changed.AddClassHandler<Control, bool>(OnMatchesTargetChanged);
        RoomProperty.Changed.AddClassHandler<Control, bool>(OnRoomChanged);
    }

    /// <summary>
    /// This element is the transparent room a popup's shadow falls into, and a press on it
    /// closes the popup.
    /// </summary>
    /// <remarks>
    /// A popup is a window of its own, and a window is only ever the size of what it
    /// holds, so the room a shadow needs is part of that window. The window is transparent
    /// there but it is still a window, and the platform hands it every click that lands
    /// inside its rectangle.
    /// <para>
    /// That rectangle reaches 18px above the visible overlay, which is more than half the
    /// height of the 26px control it opened from. So a click on the control that opened
    /// the popup lands on the popup instead, light dismiss never sees it, and the popup
    /// will not close.
    /// </para>
    /// <para>
    /// Verified against the backend rather than assumed: Avalonia's X11 popup is an
    /// override redirect window with no input shape and no pointer grab, so nothing makes
    /// the transparent part of it click through.
    /// </para>
    /// <para>
    /// So the room answers for itself. It takes a transparent fill, which is what makes it
    /// hit tested at all, and a press that lands on it rather than on the overlay inside
    /// it closes the popup and stops there. That is what a press outside a popup already
    /// does, and this is outside it in every way except the one the window manager can
    /// see.
    /// </para>
    /// </remarks>
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
    /// <remarks>
    /// A menu or a popover narrower than the button that opened it reads as a mistake, and
    /// the width a person expects is the one they just clicked. It is a floor rather than a
    /// width, so an overlay with more to say is still as wide as it needs to be.
    /// <para>
    /// Set on the element that draws the overlay rather than on the presenter, since the
    /// room a shadow needs sits outside that and should not count towards the width.
    /// </para>
    /// <para>
    /// It is written with SetCurrentValue, so a view that sets a minimum width of its own
    /// keeps it. A context menu does not take this: it belongs to whatever it was opened
    /// on, which may be a whole page.
    /// </para>
    /// </remarks>
    public static readonly AttachedProperty<bool> MatchesTargetProperty =
        AvaloniaProperty.RegisterAttached<Popups, Control, bool>("MatchesTarget");

    public static bool GetMatchesTarget(Control control) => control.GetValue(MatchesTargetProperty);

    public static void SetMatchesTarget(Control control, bool value) =>
        control.SetValue(MatchesTargetProperty, value);

    /// <summary>
    /// A wheel that nothing inside this overlay wanted stops here rather than reaching the
    /// page behind it.
    /// </summary>
    /// <remarks>
    /// A popup is its own window, but its child's logical parent is the popup itself,
    /// which lives in the parent window's tree. So an unhandled wheel routes out of the
    /// popup and into the page behind it.
    /// <para>
    /// Measured: a wheel raised inside the time popover scrolled the page 150px and left
    /// the popover where it was. The same wheel inside a dropdown or a calendar did
    /// nothing, because a scroll viewer in those had already taken it. Nothing was
    /// protecting the page, one popover simply had somewhere for the wheel to land.
    /// </para>
    /// <para>
    /// Bubbling does the deciding. A handled event never reaches this, so anything inside
    /// that wanted the wheel still gets it.
    /// </para>
    /// </remarks>
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

        // The control writes its placement again every time it opens the popup, so once
        // at template time is not enough, and putting it back after the popup is open is
        // too late: measured, the popup does not move once it has been positioned.
        //
        // So it is put back the moment it is written, which is before the open.
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
            && e.GetNewValue<PlacementMode>() != PlacementMode.BottomEdgeAlignedLeft)
        {
            Place(popup);
        }
        else if (e.Property == Popup.HorizontalOffsetProperty
                 && e.GetNewValue<double>() != popup.GetValue(WantedProperty).X)
        {
            popup.SetCurrentValue(e.Property, popup.GetValue(WantedProperty).X);
        }
        else if (e.Property == Popup.VerticalOffsetProperty
                 && e.GetNewValue<double>() != popup.GetValue(WantedProperty).Y)
        {
            popup.SetCurrentValue(e.Property, popup.GetValue(WantedProperty).Y);
        }
    }

    private static void Place(Popup popup)
    {
        var wanted = popup.GetValue(WantedProperty);

        popup.Placement = PlacementMode.BottomEdgeAlignedLeft;
        popup.HorizontalOffset = wanted.X;
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
