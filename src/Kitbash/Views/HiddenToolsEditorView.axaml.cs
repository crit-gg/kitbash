using Avalonia.Controls;

namespace Kitbash.Views;

/// <summary>
/// Which found tools the Open in button offers, drawn inside the settings window. It is
/// reached through the template in <c>App.axaml</c>, since the window presents an editor
/// as content.
/// </summary>
public partial class HiddenToolsEditorView : UserControl
{
    public HiddenToolsEditorView()
    {
        InitializeComponent();
    }
}
