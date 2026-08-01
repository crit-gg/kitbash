using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Workbench.ViewModels;

namespace Workbench.Views;

public partial class LauncherWindow : ChromelessWindow
{
    public LauncherWindow() => InitializeComponent();

    private LauncherViewModel? Model => DataContext as LauncherViewModel;

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e) => BeginMoveWindow(e);

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleMaximized();
        e.Handled = true;
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

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
