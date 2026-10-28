using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Kitbash.Views;

/// <summary>
/// Runs a second command in place of a button's own when it is clicked with Shift held.
/// Works on a <see cref="Button"/> or a <see cref="SplitButton"/>, whose main half it reaches.
/// </summary>
public class ShiftClick : AvaloniaObject
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<ShiftClick, Control, ICommand?>("Command");

    private static readonly AttachedProperty<bool> HeldProperty =
        AvaloniaProperty.RegisterAttached<ShiftClick, Control, bool>("Held");

    static ShiftClick()
    {
        CommandProperty.Changed.AddClassHandler<Control, ICommand?>(OnCommandChanged);
    }

    public static void SetCommand(Control control, ICommand? value) => control.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(Control control) => control.GetValue(CommandProperty);

    private static void OnCommandChanged(Control control, AvaloniaPropertyChangedEventArgs<ICommand?> change)
    {
        var click = ClickOf(control);

        control.RemoveHandler(InputElement.PointerReleasedEvent, OnPointer);
        control.RemoveHandler(InputElement.KeyDownEvent, OnKey);
        control.RemoveHandler(InputElement.KeyUpEvent, OnKey);
        control.RemoveHandler(click, OnClick);

        if (change.GetNewValue<ICommand?>() is null)
        {
            return;
        }

        // Tunnelled, so the modifiers are read before the button inside turns the input
        // into a click. A click carries no modifiers of its own.
        control.AddHandler(InputElement.PointerReleasedEvent, OnPointer, RoutingStrategies.Tunnel, handledEventsToo: true);
        control.AddHandler(InputElement.KeyDownEvent, OnKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        control.AddHandler(InputElement.KeyUpEvent, OnKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        control.AddHandler(click, OnClick);
    }

    // A split button raises a click event of its own, and runs its command only when that
    // click is left unhandled.
    private static RoutedEvent<RoutedEventArgs> ClickOf(Control control) =>
        control is SplitButton ? SplitButton.ClickEvent : Button.ClickEvent;

    private static void OnPointer(object? sender, PointerReleasedEventArgs e) => Hold(sender, e.KeyModifiers);

    private static void OnKey(object? sender, KeyEventArgs e) => Hold(sender, e.KeyModifiers);

    private static void Hold(object? sender, KeyModifiers modifiers)
    {
        if (sender is Control control)
        {
            control.SetValue(HeldProperty, modifiers.HasFlag(KeyModifiers.Shift));
        }
    }

    private static void OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        var held = control.GetValue(HeldProperty);

        control.SetValue(HeldProperty, false);

        if (held && GetCommand(control) is { } command && command.CanExecute(null))
        {
            e.Handled = true;
            command.Execute(null);
        }
    }
}
