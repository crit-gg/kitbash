using Avalonia;
using Avalonia.Controls;

namespace Workbench.Ui.Controls;

/// <summary>What a dialog's button means, as opposed to what it says.</summary>
public enum DialogRole
{
    /// <summary>An ordinary button. The dialog does nothing with it.</summary>
    None,

    /// <summary>Closes the dialog and says yes. Enter presses it.</summary>
    Accept,

    /// <summary>Closes the dialog and says no. Escape presses it.</summary>
    Cancel,
}

/// <summary>
/// Marks what a dialog's buttons are for, and which one is ready when it opens.
/// </summary>
/// <remarks>
/// A role rather than a label, so a dialog can say Remove, Discard or Replace and still be
/// answered with Enter, closed with Escape, and read as a yes or a no by whoever opened it.
/// <see cref="DialogWindow"/> is what acts on these.
/// <para>
/// A button with no role is an ordinary button and the dialog leaves it alone, which is how
/// a third answer such as Don't save is written.
/// </para>
/// </remarks>
public class Dialog
{
    public static readonly AttachedProperty<DialogRole> RoleProperty =
        AvaloniaProperty.RegisterAttached<Dialog, Button, DialogRole>("Role");

    /// <summary>
    /// This is the control that is ready when the dialog opens. Without it the accepting
    /// button is, so a dialog that only confirms needs to say nothing.
    /// </summary>
    /// <remarks>
    /// Mark the cancelling button instead when accepting is the destructive answer, so a
    /// held Enter or a stray space cannot delete anything.
    /// </remarks>
    public static readonly AttachedProperty<bool> TakesFocusProperty =
        AvaloniaProperty.RegisterAttached<Dialog, Control, bool>("TakesFocus");

    public static DialogRole GetRole(Button button) => button.GetValue(RoleProperty);

    public static void SetRole(Button button, DialogRole value) => button.SetValue(RoleProperty, value);

    public static bool GetTakesFocus(Control control) => control.GetValue(TakesFocusProperty);

    public static void SetTakesFocus(Control control, bool value) =>
        control.SetValue(TakesFocusProperty, value);
}
