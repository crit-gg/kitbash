using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A dialog is a real window rather than an overlay, with the same frame and the same
/// title bar as any other. There is no scrim behind it.
/// </summary>
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

    /// <summary>
    /// Which button closed the dialog. It stays at cancel when the dialog was dismissed
    /// some other way, so a caller reading it never has to guard against a third state.
    /// </summary>
    public DialogRole Chose { get; private set; } = DialogRole.Cancel;

    /// <summary>
    /// Opens the dialog wearing the same frame as the window that owns it. Setting it here
    /// rather than reading Owner, because a window is given its owner after its first layout
    /// pass, which is already too late to change the template or the size.
    /// </summary>
    public Task<TResult> ShowFor<TResult>(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (owner is ChromelessWindow framed)
        {
            Chrome = framed.Chrome;
        }

        return ShowDialog<TResult>(owner);
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

        Chose = role;

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
