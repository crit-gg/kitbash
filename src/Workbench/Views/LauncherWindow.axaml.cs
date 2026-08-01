using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Workbench.Ui.Controls;
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
            WorkspaceSelector.Flyout?.Hide();
        }

        e.Handled = true;
    }

    private async void OnAddWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        WorkspaceSelector.Flyout?.Hide();
        await AddWorkspaceFromFolderAsync();
    }

    private async void OnChooseFolderClick(object? sender, RoutedEventArgs e) =>
        await AddWorkspaceFromFolderAsync();

    // Every tool is a placeholder today, so opening one reports that it is not built.
    // The result has nowhere to go until the launcher grows an error surface.
    private void OnLaunchToolClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ToolCardViewModel card })
        {
            _ = card.Tool.Activation.ActivateAsync();
        }
    }

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
}
