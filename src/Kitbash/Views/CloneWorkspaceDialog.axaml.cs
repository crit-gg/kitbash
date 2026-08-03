using Avalonia.Interactivity;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;

namespace Kitbash.Views;

/// <summary>
/// Takes a repository address and a folder, and clones one into the other. It closes only
/// once the clone has finished, so <see cref="Root"/> is a folder that is really there.
/// </summary>
public partial class CloneWorkspaceDialog : DialogWindow
{
    public CloneWorkspaceDialog()
    {
        InitializeComponent();
    }

    /// <summary>Where the clone landed. Null unless the dialog answered yes.</summary>
    public string? Root => Model?.Root;

    private CloneWorkspaceViewModel? Model => DataContext as CloneWorkspaceViewModel;

    private async void OnCloneClick(object? sender, RoutedEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }

        if (await model.CloneAsync())
        {
            Close(true);
        }
    }

    // Stops a clone that is running. Only an idle dialog closes, so a person cannot walk
    // away from a clone that is still writing files.
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (Model is { IsCloning: true } model)
        {
            model.Cancel();

            return;
        }

        Close(false);
    }

}
