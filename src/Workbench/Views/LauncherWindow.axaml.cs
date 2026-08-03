using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Workbench.Ui.Controls;
using Workbench.ViewModels;

namespace Workbench.Views;

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

    private LauncherViewModel? Model => DataContext as LauncherViewModel;

    /// <summary>
    /// Builds the workspace list once, before anyone asks for it.
    /// </summary>
    /// <remarks>
    /// A flyout is a window of its own, and the first one a process opens costs far more
    /// than the rest. Measured on this window with a 4ms heartbeat on the UI thread: the
    /// first open held the thread for 78ms and later opens for 5 to 18. Roughly 60 of that
    /// 78 is machinery any popup would pay for, and the rest is this one's rows, its badge
    /// and its icons being built for the first time.
    /// <para>
    /// So it is paid here instead, where the window is up and nobody is waiting on it. The
    /// same window measured both ways, first open: 70.6ms of ShowAt and a 75.7ms gap without
    /// this, 14.8ms and 37.3ms with it. What is left is the new window's first composite,
    /// which cannot be paid in advance because the warmed one was closed again.
    /// </para>
    /// <para>
    /// The presenter is held at zero opacity while this happens. Showing and hiding inside
    /// one dispatcher frame probably never reaches the screen, but probably is not good
    /// enough for something that would read as a flicker at every launch, and layout is what
    /// costs the time rather than the paint.
    /// </para>
    /// </remarks>
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

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path && Model is { } model)
        {
            await model.AddWorkspaceAsync(path);
        }
    }
}
