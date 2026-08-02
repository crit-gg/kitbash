using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    }

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
