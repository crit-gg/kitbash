using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Workbench.Ui.Controls;

/// <summary>
/// Lets a number be dragged rather than typed. Attach it to a <see cref="NumericUpDown"/>:
/// <c>ui:Scrub.Drags="True"</c>, which the spinbox theme does for every one.
/// </summary>
public class Scrub
{
    /// <summary>How far the pointer moves before this is a drag rather than a click.</summary>
    private const double Threshold = 3;

    /// <summary>Pixels per step. Roughly a value per four pixels of travel.</summary>
    private const double Pace = 4;

    public static readonly AttachedProperty<bool> DragsProperty =
        AvaloniaProperty.RegisterAttached<Scrub, NumericUpDown, bool>("Drags");

    /// <summary>Where the press landed and what the value was, held on the control itself.</summary>
    private static readonly AttachedProperty<Point?> FromProperty =
        AvaloniaProperty.RegisterAttached<Scrub, NumericUpDown, Point?>("From");

    private static readonly AttachedProperty<decimal?> StartedAtProperty =
        AvaloniaProperty.RegisterAttached<Scrub, NumericUpDown, decimal?>("StartedAt");

    private static readonly AttachedProperty<bool> DraggingProperty =
        AvaloniaProperty.RegisterAttached<Scrub, NumericUpDown, bool>("Dragging");

    static Scrub()
    {
        DragsProperty.Changed.AddClassHandler<NumericUpDown, bool>(OnDragsChanged);
    }

    public static bool GetDrags(NumericUpDown control) => control.GetValue(DragsProperty);

    public static void SetDrags(NumericUpDown control, bool value) => control.SetValue(DragsProperty, value);

    private static void OnDragsChanged(NumericUpDown control, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        control.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        control.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        control.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);

        if (!change.GetNewValue<bool>())
        {
            return;
        }

        control.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
    }

    private static void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not NumericUpDown control || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // The stepper is a button and keeps its own presses.
        if (e.Source is Button)
        {
            return;
        }

        control.SetValue(FromProperty, e.GetPosition(control));
        control.SetValue(StartedAtProperty, control.Value);
        control.SetValue(DraggingProperty, false);
    }

    private static void OnMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not NumericUpDown control
            || control.GetValue(FromProperty) is not { } from
            || control.GetValue(StartedAtProperty) is not { } started)
        {
            return;
        }

        var moved = e.GetPosition(control).X - from.X;

        if (!control.GetValue(DraggingProperty))
        {
            if (Math.Abs(moved) < Threshold)
            {
                return;
            }

            control.SetValue(DraggingProperty, true);
            control.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        }

        var steps = (decimal)Math.Truncate(moved / Pace);
        var wanted = started + (steps * control.Increment);

        control.Value = Math.Clamp(wanted, control.Minimum, control.Maximum);

        // Taken from the field, so the drag does not also select text.
        e.Handled = true;
    }

    private static void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not NumericUpDown control)
        {
            return;
        }

        // Only swallow the release that ended a drag. A plain click has to reach the
        // field so the caret lands where it was aimed.
        if (control.GetValue(DraggingProperty))
        {
            control.Cursor = Cursor.Default;
            e.Handled = true;
        }

        control.ClearValue(FromProperty);
        control.ClearValue(StartedAtProperty);
        control.ClearValue(DraggingProperty);
    }
}
