using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Workbench.Ui.Controls;
using Workbench.ViewModels;

namespace Workbench.Views;

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

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder",
            AllowMultiple = false,
            SuggestedStartLocation = await StartingAtAsync(model.Folder),
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            model.Folder = path;
        }
    }

    /// <summary>
    /// The folder the picker opens in, or null to leave it to the platform. A path that is
    /// not there answers null rather than failing.
    /// </summary>
    private async Task<IStorageFolder?> StartingAtAsync(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        try
        {
            return await StorageProvider.TryGetFolderFromPathAsync(folder.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or IOException
            or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
