using Avalonia.Controls;

namespace Kitbash.Views;

/// <summary>
/// What each installed tool does to the launcher, drawn inside the settings window. It is
/// reached through the template in <c>App.axaml</c>, since the window presents an editor
/// as content.
/// </summary>
public partial class ToolActionsEditorView : UserControl
{
    public ToolActionsEditorView()
    {
        InitializeComponent();
    }
}
