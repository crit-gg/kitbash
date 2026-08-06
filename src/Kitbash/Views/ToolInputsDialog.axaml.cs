using Avalonia.Controls;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>
/// Asks for what a script needs before it runs. The answers become its arguments.
/// </summary>
public partial class ToolInputsDialog : DialogWindow
{
    /// <summary>
    /// The generated InitializeComponent, since that is what assigns the named fields.
    /// </summary>
    public ToolInputsDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the form over its owner and answers whether Run was pressed. What was typed
    /// stays on the view model, so a caller reads it from there.
    /// </summary>
    public static Task<bool> AskAsync(Window owner, ToolInputsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(model);

        var dialog = new ToolInputsDialog { Title = model.Title, DataContext = model };

        return dialog.ShowDialog<bool>(owner);
    }
}
