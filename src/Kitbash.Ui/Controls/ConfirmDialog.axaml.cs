namespace Kitbash.Ui.Controls;

/// <summary>How much is at stake, which decides the glyph and which answer is ready.</summary>
public enum ConfirmWeight
{
    /// <summary>An ordinary decision. Accepting is ready, so Enter answers yes.</summary>
    Ordinary,

    /// <summary>
    /// Something that cannot be undone. The accepting button is red and cancelling is the
    /// one that is ready, so a keypress cannot answer yes.
    /// </summary>
    Grave,
}

/// <summary>
/// One question with two answers. <c>ShowDialog</c> answers true when it was accepted.
/// </summary>
public partial class ConfirmDialog : DialogWindow
{
    /// <summary>
    /// The generated InitializeComponent, since that is what assigns the named fields.
    /// AvaloniaXamlLoader.Load builds the tree and leaves every one of them null.
    /// </summary>
    public ConfirmDialog() => InitializeComponent();

    /// <summary>
    /// Builds one. The title is the window's, the heading is the question, and the detail
    /// is the sentence under it, which may be empty.
    /// </summary>
    /// <param name="accept">What the accepting button says, as the verb it does.</param>
    public static ConfirmDialog For(
        string title,
        string heading,
        string detail = "",
        string accept = "Continue",
        ConfirmWeight weight = ConfirmWeight.Ordinary)
    {
        var dialog = new ConfirmDialog { Title = title };
        var grave = weight == ConfirmWeight.Grave;

        dialog.Heading.Text = heading;
        dialog.Detail.Text = detail;
        dialog.Detail.IsVisible = !string.IsNullOrWhiteSpace(detail);

        dialog.Mark.Glyph = grave ? IconGlyph.AlertTriangle : IconGlyph.InfoCircle;
        dialog.Mark.Classes.Set("grave", grave);

        dialog.Accept.Content = accept;
        dialog.Accept.Classes.Set("primary", !grave);
        dialog.Accept.Classes.Set("danger", grave);

        Dialog.SetTakesFocus(grave ? dialog.Refuse : dialog.Accept, true);

        return dialog;
    }
}
