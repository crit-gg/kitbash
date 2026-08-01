using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using Avalonia.Interactivity;
using Workbench.ViewModels;

namespace Workbench.Views;

public partial class LauncherWindow : ChromelessWindow
{
    public LauncherWindow() => InitializeComponent();

    private LauncherViewModel? Model => DataContext as LauncherViewModel;

    private void OnWorkspacePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: WorkspaceViewModel workspace })
        {
            Model?.SwitchTo(workspace);
        }

        e.Handled = true;
    }

    private async void OnAddWorkspacePressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        await AddWorkspaceFromFolderAsync();
    }

    private async void OnChooseFolderClick(object? sender, RoutedEventArgs e) =>
        await AddWorkspaceFromFolderAsync();

    private async Task AddWorkspaceFromFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add workspace",
            AllowMultiple = false,
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            Model?.AddWorkspace(path);
        }
    }

    private void OnToggleWorkspacesPressed(object? sender, PointerPressedEventArgs e)
    {
        Model?.ToggleWorkspacesCommand.Execute(null);
        e.Handled = true;
    }

    // Clicking the scrim closes the list. The list itself sits above it and
    // swallows its own clicks.
    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender))
        {
            Model?.CloseWorkspacesCommand.Execute(null);
        }

        e.Handled = true;
    }
}
