using Avalonia.Controls;

namespace Kitbash.Views;

/// <summary>
/// The custom tool list, drawn inside the settings window. It is reached through the
/// template in <c>App.axaml</c>, since the window presents an editor as content.
/// </summary>
public partial class CustomToolsEditorView : UserControl
{
    public CustomToolsEditorView()
    {
        InitializeComponent();
    }
}
