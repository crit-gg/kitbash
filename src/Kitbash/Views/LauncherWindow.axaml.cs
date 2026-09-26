using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Core.Godot;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Tools;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Kitbash.Ui.Toasts;
using Kitbash.Updates;
using Kitbash.ViewModels;

namespace Kitbash.Views;

public partial class LauncherWindow : ChromelessWindow
{
    private readonly ISettingsWindows? _settings;

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

                model.AskingInputs ??= form => ToolInputsDialog.AskAsync(this, form);

                model.RunningScript ??= (subject, work) => ToolRunDialog.RunAsync(this, subject, work);

                model.RevealTool ??= RevealTool;

                model.OpenIn.PropertyChanged += OnOpenInChanged;
                FillOpenIn();
            }
        };
    }

    /// <summary>
    /// Rebuilds the Open in menu when the workspace changes. It is filled as soon as the
    /// rows are known rather than when the flyout opens, since a presenter that has already
    /// been built does not pick up items added on the way open.
    /// </summary>
    private void OnOpenInChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OpenInViewModel.Rows))
        {
            FillOpenIn();
        }
    }

    // A flyout is not in the window's name scope, so it is reached through its button.
    private void FillOpenIn()
    {
        if (OpenInButton.Flyout is MenuFlyout menu)
        {
            OpenIn?.Fill(menu, Model?.OpenIn.Rows ?? []);
        }
    }

    /// <summary>Where the rail's Settings item goes. Handed over by the composition root.</summary>
    public ISettingsWindows? Settings
    {
        get => _settings;
        init
        {
            _settings = value;

            // The menu is gathered rather than watched, so hiding a tool or adding one
            // shows up when the window that changed it closes.
            if (value is { } windows)
            {
                windows.Closed += (_, _) => _ = Model?.RefreshOpenInAsync();
            }
        }
    }

    /// <summary>Builds the Open in menu's controls. Handed over by the composition root.</summary>
    public OpenInMenu? OpenIn { get; init; }

    /// <summary>
    /// The repository list a link may add to. Handed over by the composition root, since
    /// the window is built without a container.
    /// </summary>
    public IToolRepositoryList? Repositories { get; init; }

    /// <summary>The engine repository list a link may add to, handed over the same way.</summary>
    public IEngineRepositoryList? EngineRepositories { get; init; }

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

        if (await dialog.ShowFor<bool>(this) && dialog.Request is { } request)
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
        if (await dialog.ShowFor<bool>(this) && dialog.Root is { } root)
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

    /// <summary>
    /// The name opens its workspace, which is the one thing on the workspaces page that
    /// switches. A folder that has gone has a disabled name and never reaches this.
    /// </summary>
    private void OnOpenWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WorkspaceRowViewModel row })
        {
            _ = Model?.OpenWorkspaceAsync(row.Workspace);
        }
    }

    /// <summary>
    /// Everything this workspace can be opened in, which was gathered when its row was
    /// read. A menu is built per press rather than kept, since a presenter already built
    /// does not pick up items added later.
    /// </summary>
    private void OnRowOpenInClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WorkspaceRowViewModel row } button)
        {
            return;
        }

        var menu = new MenuFlyout();

        OpenIn?.Fill(menu, row.OpenIn.Rows);
        menu.ShowAt(button);
    }

    /// <summary>
    /// The tools this workspace provides. Each one opens against this workspace rather
    /// than against whichever one is open.
    /// </summary>
    private void OnRowToolsClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WorkspaceRowViewModel row } button)
        {
            return;
        }

        var menu = new MenuFlyout();

        foreach (var tool in row.Tools)
        {
            menu.Items.Add(Item(
                tool.Name,
                enabled: true,
                () => _ = Model?.LaunchToolAsync(tool, row.Root)));
        }

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

        if (await dialog.ShowFor<bool>(this) && Model is { } model)
        {
            await model.RenameWorkspaceAsync(workspace, dialog.ChosenName);
        }
    }

    private async Task RemoveWorkspaceAsync(WorkspaceViewModel workspace)
    {
        WorkspaceSelector.Flyout?.Hide();

        var dialog = RemoveWorkspaceDialog.For(workspace);

        if (await dialog.ShowFor<bool>(this) && Model is { } model)
        {
            await model.RemoveWorkspaceAsync(workspace);
        }
    }

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

        if (await UninstallDialog.For(tool, size).ShowFor<bool>(this))
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

    /// <summary>
    /// Does what a kitbash link asks for. Every verb ends in something on screen, and none
    /// of them clones, installs or runs anything without a person pressing a button.
    /// </summary>
    public async Task FollowAsync(DeepLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        switch (link.Verb)
        {
            case "clone":
                await CloneFromLinkAsync(link.Value("repo"));
                break;

            case "settings":
                Settings?.Open(this, link.First);
                break;

            case "engine":
                await ShowEngineFromLinkAsync(link.First);
                break;

            case "tool":
                await ShowToolFromLinkAsync(link.First);
                break;

            case "tools" when Names(link.First, "repository"):
                await AddRepositoryFromLinkAsync(link.Value("add"));
                break;

            case "engines" when Names(link.First, "repository"):
                await AddEngineRepositoryFromLinkAsync(link.Value("add"), link.Value("name"));
                break;

            default:
                // The link itself is not read back out, since it came from a web page and
                // a toast is not the place to put whatever it says.
                Say(ToastTier.Warn, "Kitbash does not know that link");
                break;
        }
    }

    /// <summary>
    /// Opens the clone dialog with the address a link carried already in it. Nothing is
    /// cloned until the dialog is answered.
    /// </summary>
    private async Task CloneFromLinkAsync(string? address)
    {
        if (Model is not { } model)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            Say(ToastTier.Warn, "That link did not name a repository");

            return;
        }

        var clone = model.NewClone();

        // Whatever the link carried, so a person sees it before git does. The dialog
        // refuses to run until GitRemote accepts it, which is what keeps this safe.
        clone.Address = address;

        var dialog = new CloneWorkspaceDialog { DataContext = clone };

        if (await dialog.ShowFor<bool>(this) && dialog.Root is { } root)
        {
            await model.AddWorkspaceAsync(root);
        }
    }

    private async Task ShowEngineFromLinkAsync(string? version)
    {
        if (Model is not { } model)
        {
            return;
        }

        if (!EngineVersionPattern.TryParse(version, out var wanted))
        {
            Say(ToastTier.Warn, "That link did not name a Godot version");

            return;
        }

        await model.ShowEngineAsync(wanted);
    }

    private async Task ShowToolFromLinkAsync(string? id)
    {
        if (Model is not { } model)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            Say(ToastTier.Warn, "That link did not name a tool");

            return;
        }

        if (!await model.ShowToolAsync(id))
        {
            Say(
                ToastTier.Warn,
                $"No tool here is called {Short(id)}",
                "A repository offering it has to be on the list first.");
        }
    }

    /// <summary>
    /// Adds a repository a link named, once a person has said so. This is how a program
    /// gets onto the machine, so the dialog is the whole point of it.
    /// </summary>
    private async Task AddRepositoryFromLinkAsync(string? address)
    {
        if (Model is not { } model || Repositories is not { } repositories)
        {
            return;
        }

        if (Address(address) is not { } url)
        {
            Say(
                ToastTier.Warn,
                "That link did not name a repository",
                "A repository address is http or https.");

            return;
        }

        // Both lists, since a workspace already offering it makes this nothing to do.
        // Reading them touches a disk, so it happens before the dialog is built.
        var listed = await Task.Run(repositories.Read);

        if (listed.Any(source => source.Url == url))
        {
            Say(ToastTier.Info, "That repository is already on the list");

            return;
        }

        if (!await AddRepositoryDialog.For(url).ShowFor<bool>(this))
        {
            return;
        }

        try
        {
            await Task.Run(() => repositories.WriteGlobal(
                [.. repositories.ReadGlobal(), new ToolRepositorySource(ToolRepositorySource.GitHub, url, string.Empty)]));
        }
        catch (SettingsFileUnreadableException)
        {
            Say(
                ToastTier.Error,
                "The global config would not parse, so nothing was added",
                "Fix it by hand and follow the link again.");

            return;
        }

        await model.RefreshToolsAsync(refresh: true);

        Say(ToastTier.Ok, $"Added {url.Value.Host}", "Whatever it offers is on the page now.");
    }

    /// <summary>
    /// Adds an engine repository a link named, once a person has said so. A workspace names
    /// it by <paramref name="name"/>, which defaults to the repository's own name.
    /// </summary>
    private async Task AddEngineRepositoryFromLinkAsync(string? address, string? name)
    {
        if (Model is not { } model || EngineRepositories is not { } repositories)
        {
            return;
        }

        if (Address(address) is not { } url || EngineRepositoryAddress.ForGitHub(url) is not { } where)
        {
            Say(
                ToastTier.Warn,
                "That link did not name an engine repository",
                "It has to be a repository on github.com.");

            return;
        }

        var called = string.IsNullOrWhiteSpace(name) ? where.Name : name.Trim();
        var listed = await Task.Run(repositories.ReadGlobal);

        if (listed.Any(source => source.Address == where))
        {
            Say(ToastTier.Info, "That engine repository is already on the list");

            return;
        }

        if (listed.Any(source => string.Equals(source.Name, called, StringComparison.OrdinalIgnoreCase)))
        {
            Say(
                ToastTier.Warn,
                $"An engine repository is already called {Short(called)}",
                "Rename one in the settings window and follow the link again.");

            return;
        }

        if (!await AddRepositoryDialog.ForEngines(url, called).ShowFor<bool>(this))
        {
            return;
        }

        try
        {
            await Task.Run(() => repositories.WriteGlobal(
                [.. listed, new EngineRepositorySource(called, where, url, string.Empty)]));
        }
        catch (SettingsFileUnreadableException)
        {
            Say(
                ToastTier.Error,
                "The global config would not parse, so nothing was added",
                "Fix it by hand and follow the link again.");

            return;
        }

        await model.Engines.LoadAsync();

        Say(ToastTier.Ok, $"Added {called}", "Its builds are on the engines page now.");
    }

    /// <summary>
    /// Scrolls a tool card into view. Neither of the two lists holding cards virtualises,
    /// so every container is real and the visual tree is where one is found.
    /// </summary>
    private void RevealTool(ToolCardViewModel card) =>
        Dispatcher.UIThread.Post(
            () => Container(ToolGroups, card)?.BringIntoView(),
            DispatcherPriority.Background);

    private static Control? Container(Visual root, object item)
    {
        if (root is Control control && ReferenceEquals(control.DataContext, item))
        {
            return control;
        }

        foreach (var child in root.GetVisualChildren())
        {
            if (Container(child, item) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Http and https only, which is what WebAddress already refuses anything else for.</summary>
    private static WebAddress? Address(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        try
        {
            return WebAddress.Parse(address.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or UriFormatException)
        {
            return null;
        }
    }

    private static bool Names(string? segment, string word) =>
        string.Equals(segment, word, StringComparison.OrdinalIgnoreCase);

    /// <summary>Enough of a link's own word to recognise it, since a link chose the length.</summary>
    private static string Short(string value) => value.Length <= 48 ? value : value[..48];

    /// <summary>
    /// Says something about a link. The host is wired to the engines page's service, which
    /// is the launcher's only one.
    /// </summary>
    private void Say(ToastTier tier, string title, string? body = null) =>
        Model?.Engines.Toasts.Post(new ToastRequest { Tier = tier, Title = title, Body = body });

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
