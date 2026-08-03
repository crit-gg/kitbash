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
public class Dialog
{
    public static readonly AttachedProperty<DialogRole> RoleProperty =
        AvaloniaProperty.RegisterAttached<Dialog, Button, DialogRole>("Role");

    /// <summary>
    /// This is the control that is ready when the dialog opens. Without it the accepting
    /// button is, so a dialog that only confirms needs to say nothing.
    /// </summary>
    public static readonly AttachedProperty<bool> TakesFocusProperty =
        AvaloniaProperty.RegisterAttached<Dialog, Control, bool>("TakesFocus");

    public static DialogRole GetRole(Button button) => button.GetValue(RoleProperty);

    public static void SetRole(Button button, DialogRole value) => button.SetValue(RoleProperty, value);

    public static bool GetTakesFocus(Control control) => control.GetValue(TakesFocusProperty);

    public static void SetTakesFocus(Control control, bool value) =>
        control.SetValue(TakesFocusProperty, value);
}
