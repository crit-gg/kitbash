using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Kitbash.Tools;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Kitbash.Updates;
using Kitbash.ViewModels;

namespace Kitbash.Views;

public partial class LauncherWindow : ChromelessWindow
{
    public LauncherWindow()
    {
        InitializeComponent();

        // Nothing behind the git strip runs while the window is not in front, and coming
        // back reads at once. Editors do this for the same reason: the time the app was
        // away is exactly when something is most likely to have changed, and the time it
        // spends away is exactly when running git buys nothing.
        Activated += (_, _) => Model?.SetActive(true);
        Deactivated += (_, _) => Model?.SetActive(false);

        // The host draws whatever service it is handed, and the service belongs to whoever
        // raised the toast. The window's own comes with the view model rather than being
        // resolved here, since the window is built without a container.
        // A dialog belongs to a window and a view model has none, so the two things the
        // strip cannot do for itself are handed over here. Same shape as the engines page.
        DataContextChanged += (_, _) =>
        {
            Toasts.Service = Model?.Engines.Toasts;

            if (Model is { } model)
            {
                model.Launching ??= (project, mode, work) =>
                    LaunchDialog.RunAsync(this, project, mode, work);


                model.Failed ??= (failure, mode) => LaunchFailedDialog.Show(this, failure, mode);
            }
        };
    }

    /// <summary>Where the rail's Settings item goes. Handed over by the composition root.</summary>
    public ISettingsWindows? Settings { get; init; }

    /// <summary>
    /// What the title bar reads out beside the name. Set here rather than bound, since a
    /// version does not change while the window is open: an update restarts the app.
    /// </summary>
    public IApplicationVersion? Version
    {
        init => TitleBar.Version = value?.Current;
    }

    private LauncherViewModel? Model => DataContext as LauncherViewModel;

    // The item is a way in rather than a page, so the mark goes straight back off it and
    // the workspace or engines page stays the one the rail says is open.
    private void OnSettingsSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (RailFoot.SelectedIndex < 0)
        {
            return;
        }

        RailFoot.SelectedIndex = -1;
        Settings?.Open(this);
    }

    /// <summary>
    /// Builds the workspace list once, before anyone asks for it.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (WorkspaceSelector.Flyout is not Flyout flyout)
        {
            return;
        }

        flyout.FlyoutPresenterClasses.Add("warming");

        try
        {
            flyout.ShowAt(WorkspaceSelector);
            flyout.Hide();
        }
        finally
        {
            flyout.FlyoutPresenterClasses.Remove("warming");
        }
    }

    // Tapped rather than PointerPressed, so a row behaves the way every button does: the
    // primary button only, and the press and the release both have to land on the row.
    // PointerPressed fired on any button, including the right one, and it fired the moment
    // the button went down, so a press that dragged away still switched.
    private async void OnWorkspaceTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: WorkspaceViewModel workspace } row)
        {
            return;
        }

        // A control inside the row keeps its own taps. Otherwise a tap on the row's own
        // button would switch the workspace behind it as well.
        if (TakesItsOwnTaps(e.Source as Visual, row))
        {
            return;
        }

        e.Handled = true;

        // Closed first, so the list goes the moment it is picked rather than after the disk
        // has answered.
        WorkspaceSelector.Flyout?.Hide();

        if (Model is { } model)
        {
            await model.SwitchToAsync(workspace);
        }
    }

    // A button or anything else focusable is a control and keeps the tap. A label, an icon,
    // a border or a panel is decoration, so the row underneath gets it.
    private static bool TakesItsOwnTaps(Visual? source, Control row)
    {
        for (var visual = source; visual is not null && visual != row; visual = visual.GetVisualParent())
        {
            if (visual is InputElement { Focusable: true })
            {
                return true;
            }
        }

        return false;
    }

    private async void OnNewWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        // The dropdown goes first, so there is one thing on screen at a time and
        // cancelling returns to the launcher rather than to the open list.
        WorkspaceSelector.Flyout?.Hide();

        if (Model is not { } model)
        {
            return;
        }

        var dialog = new NewWorkspaceDialog { DataContext = model.NewWorkspace() };

        if (await dialog.ShowDialog<bool>(this) && dialog.Request is { } request)
        {
            await model.CreateWorkspaceAsync(request, dialog.NeedsInstall);
        }
    }

    private async void OnAddWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        WorkspaceSelector.Flyout?.Hide();
        await AddWorkspaceFromFolderAsync();
    }

    private async void OnCloneWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        WorkspaceSelector.Flyout?.Hide();

        if (Model is not { } model)
        {
            return;
        }

        var dialog = new CloneWorkspaceDialog { DataContext = model.NewClone() };

        // The dialog only says yes once git has finished, so the folder is really there.
        if (await dialog.ShowDialog<bool>(this) && dialog.Root is { } root)
        {
            await model.AddWorkspaceAsync(root);
        }
    }

    // The row's own menu. Assembled here rather than declared with the row, since
    // MenuFlyout takes items or an ItemsSource but not both.
    private void OnWorkspaceMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WorkspaceViewModel workspace } button)
        {
            return;
        }

        var menu = new MenuFlyout();

        menu.Items.Add(Item(
            "Open folder",
            workspace.IsOnDisk,
            () => Model?.OpenFolder(workspace.Workspace.Root)));

        menu.Items.Add(Item("Copy path", enabled: true, () => _ = CopyPathAsync(workspace)));

        menu.Items.Add(Item(
            "Rename workspace",
            workspace.IsOnDisk,
            () => _ = RenameWorkspaceAsync(workspace)));

        // Last, behind a separator, and it names its workspace, so a click on the wrong
        // row is visible before it is confirmed.
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(
            workspace.RemoveLabel,
            enabled: true,
            () => _ = RemoveWorkspaceAsync(workspace),
            danger: true));

        menu.ShowAt(button);
    }

    private static MenuItem Item(string header, bool enabled, Action run, bool danger = false)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };

        if (danger)
        {
            item.Classes.Add("danger");
        }

        item.Click += (_, _) => run();

        return item;
    }

    /// <summary>The real root rather than the shortened one the row draws.</summary>
    private async Task CopyPathAsync(WorkspaceViewModel workspace)
    {
        WorkspaceSelector.Flyout?.Hide();

        // Avalonia 12 replaced SetTextAsync with a format and a value.
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetValueAsync(DataFormat.Text, workspace.Workspace.Root);
        }
    }

    private async Task RenameWorkspaceAsync(WorkspaceViewModel workspace)
    {
        WorkspaceSelector.Flyout?.Hide();

        var dialog = RenameWorkspaceDialog.For(workspace);

        if (await dialog.ShowDialog<bool>(this) && Model is { } model)
        {
            await model.RenameWorkspaceAsync(workspace, dialog.ChosenName);
        }
    }

    private async Task RemoveWorkspaceAsync(WorkspaceViewModel workspace)
    {
        WorkspaceSelector.Flyout?.Hide();

        var dialog = RemoveWorkspaceDialog.For(workspace);

        if (await dialog.ShowDialog<bool>(this) && Model is { } model)
        {
            await model.RemoveWorkspaceAsync(workspace);
        }
    }

    private async void OnChooseFolderClick(object? sender, RoutedEventArgs e) =>
        await AddWorkspaceFromFolderAsync();

    // Every tool is a placeholder today, so opening one reports that it is not built.
    // The result has nowhere to go until the launcher grows an error surface.
    private void OnLaunchToolClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ToolCardViewModel { Tool: { } tool } }
            && DataContext is LauncherViewModel launcher)
        {
            _ = launcher.LaunchToolAsync(tool);
        }
    }

    // A flyout is view furniture, and MenuFlyout takes items or an ItemsSource but not
    // both, so the menu is assembled here rather than declared with the card.
    private void OnToolMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ToolCardViewModel card } button)
        {
            return;
        }

        var menu = new MenuFlyout();

        foreach (var item in card.Menu)
        {
            var entry = new MenuItem { Header = item.Label, InputGesture = Gesture(item.Hint) };

            entry.Click += (_, _) => item.Invoke();

            menu.Items.Add(entry);
        }

        // Last, behind a separator, and it names its tool, so a click on the wrong card is
        // visible before it is confirmed.
        if (card.Tool is { } tool)
        {
            Separate(menu);
            menu.Items.Add(Item(
                card.RemoveLabel,
                enabled: true,
                () => _ = UninstallToolAsync(tool),
                danger: true));
        }

        menu.ShowAt(button);
    }

    // A folder holding a manifest is a tool wherever it sits, so this is how a build on
    // this machine gets on the page without being published anywhere.
    private async void OnInstallFromFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Install from folder",
            AllowMultiple = false,
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path && Model is { } model)
        {
            await model.InstallToolFromFolderAsync(path);
        }
    }

    private async Task UninstallToolAsync(InstalledTool tool)
    {
        if (Model is not { } model)
        {
            return;
        }

        // Walking a directory is a disk read, so it happens before the dialog is built
        // rather than while the UI thread is drawing it.
        var size = await Task.Run(() => model.SizeOnDisk(tool));

        if (await UninstallDialog.For(tool, size).ShowDialog<bool>(this))
        {
            await model.UninstallToolAsync(tool);
        }
    }

    private static void Separate(MenuFlyout menu)
    {
        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new Separator());
        }
    }

    /// <summary>
    /// The shortcut a menu line shows, written the way a person says it. Null draws no
    /// hint, and so does anything that is not a gesture, rather than taking the app down.
    /// </summary>
    private static KeyGesture? Gesture(string? hint)
    {
        if (hint is null)
        {
            return null;
        }

        try
        {
            return KeyGesture.Parse(hint.Replace(' ', '+'));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return null;
        }
    }

    private async Task AddWorkspaceFromFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add workspace",
            AllowMultiple = false,
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path && Model is { } model)
        {
            await model.AddWorkspaceAsync(path);
        }
    }
}
