using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Projects;
using Kitbash.Ui.Projects;
using Kitbash.Ui.Settings;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The window an app opens with. One list of what has been opened before, over whatever
/// <see cref="IProjectKind"/> the app registered. Open it through
/// <see cref="IProjectsWindows"/> rather than building one here.
/// </summary>
public partial class ProjectsWindow : ChromelessWindow
{
    private readonly ISettingsWindows? _settings;

    /// <summary>The app's pages, in rail order after the list.</summary>
    private IReadOnlyList<ProjectPage> _pages = [];

    /// <summary>True while an open is in flight, so a second gesture is ignored.</summary>
    private bool _opening;

    public ProjectsWindow()
    {
        InitializeComponent();
    }

    /// <summary>What a project is, for the app this window belongs to.</summary>
    public IProjectKind? Kind { get; init; }

    /// <summary>Opening a folder in the file browser, which is the one platform call here.</summary>
    public IPlatformServices? Platform { get; init; }

    /// <summary>
    /// Whether a path is a folder, which is what Open folder needs to know. The only disk
    /// this window touches, and it is read on the thread pool.
    /// </summary>
    public IFileSystem? FileSystem { get; init; }

    /// <summary>Null for an app with no settings window, which draws no cog.</summary>
    public ISettingsWindows? Settings
    {
        get => _settings;

        // In the setter rather than alongside the data context, so the rail is right
        // whatever order a caller writes its initialiser in.
        init
        {
            _settings = value;
            RailFoot.IsVisible = value is not null;
        }
    }

    private ProjectsViewModel? Model => DataContext as ProjectsViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        ShowIcon(Model?.Words.Icon);
        BuildRail();

        // The first row is picked, so Enter opens the thing a person came back for and
        // the keyboard has somewhere to start.
        Projects.SelectedIndex = Model?.Rows.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// The list, then whatever the app added. Built here rather than declared, since each
    /// item carries a word only the app knows and a container theme cannot be handed one
    /// per item.
    /// </summary>
    private void BuildRail()
    {
        _pages = Kind?.Pages ?? [];

        Rail.Items.Clear();
        Rail.Items.Add(RailItem(IconGlyph.Window, Model?.ListLabel ?? string.Empty));

        foreach (var page in _pages)
        {
            Rail.Items.Add(RailItem(page.Glyph, page.Label));
        }

        Rail.SelectedIndex = 0;
    }

    private ListBoxItem RailItem(IconGlyph glyph, string label)
    {
        // Named in full, since Icon on a Window is the one the desktop shows.
        var icon = new Icon { Glyph = glyph };
        icon[!Kitbash.Ui.Controls.Icon.SizeProperty] =
            new DynamicResourceExtension("IconSizeLarge");

        // Found from the window, so its own resources are searched before the app's, the
        // way a StaticResource in the markup would be.
        var item = new ListBoxItem
        {
            Theme = this.FindResource("ActivityRailItem") as ControlTheme,
            Content = icon,
        };

        ToolTip.SetTip(item, label);
        ToolTip.SetPlacement(item, PlacementMode.Right);
        AutomationProperties.SetName(item, label);

        return item;
    }

    private void OnPageSelected(object? sender, SelectionChangedEventArgs e)
    {
        var index = Rail.SelectedIndex;

        // Nothing selected happens while the rail is being rebuilt. The list stands until
        // something says otherwise, so the window is never a blank pane.
        var page = index > 0 && index - 1 < _pages.Count ? _pages[index - 1] : null;

        PagePane.Content = page?.Content;
        PagePane.IsVisible = page is not null;
        ListPage.IsVisible = page is null;
    }

    /// <summary>
    /// The app's own icon, in the title bar and in the task bar. An asset that is not
    /// there leaves both empty rather than stopping the window from opening.
    /// </summary>
    private void ShowIcon(Uri? source)
    {
        if (source is null)
        {
            return;
        }

        try
        {
            using var stream = AssetLoader.Open(source);
            var bitmap = new Bitmap(stream);

            TitleBar.Icon = bitmap;
            Icon = new WindowIcon(bitmap);
        }
        catch (Exception exception) when (exception is FileNotFoundException
            or ArgumentException or UriFormatException)
        {
        }
    }

    private void OnSettingsSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (RailFoot.SelectedIndex < 0)
        {
            return;
        }

        // A way in rather than a place, so it never stays selected.
        RailFoot.SelectedIndex = -1;
        Settings?.Open(this);
    }

    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        if (Kind is not { } kind)
        {
            return;
        }

        await AcceptAsync(await kind.CreateAsync(this));
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e) => await BrowseAsync(null);

    // A left click anywhere on a row opens it. On the list rather than in the row template,
    // so the row's own padding counts as the row. The menu button marks its own release
    // handled, so a click there never reaches this.
    private void OnRowClick(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        var row = (e.Source as Visual)
            ?.FindAncestorOfType<ListBoxItem>(includeSelf: true)
            ?.DataContext as ProjectRowViewModel;

        // The press is what picks the row, so a release under the list or on a row the
        // press did not land on is a drag rather than a click.
        if (row is not null && Selected == row)
        {
            _ = OpenAsync(row, inNewWindow: false);
        }
    }

    private void OnListKey(object? sender, KeyEventArgs e)
    {
        if (Selected is not { } row)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = OpenAsync(row, inNewWindow: false);
            return;
        }

        // The one shortcut the row menu names, so the menu and the keyboard agree.
        if (e.Key == Key.C && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            e.Handled = true;
            _ = CopyPathAsync(row);
        }
    }

    // Assembled here rather than declared with the row, since a MenuFlyout takes items or
    // an ItemsSource but not both, and every item is named after the row it acts on.
    private void OnRowMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ProjectRowViewModel row } button)
        {
            return;
        }

        // Picking the row first, so the menu and the row it belongs to cannot disagree.
        Projects.SelectedItem = row;

        var menu = new MenuFlyout();

        foreach (var item in ItemsFor(row))
        {
            menu.Items.Add(item);
        }

        // The button keeps its own menu company. The pointer has left the row by the time
        // the menu is open, so the hover rule alone would take it away underneath.
        button.Classes.Add("open");
        menu.Closed += (_, _) => button.Classes.Remove("open");

        menu.ShowAt(button);
    }

    // The same items, opened where the pointer is. A ContextMenu rather than a MenuFlyout,
    // since a menu belonging to the row opens at the cursor at its own width.
    private void OnRowContext(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: ProjectRowViewModel row } control)
        {
            return;
        }

        Projects.SelectedItem = row;

        var menu = new ContextMenu();

        foreach (var item in ItemsFor(row))
        {
            menu.Items.Add(item);
        }

        // Held by the row, so it is torn down with the row rather than left attached to
        // one a recycled container is now showing.
        control.ContextMenu = menu;
        menu.Closed += (_, _) => control.ContextMenu = null;

        menu.Open(control);
        e.Handled = true;
    }

    private IEnumerable<Control> ItemsFor(ProjectRowViewModel row)
    {
        yield return Item(
            row.OpenLabel,
            () => _ = OpenAsync(row, inNewWindow: false),
            new KeyGesture(Key.Enter));

        yield return Item(
            "Open in a new window",
            () => _ = OpenAsync(row, inNewWindow: true),
            enabled: !row.IsBroken);

        yield return new Separator();

        yield return Item("Open folder", () => OpenFolder(row));

        yield return Item(
            "Copy path",
            () => _ = CopyPathAsync(row),
            new KeyGesture(Key.C, KeyModifiers.Control | KeyModifiers.Shift));

        // Last, behind a separator, and it names its row, so a click on the wrong one is
        // visible before it is confirmed.
        yield return new Separator();
        yield return Item(row.RemoveLabel, () => Forget(row), danger: true);
    }

    private static MenuItem Item(
        string header,
        Action run,
        KeyGesture? gesture = null,
        bool enabled = true,
        bool danger = false)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGesture = gesture,
            IsEnabled = enabled,
        };

        if (danger)
        {
            item.Classes.Add("danger");
        }

        item.Click += (_, _) => run();

        return item;
    }

    private ProjectRowViewModel? Selected => Projects.SelectedItem as ProjectRowViewModel;

    /// <summary>
    /// Opens a row, which is what a click, Enter and the menu all reach. Public so one path
    /// answers every gesture and a test can drive the same one.
    /// </summary>
    public async Task OpenAsync(ProjectRowViewModel row, bool inNewWindow)
    {
        ArgumentNullException.ThrowIfNull(row);

        // One at a time. A click opens, so a double click is two of them, and an app must
        // not be told to open the same project twice.
        if (Kind is not { } kind || _opening)
        {
            return;
        }

        _opening = true;

        try
        {
            // A row that cannot be opened offers the only thing that would help. A click and
            // Enter go the same way as the menu, so no gesture reports a failure the menu
            // would have avoided.
            if (row.IsBroken)
            {
                await BrowseAsync(row.Project);
                return;
            }

            if (!await kind.OpenAsync(row.Project, inNewWindow, this))
            {
                return;
            }

            // Opening is what makes it recent, and the list is read again so the row moves
            // even when the window stays.
            Model?.Remember(new ProjectChoice(row.Project.Path, row.Project.Name));

            if (kind.ClosesOnOpen && !inNewWindow)
            {
                Close();
                return;
            }

            await ReloadAsync();
        }
        finally
        {
            _opening = false;
        }
    }

    /// <summary>
    /// Browsing for one, which is the Open button and the way a lost row is found again.
    /// </summary>
    private async Task BrowseAsync(RecentProject? replacing)
    {
        if (Kind is not { } kind)
        {
            return;
        }

        var choice = await kind.BrowseAsync(this, replacing);

        if (choice is null)
        {
            return;
        }

        // The old entry goes only once a new path has been accepted, so a person who
        // changes their mind still has the row that was there.
        if (replacing is { } old && Model is { } model)
        {
            model.Forget(old.Path);
        }

        await AcceptAsync(choice);
    }

    /// <summary>Remembers a path the app accepted, then opens it.</summary>
    private async Task AcceptAsync(ProjectChoice? choice)
    {
        if (choice is null || Model is not { } model || Kind is not { } kind)
        {
            return;
        }

        var project = model.Remember(choice);

        await ReloadAsync();

        if (await kind.OpenAsync(project, inNewWindow: false, this) && kind.ClosesOnOpen)
        {
            Close();
        }
    }

    private async Task ReloadAsync()
    {
        if (Model is { } model)
        {
            await model.LoadAsync();
        }
    }

    /// <summary>Shows the row in the file browser. Off the UI thread, like every open is.</summary>
    private void OpenFolder(ProjectRowViewModel row)
    {
        if (Platform is not { } platform || FileSystem is not { } files)
        {
            return;
        }

        var path = row.Path;

        Task.Run(() =>
        {
            try
            {
                // A path can name a file for an app whose project is one, and a row can be
                // pointing at a folder that has gone, so the nearest folder above it is
                // what gets shown.
                if (Nearest(files, path) is { } folder)
                {
                    platform.OpenInFileBrowser(DirectoryLocation.Parse(folder));
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                or DirectoryNotFoundException or InvalidOperationException)
            {
            }
        });
    }

    private static string? Nearest(IFileSystem files, string path)
    {
        for (var at = path; !string.IsNullOrWhiteSpace(at); at = System.IO.Path.GetDirectoryName(at))
        {
            if (files.DirectoryExists(at))
            {
                return at;
            }
        }

        return null;
    }

    private async Task CopyPathAsync(ProjectRowViewModel row)
    {
        // Avalonia 12 replaced SetTextAsync with a format and a value.
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetValueAsync(DataFormat.Text, row.Path);
        }
    }

    private void Forget(ProjectRowViewModel row)
    {
        Model?.Forget(row.Path);
        _ = ReloadAsync();
    }
}
