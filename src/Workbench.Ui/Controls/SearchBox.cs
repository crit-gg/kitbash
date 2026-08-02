using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Workbench.Ui.Controls;

/// <summary>
/// A text field that filters something. It carries a search mark and a way to empty
/// itself, and it is a <see cref="TextBox"/> in every other respect.
/// </summary>
/// <remarks>
/// This exists for the clear button and nothing else. A mark and a radius would be a class
/// on the text field theme, but emptying the field is behaviour, and a theme cannot carry
/// behaviour. The button appears only once there is something to clear, which the theme
/// reads off the <c>:empty</c> pseudo class the text box already sets.
/// <para>
/// The button is listened for rather than looked up. It lives in the theme's inner right
/// content, which is materialised into a presenter and is not a template child, so it is
/// in no template name scope and <c>OnApplyTemplate</c> cannot find it. A click bubbles,
/// so the field hears it wherever the button actually sits.
/// </para>
/// </remarks>
public class SearchBox : TextBox
{
    private const string ClearPart = "PART_Clear";

    public SearchBox()
    {
        AddHandler(Button.ClickEvent, OnClick);
    }

    // Focus goes back to the field, since clearing a filter is almost always followed by
    // typing another one.
    private void OnClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button { Name: ClearPart })
        {
            return;
        }

        Clear();
        Focus();
        e.Handled = true;
    }
}
