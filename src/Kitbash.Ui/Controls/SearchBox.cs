using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A text field that filters something. It carries a search mark and a way to empty
/// itself, and it is a <see cref="TextBox"/> in every other respect.
/// </summary>
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
