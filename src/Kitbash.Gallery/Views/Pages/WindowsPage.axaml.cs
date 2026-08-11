using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Projects;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>The window shell itself: the rail, the status bar, and the windows an app opens.</summary>
public partial class WindowsPage : GalleryPage
{
    /// <summary>What the welcome window needs, handed in the way a tool would hand it.</summary>
    private readonly IPlatformServices _platform;
    private readonly IFileSystem _files;

    /// <summary>One at a time, so switching host closes the one that was open.</summary>
    private ProjectsWindow? _projects;

    private SplashWindow? _splash;
    private int _splashStep;

    public WindowsPage(IPlatformServices platform, IFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(files);

        _platform = platform;
        _files = files;

        InitializeComponent();
    }

    /// <summary>The window this page is in, which is what a dialog is opened over.</summary>
    private Window? Host => TopLevel.GetTopLevel(this) as Window;

    /// <summary>
    /// The same two lines the title bar's own gesture runs. ChromelessWindow keeps that
    /// method to itself, so a consumer outside the library writes the state.
    /// </summary>
    private void OnMaximize(object? sender, RoutedEventArgs e)
    {
        if (Host is not { } window)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>
    /// The welcome window as one of the design's three hosts. Built here rather than
    /// through IProjectsWindows, since that holds one host and this page shows three.
    /// </summary>
    private void OnOpenProjects(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string name } || Host is not { } owner)
        {
            return;
        }

        _projects?.Close();

        var kind = HarnessProjectKind.For(Enum.Parse<ProjectHost>(name));
        kind.Reported = report => ProjectsAnswer.Text = report;

        _projects = new ProjectsWindow
        {
            Kind = kind,
            Platform = _platform,
            FileSystem = _files,
            DataContext = new ProjectsViewModel(kind.Recent, kind),
        };

        _projects.Closed += (_, _) => _projects = null;
        _projects.Show(owner);
    }

    // Built here rather than in a view, so the shape a real dialog takes is visible:
    // a title bar, content, and a footer holding the actions.
    private async void OnOpenDialog(object? sender, RoutedEventArgs e)
    {
        if (Host is not { } owner)
        {
            return;
        }

        var body = new TextBlock
        {
            Text = "A dialog is a real window with the same frame and the same title bar as any other. It cannot be resized or minimised, so its title bar keeps the close button alone. There is no scrim behind it.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16),
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };

        var dialog = new DialogWindow
        {
            Title = "Remove workspace",
            Width = 420,
            Height = 220,
        };

        var cancel = new Button { Content = "Cancel", Classes = { "ghost" } };
        var remove = new Button { Content = "Remove", Classes = { "danger" } };

        // A role rather than a handler. The dialog closes itself and answers for the
        // button that was pressed, so nothing here is wired to either one.
        Dialog.SetRole(cancel, DialogRole.Cancel);
        Dialog.SetRole(remove, DialogRole.Accept);

        // Accepting is the destructive answer here, so cancelling is the one that is
        // ready and Enter no longer reaches Remove.
        Dialog.SetTakesFocus(cancel, true);

        actions.Children.Add(cancel);
        actions.Children.Add(remove);

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
        };

        var bar = new WindowTitleBar { Title = "Remove workspace" };
        var footer = new DialogFooter { Content = actions };

        layout.Children.Add(bar);
        layout.Children.Add(body);
        layout.Children.Add(footer);
        Grid.SetRow(bar, 0);
        Grid.SetRow(body, 1);
        Grid.SetRow(footer, 2);

        dialog.Content = layout;

        var removed = await dialog.ShowDialog<bool>(owner);
        DialogAnswer.Text = removed ? "The dialog said remove." : "The dialog said no.";
    }

    /// <summary>
    /// A glyph mark, for the sample splash. The real one is a badge and wears no frame,
    /// so without this nothing here would still show the framed form.
    /// </summary>
    private static IImage DemoMark()
    {
        var group = new DrawingGroup();

        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M16 3 L28 10 L16 17 L4 10 Z"),
            Brush = new SolidColorBrush(Color.FromRgb(0x56, 0x9e, 0xff)),
        });

        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M4 15 L16 22 L28 15 L28 18 L16 25 L4 18 Z"),
            Brush = new SolidColorBrush(Color.FromArgb(158, 0x8f, 0xbe, 0xf5)),
        });

        group.Children.Add(new GeometryDrawing
        {
            Geometry = StreamGeometry.Parse("M4 22 L16 29 L28 22 L28 24.5 L16 31.5 L4 24.5 Z"),
            Brush = new SolidColorBrush(Color.FromArgb(77, 0x8f, 0xbe, 0xf5)),
        });

        return new DrawingImage { Drawing = group };
    }

    private void OnOpenSplash(object? sender, RoutedEventArgs e)
    {
        _splash?.Close();
        _splashStep = 0;

        _splash = new SplashWindow
        {
            Mark = DemoMark(),
            AppName = "Workbench",
            AppVersion = "1.4.2",
            Description = "Game data tooling for Godot projects",
        };

        _splash.Closed += (_, _) =>
        {
            _splash = null;
            SplashAnswer.Text = "The splash closed.";
        };

        _splash.Show();

        SplashAnswer.Text = "No progress until it is asked for.";
    }

    private static readonly string[] SplashSteps =
    [
        "Checking for an update",
        "Reading the workspace registry",
        "Loading workspace index",
        "Finding installed engines",
        "Opening the launcher",
    ];

    private void OnSplashStep(object? sender, RoutedEventArgs e) => ReportSplash(false);

    private void OnSplashFraction(object? sender, RoutedEventArgs e) => ReportSplash(true);

    private void ReportSplash(bool measured)
    {
        if (_splash is null)
        {
            SplashAnswer.Text = "Open one first.";

            return;
        }

        _splashStep = Math.Min(_splashStep + 1, SplashSteps.Length);

        var count = $"{_splashStep} of {SplashSteps.Length}";
        var fraction = measured ? _splashStep / (double)SplashSteps.Length : (double?)null;

        _splash.Report(SplashSteps[_splashStep - 1], fraction, count);

        SplashAnswer.Text = measured
            ? "The bar is filling to a fraction."
            : "The bar is sweeping, since nothing said how far along it is.";
    }

    private void OnCloseSplash(object? sender, RoutedEventArgs e) => _splash?.Close();
}
