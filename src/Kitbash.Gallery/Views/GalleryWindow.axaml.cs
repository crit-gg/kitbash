using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Gallery.Views.Pages;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Toasts;

namespace Kitbash.Gallery.Views;

public partial class GalleryWindow : ChromelessWindow
{
    /// <summary>The window's own toasts, handed in rather than reached for.</summary>
    private readonly IToastService _toasts;

    /// <summary>A region of its own for one panel, which the toasts page asks for.</summary>
    private readonly IToastServiceFactory _scopes;

    /// <summary>The desktop's own colour picking, handed to the pages that hold a picker.</summary>
    private readonly IScreenColour _screen;

    /// <summary>What the welcome window needs, handed in the way a tool would hand it.</summary>
    private readonly IPlatformServices _platform;
    private readonly IFileSystem _files;

    /// <summary>The saved colours the inputs page and the colour page share.</summary>
    private readonly Palette _palette = new();

    /// <summary>Held rather than rebuilt, so turning it off removes the one that went on.</summary>
    private readonly StyleInclude _comfortable = new(new Uri("avares://Kitbash.Ui/Themes/"))
    {
        Source = new Uri("avares://Kitbash.Ui/Themes/KitbashComfortable.axaml"),
    };

    /// <summary>The pages, in rail order.</summary>
    private readonly List<RailPage> _pages;

    private bool _disabled;

    public GalleryWindow(
        IToastService toasts,
        IToastServiceFactory scopes,
        IScreenColour screen,
        IPlatformServices platform,
        IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(files);

        _toasts = toasts;
        _scopes = scopes;
        _screen = screen;
        _platform = platform;
        _files = files;

        InitializeComponent();

        TitleBar.Version = BuildVersion();

        WindowToasts.Service = _toasts;

        _pages =
        [
            new(IconGlyph.PlayCircle, "BUTTONS AND DROPDOWNS", () => new ButtonsPage()),
            new(IconGlyph.Tag, "CHIPS, BADGES AND PROGRESS", () => new ChipsPage()),
            new(IconGlyph.Pencil, "INPUTS", () => new InputsPage(_screen, _palette)),
            new(IconGlyph.Crosshair, "COLOUR PICKER", () => new ColourPage(_screen, _palette)),
            new(IconGlyph.Layout, "PANELS AND OVERLAYS", () => new PanelsPage()),
            new(IconGlyph.Sitemap, "LISTS AND TREES", () => new ListsPage()),
            new(IconGlyph.Table, "DATA GRIDS", () => new GridsPage()),
            new(IconGlyph.Columns, "TABS AND DOCKING", () => new DockingPage()),
            new(IconGlyph.Workflow, "THE NODE GRAPH", () => new GraphPage()),
            new(IconGlyph.Cube, "A MATERIAL GRAPH", () => new MaterialPage()),
            new(IconGlyph.Message, "TOASTS AND ALERTS", () => new ToastsPage(_toasts, _scopes)),
            new(IconGlyph.Window, "THE WINDOW SHELL", () => new WindowsPage(_platform, _files)),
        ];

        BuildRail();
    }

    private void BuildRail()
    {
        foreach (var page in _pages)
        {
            Rail.Items.Add(RailItem(page));
        }

        Rail.SelectedIndex = 0;
    }

    private ListBoxItem RailItem(RailPage page)
    {
        // Named in full, since Icon on a Window is the one the desktop shows.
        var icon = new Icon { Glyph = page.Glyph };
        icon[!Kitbash.Ui.Controls.Icon.SizeProperty] =
            new DynamicResourceExtension("IconSizeLarge");

        // Found from the window, so its own resources are searched before the app's, the
        // way a StaticResource in the markup would be.
        var item = new ListBoxItem
        {
            Theme = this.FindResource("ActivityRailItem") as ControlTheme,
            Content = icon,
        };

        ToolTip.SetTip(item, page.Name);
        ToolTip.SetPlacement(item, PlacementMode.Right);
        AutomationProperties.SetName(item, page.Name);

        return item;
    }

    private void OnPageSelected(object? sender, SelectionChangedEventArgs e)
    {
        var index = Rail.SelectedIndex;

        if (index < 0 || index >= _pages.Count)
        {
            return;
        }

        PageName.Text = _pages[index].Name;
        Body.Content = _pages[index].Page;
    }

    /// <summary>
    /// The version this was built at. The SDK appends the commit as build metadata, which
    /// is not part of the product version, so everything after the plus goes.
    /// </summary>
    internal static string BuildVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return string.Empty;
        }

        var metadata = informational.IndexOf('+', StringComparison.Ordinal);

        return metadata < 0 ? informational : informational[..metadata];
    }

    private void OnToggleEnabled(object? sender, RoutedEventArgs e)
    {
        _disabled = !_disabled;
        Body.IsEnabled = !_disabled;
        ToggleEnabledButton.Content = _disabled ? "Enable everything" : "Disable everything";
    }

    /// <summary>
    /// Adds and removes the second density, which is the one line an app takes at startup.
    /// Every control reads its size through a dynamic resource, so the page relays itself.
    /// </summary>
    private void OnToggleDensity(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not { } application || sender is not ToggleButton toggle)
        {
            return;
        }

        if (toggle.IsChecked is true)
        {
            application.Styles.Add(_comfortable);
        }
        else
        {
            application.Styles.Remove(_comfortable);
        }
    }

    /// <summary>
    /// A mark drawn here rather than loaded, since the gallery ships no icon and the point
    /// is that the mark belongs to the host.
    /// </summary>
    // The mark comes off icons/gallery through the csproj, so the splash and the
    // window draw the same file tools/appmark/generate.py wrote.
    private static readonly Uri MarkUri =
        new("avares://Kitbash.Gallery/Assets/Icons/icon_256x256.png");

    /// <summary>The app mark, at the largest size, for the splash to draw.</summary>
    internal static IImage SplashMark() => new Bitmap(AssetLoader.Open(MarkUri));

    /// <summary>
    /// A page the rail opens. It is built the first time it is asked for and kept after,
    /// so opening the gallery costs one page rather than ten.
    /// </summary>
    private sealed class RailPage(IconGlyph glyph, string name, Func<Control> build)
    {
        private Control? _built;

        public IconGlyph Glyph { get; } = glyph;

        public string Name { get; } = name;

        public Control Page => _built ??= build();
    }
}
