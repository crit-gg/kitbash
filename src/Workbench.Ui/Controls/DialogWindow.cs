using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Workbench.Ui.Controls;

/// <summary>
/// A dialog is a real window rather than an overlay, with the same frame and the same
/// title bar as any other. There is no scrim behind it.
/// </summary>
/// <remarks>
/// It differs from a main window in three ways only. It cannot be resized or
/// minimised, so the title bar keeps its close button alone. It centres on the window
/// that opened it. It stays out of the task bar, because it belongs to that window
/// rather than standing on its own.
/// <para>
/// A dialog lays out its own content. <see cref="DialogFooter"/> is the row its
/// actions sit in. Size is left to the caller, so a dialog can be measured or fixed.
/// </para>
/// <para>
/// Its buttons carry a <see cref="DialogRole"/> rather than a handler. A roled button
/// closes the dialog and answers for it, so
/// <c>await dialog.ShowDialog&lt;bool&gt;(owner)</c> says which was pressed and the caller
/// wires nothing. Closing any other way, including the title bar's close button, is a no.
/// </para>
/// <para>
/// Enter and Escape are Avalonia's own, not ours. A role sets <c>IsDefault</c> or
/// <c>IsCancel</c> on the button and the framework does the rest, which was measured
/// rather than assumed: Enter presses the accepting button while focus sits in a text
/// field, and Escape presses the cancelling one.
/// </para>
/// <para>
/// Marking the cancelling button with <see cref="Dialog.TakesFocusProperty"/> takes Enter
/// off the accepting one. Measured before that: a dialog that opened with Cancel ready
/// still accepted on Enter, which is the opposite of what marking it was asking for.
/// </para>
/// </remarks>
public class DialogWindow : ChromelessWindow
{
    public DialogWindow()
    {
        CanResize = false;
        CanMinimize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Heard rather than looked up, so a button anywhere in the dialog answers, not
        // only one the footer happens to hold.
        AddHandler(Button.ClickEvent, OnClick);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var ready = Marked();

        // Enter accepts, unless the dialog has said the cancelling button is the one that
        // is ready. Marking it is a statement that accepting is the dangerous answer, and
        // leaving Enter on it would hand that answer back.
        if (ready is not Button { } marked || GetRoleOf(marked) != DialogRole.Cancel)
        {
            Answer(DialogRole.Accept, button => button.IsDefault = true);
        }

        Answer(DialogRole.Cancel, button => button.IsCancel = true);

        (ready ?? Buttons(DialogRole.Accept).FirstOrDefault())?.Focus(NavigationMethod.Tab);
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button || GetRoleOf(button) is var role && role == DialogRole.None)
        {
            return;
        }

        Close(role == DialogRole.Accept);
        e.Handled = true;
    }

    private void Answer(DialogRole role, Action<Button> wire)
    {
        foreach (var button in Buttons(role))
        {
            wire(button);
        }
    }

    /// <summary>
    /// What the dialog said should be ready, if anything did. Focus goes to it as though
    /// it had been tabbed to, so it wears the halo: a dialog that opens with a button
    /// already answering has to look like it, or the first Enter is a surprise.
    /// </summary>
    private Control? Marked() =>
        this.GetVisualDescendants().OfType<Control>().FirstOrDefault(Dialog.GetTakesFocus);

    private IEnumerable<Button> Buttons(DialogRole role) =>
        this.GetVisualDescendants().OfType<Button>().Where(button => GetRoleOf(button) == role);

    private static DialogRole GetRoleOf(Button button) => Dialog.GetRole(button);
}
