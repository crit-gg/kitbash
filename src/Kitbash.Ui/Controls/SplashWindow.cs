using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

// The shape, not System.IO.Path, which the implicit usings would otherwise win with.
using Path = Avalonia.Controls.Shapes.Path;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The window an app shows while it starts. It draws its own frame and carries every
/// value it needs, so it opens before any theme is loaded. The host owns its lifecycle.
/// </summary>
public class SplashWindow : Window
{
    /// <summary>The host app's mark. Give it a small bitmap, since it is drawn at 48.</summary>
    public static readonly StyledProperty<IImage?> MarkProperty =
        AvaloniaProperty.Register<SplashWindow, IImage?>(nameof(Mark));

    /// <summary>Whether the accent tile sits behind the mark. Off draws the mark alone.</summary>
    public static readonly StyledProperty<bool> ShowMarkFrameProperty =
        AvaloniaProperty.Register<SplashWindow, bool>(nameof(ShowMarkFrame), true);

    /// <summary>The host app's name. It is drawn in caps whatever case it arrives in.</summary>
    public static readonly StyledProperty<string> AppNameProperty =
        AvaloniaProperty.Register<SplashWindow, string>(nameof(AppName), string.Empty);

    /// <summary>The host app's version. Blank collapses the pill.</summary>
    public static readonly StyledProperty<string> AppVersionProperty =
        AvaloniaProperty.Register<SplashWindow, string>(nameof(AppVersion), string.Empty);

    /// <summary>One line under the name. Blank collapses it.</summary>
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<SplashWindow, string>(nameof(Description), string.Empty);

    /// <summary>Whether the lattice and the corner brackets are drawn.</summary>
    public static readonly StyledProperty<bool> ShowBackdropProperty =
        AvaloniaProperty.Register<SplashWindow, bool>(nameof(ShowBackdrop), true);

    /// <summary>Whether the close mark is drawn.</summary>
    public static readonly StyledProperty<bool> ShowCloseProperty =
        AvaloniaProperty.Register<SplashWindow, bool>(nameof(ShowClose), true);

    /// <summary>Whether the progress row is shown. Off until the host turns it on.</summary>
    public static readonly StyledProperty<bool> IsProgressVisibleProperty =
        AvaloniaProperty.Register<SplashWindow, bool>(nameof(IsProgressVisible));

    /// <summary>
    /// How far along the work is, from 0 to 1. Null is indeterminate, which is the form for
    /// a wait with no countable end.
    /// </summary>
    public static readonly StyledProperty<double?> ProgressProperty =
        AvaloniaProperty.Register<SplashWindow, double?>(nameof(Progress));

    /// <summary>What the host is doing now.</summary>
    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<SplashWindow, string>(nameof(Status), string.Empty);

    /// <summary>The countable stage, such as 3 of 5. Blank collapses it.</summary>
    public static readonly StyledProperty<string> StepProperty =
        AvaloniaProperty.Register<SplashWindow, string>(nameof(Step), string.Empty);

    /// <summary>
    /// How long the card takes to fade up when the window opens. Zero, the default, draws it
    /// at full opacity from the first frame. Set it before Show. There is no fade out.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> FadeInProperty =
        AvaloniaProperty.Register<SplashWindow, TimeSpan>(nameof(FadeIn));

    private const double CardWidth = 600;
    private const double Gutter = 12;

    // The box every Box Icons glyph is drawn on.
    private const double GlyphBox = 24;

    // How much of the track the indeterminate sweep covers.
    private const double SweepShare = 0.3;

    // How wide the sheen crossing a determinate fill is.
    private const double ShimmerWidth = 120;

    // How long the indeterminate sweep takes to cross the track.
    private static readonly TimeSpan SweepCycle = TimeSpan.FromSeconds(1.3);

    // The Slate tokens this window draws with. They are literals because the window has to
    // stand up before Themes/Tokens.axaml is loaded, so a change there means a change here.
    private static readonly IBrush SurfaceRoot = new ImmutableSolidColorBrush(0xff1e1f22);
    private static readonly IBrush SurfaceWell = new ImmutableSolidColorBrush(0xff161719);
    private static readonly IBrush SurfaceDrop = new ImmutableSolidColorBrush(0xff14293f);
    private static readonly IBrush LineWindow = new ImmutableSolidColorBrush(0xff4a4e55);
    private static readonly IBrush LineSeam = new ImmutableSolidColorBrush(0xff33353a);
    private static readonly IBrush AccentTintLine = new ImmutableSolidColorBrush(0xff2b4a6b);
    private static readonly IBrush AccentTintInk = new ImmutableSolidColorBrush(0xff8fbef5);
    private static readonly IBrush Accent = new ImmutableSolidColorBrush(0xff569eff);
    private static readonly IBrush InkTitle = new ImmutableSolidColorBrush(0xffe6ebf2);
    private static readonly IBrush InkPrimary = new ImmutableSolidColorBrush(0xffe3e5e9);
    private static readonly IBrush InkSecondary = new ImmutableSolidColorBrush(0xffa9aeb6);
    private static readonly IBrush InkMuted = new ImmutableSolidColorBrush(0xff7b8089);
    private static readonly IBrush InkCaption = new ImmutableSolidColorBrush(0xffeef1f4);

    // No offset and a blur that equals the gutter, which is what keeps the soft edge from
    // being clipped at the window boundary.
    private static readonly IEffect CardShadow =
        Avalonia.Media.Effect.Parse("drop-shadow(0 0 12 #60000000)");

    // Every line of text on the card carries this, so a word never sits flat on the lattice
    // behind it. The version is the one exception, since its pill is already a solid ground.
    private static readonly IEffect TextShadow =
        Avalonia.Media.Effect.Parse("drop-shadow(0 1 4 #a6000000)");

    // The indeterminate sweep. A fraction takes flat Accent instead.
    private static readonly IBrush SweepFill = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0, 0x56, 0x9e, 0xff), 0),
            new GradientStop(Color.FromRgb(0x56, 0x9e, 0xff), 0.55),
            new GradientStop(Color.FromArgb(51, 0x8f, 0xbe, 0xf5), 1),
        },
    };

    // The sheen that crosses a determinate fill. Faint on purpose: it says the app is still
    // running, and anything louder competes with the fill it is drawn on.
    private static readonly IBrush ShimmerFill = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0, 0xff, 0xff, 0xff), 0),
            new GradientStop(Color.FromArgb(51, 0xff, 0xff, 0xff), 0.5),
            new GradientStop(Color.FromArgb(0, 0xff, 0xff, 0xff), 1),
        },
    };

    private static readonly BoxShadows MarkShadow = BoxShadows.Parse("0 6 18 0 #6b000000");

    // A mark that is already a badge takes no frame, so what settles it onto the
    // backdrop is drawn around it instead: a soft light behind, and a shadow that
    // follows the mark's own silhouette rather than a box. The art is never touched.
    private const double MarkLightSize = 100;

    // The settled shadow, in two passes with no offset, so it is a glow on the mark's
    // own silhouette rather than something cast. The contact line goes on the image and
    // the falloff on the border around it: an effect applies to everything beneath it,
    // so nesting is what layers them. Side by side they would each shadow the mark on
    // its own and the contact line would be lost under the falloff.
    private static readonly IEffect MarkContact =
        Avalonia.Media.Effect.Parse("drop-shadow(0 0 1.9 #d4000000)");

    private static readonly IEffect MarkFalloff =
        Avalonia.Media.Effect.Parse("drop-shadow(0 0 5 #99000000)");

    // The settled light, a quarter brighter than the drawing it came from. Still far
    // fainter than it sounds, peaking at eight percent of an alpha channel, and it
    // cools as it falls: a pale white at the middle, through a mid blue, to the
    // accent going out. It is gone by 0.64 of the radius, so the last third of the
    // box is empty and the size is headroom.
    private static readonly IBrush MarkLight = new RadialGradientBrush
    {
        Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(49, 0xd6, 0xe4, 0xf6), 0),
            new GradientStop(Color.FromArgb(36, 0xd6, 0xe4, 0xf6), 0.22),
            new GradientStop(Color.FromArgb(22, 0x8c, 0xaf, 0xdc), 0.40),
            new GradientStop(Color.FromArgb(11, 0x56, 0x9e, 0xff), 0.52),
            new GradientStop(Color.FromArgb(0, 0x56, 0x9e, 0xff), 0.64),
            new GradientStop(Color.FromArgb(0, 0x56, 0x9e, 0xff), 1),
        },
    };

    private static readonly FontFamily UiFont =
        new("avares://Kitbash.Ui/Assets/Fonts#Archivo");

    private static readonly FontFamily MonoFont =
        new("avares://Kitbash.Ui/Assets/Fonts#JetBrains Mono");

    // The same glyph as IconX in Themes/Icons.axaml, on the same 24 box. Kept here because
    // the splash never loads that dictionary.
    private const string CloseGlyph =
        "F1 m9.17 13.41-3.54 3.54a.996.996 0 0 0 .71 1.7c.26 0 .51-.1.71-.29l2.83-2.83L12 " +
        "13.41l2.83 2.83 2.12 2.12c.2.2.45.29.71.29s.51-.1.71-.29a.996.996 0 0 0 0-1.41l-3." +
        "54-3.54L13.42 12l4.95-4.95a.996.996 0 1 0-1.41-1.41l-4.95 4.95-4.95-4.95a.996.996 " +
        "0 1 0-1.41 1.41L10.6 12l-1.41 1.41Z";

    private readonly SplashBackdrop _backdrop = new() { Name = "Backdrop" };
    private readonly Border _markFrame;
    private readonly Border _markLight;
    private readonly Border _markShadow;
    private readonly Image _markImage;
    private readonly TextBlock _name;
    private readonly Border _versionPill;
    private readonly TextBlock _versionText;
    private readonly TextBlock _description;
    private readonly StackPanel _progress;
    private readonly Border _track;
    private readonly Border _sweep;
    private readonly Border _shimmer;
    private readonly Border _dot;
    private readonly TextBlock _status;
    private readonly TextBlock _step;
    private readonly Border _close;
    private readonly Path _closeGlyph;

    private Border? _card;
    private bool _closeHeld;
    private bool _dismissed;
    private bool _opened;
    private CancellationTokenSource? _sweeping;
    private CancellationTokenSource? _shimmering;
    private CancellationTokenSource? _pulse;
    private CancellationTokenSource? _fading;
    private Task? _faded;
    private double _trackWidth;

    public SplashWindow()
    {
        _markImage = new Image
        {
            Name = "MarkImage",
            Width = 26,
            Height = 26,
            Stretch = Stretch.Uniform,
        };

        RenderOptions.SetBitmapInterpolationMode(_markImage, BitmapInterpolationMode.HighQuality);

        // The light draws at its full size and takes the mark's room in layout, so a
        // 100px glow does not grow the head of the splash. Nothing clips it.
        _markLight = new Border
        {
            Name = "MarkLight",
            Width = MarkLightSize,
            Height = MarkLightSize,
            Margin = new Thickness(-(MarkLightSize - 48) / 2),
            Background = MarkLight,
            IsHitTestVisible = false,
            IsVisible = false,
        };

        _markShadow = new Border { Name = "MarkShadow", Child = _markImage };

        _markFrame = new Border
        {
            Name = "Mark",
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(8),
            Background = SurfaceDrop,
            BorderBrush = AccentTintLine,
            BorderThickness = new Thickness(1),
            BoxShadow = MarkShadow,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Panel { Children = { _markLight, _markShadow } },
        };

        _name = new TextBlock
        {
            Name = "AppName",
            Effect = TextShadow,
            FontFamily = UiFont,
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.76,
            LineHeight = 24.2,
            Foreground = InkTitle,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _versionText = new TextBlock
        {
            FontFamily = MonoFont,
            FontSize = 11,
            FontWeight = FontWeight.Medium,
            LineHeight = 16.5,
            Foreground = AccentTintInk,
        };

        _versionPill = new Border
        {
            Name = "Version",
            Background = SurfaceDrop,
            BorderBrush = AccentTintLine,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = _versionText,
        };

        _description = new TextBlock
        {
            Name = "Description",
            Effect = TextShadow,
            FontFamily = UiFont,
            FontSize = 12.5,
            LetterSpacing = 0.25,
            LineHeight = 17.5,
            Foreground = InkSecondary,
            IsVisible = false,
        };

        _track = new Border
        {
            Name = "Track",
            Height = 5,
            Background = SurfaceWell,
            BorderBrush = LineSeam,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            ClipToBounds = true,
        };

        _sweep = new Border
        {
            Name = "Sweep",
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = SweepFill,

            // Set before the transition exists. A transition from the unset width, which is
            // NaN, interpolates to NaN and the bar never draws at all.
            Width = 0,

            Transitions =
            [
                new DoubleTransition
                {
                    Property = WidthProperty,
                    Duration = TimeSpan.FromMilliseconds(180),
                    Easing = new CubicEaseOut(),
                },
            ],
        };

        _shimmer = new Border
        {
            Name = "Shimmer",
            Width = ShimmerWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = ShimmerFill,
            IsVisible = false,
        };

        // Clipped by the fill, so the sheen crosses the whole track and is only ever seen
        // over the part that is filled.
        _sweep.ClipToBounds = true;
        _sweep.Child = _shimmer;

        _track.Child = _sweep;
        _track.SizeChanged += OnTrackSized;

        _dot = new Border
        {
            Name = "Dot",
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = Accent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _status = new TextBlock
        {
            Name = "Status",
            Effect = TextShadow,
            FontFamily = MonoFont,
            FontSize = 11,
            FontWeight = FontWeight.Medium,
            Foreground = InkPrimary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _step = new TextBlock
        {
            Name = "Step",
            Effect = TextShadow,
            FontFamily = MonoFont,
            FontSize = 11,
            FontWeight = FontWeight.Medium,
            Foreground = InkSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };

        // Stroked in its own fill, which is what the design's doubled drop shadow is doing.
        // The thickness is in the glyph's 24 units, so it lands near a pixel at 16.
        _closeGlyph = new Path
        {
            Data = StreamGeometry.Parse(CloseGlyph),
            StrokeThickness = 1.5,
            StrokeJoin = PenLineJoin.Round,
            StrokeLineCap = PenLineCap.Round,
        };

        _close = new Border
        {
            Name = "Close",
            Width = 24,
            Height = 24,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 10, 10, 0),

            // The glyph is drawn on a 24 box and wanted at 16, and scaling the box rather
            // than the ink is what keeps it the same weight as every other icon.
            Child = new Canvas
            {
                Width = GlyphBox,
                Height = GlyphBox,
                RenderTransform = new ScaleTransform(16 / GlyphBox, 16 / GlyphBox),
                RenderTransformOrigin = RelativePoint.Center,
                Children = { _closeGlyph },
            },
        };

        InkCloseGlyph(InkMuted);

        _close.PointerEntered += OnCloseEntered;
        _close.PointerExited += OnCloseExited;
        _close.PointerPressed += OnClosePressed;
        _close.PointerMoved += OnCloseMoved;
        _close.PointerReleased += OnCloseReleased;
        _close.PointerCaptureLost += OnCloseCaptureLost;

        // The whole card moves the window, since there is no title bar to grab. The close
        // mark marks its own press handled, and a handled press never reaches this.
        AddHandler(PointerPressedEvent, OnBodyPressed, RoutingStrategies.Bubble);

        _progress = BuildProgress();

        Content = BuildBody();
        Template = Frame;

        // The desktop draws nothing here, so the window is exactly the card plus the gutter
        // its own shadow falls into. It is not a ChromelessWindow and reads no window
        // setting, so nothing can hand this frame to the desktop.
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        SizeToContent = SizeToContent.Height;
        Width = CardWidth + (Gutter * 2);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
    }

    /// <inheritdoc cref="MarkProperty"/>
    public IImage? Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <inheritdoc cref="ShowMarkFrameProperty"/>
    public bool ShowMarkFrame
    {
        get => GetValue(ShowMarkFrameProperty);
        set => SetValue(ShowMarkFrameProperty, value);
    }

    /// <inheritdoc cref="AppNameProperty"/>
    public string AppName
    {
        get => GetValue(AppNameProperty);
        set => SetValue(AppNameProperty, value);
    }

    /// <inheritdoc cref="AppVersionProperty"/>
    public string AppVersion
    {
        get => GetValue(AppVersionProperty);
        set => SetValue(AppVersionProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="ShowBackdropProperty"/>
    public bool ShowBackdrop
    {
        get => GetValue(ShowBackdropProperty);
        set => SetValue(ShowBackdropProperty, value);
    }

    /// <inheritdoc cref="ShowCloseProperty"/>
    public bool ShowClose
    {
        get => GetValue(ShowCloseProperty);
        set => SetValue(ShowCloseProperty, value);
    }

    /// <inheritdoc cref="IsProgressVisibleProperty"/>
    public bool IsProgressVisible
    {
        get => GetValue(IsProgressVisibleProperty);
        set => SetValue(IsProgressVisibleProperty, value);
    }

    /// <inheritdoc cref="ProgressProperty"/>
    public double? Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public string Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <inheritdoc cref="StepProperty"/>
    public string Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <inheritdoc cref="FadeInProperty"/>
    public TimeSpan FadeIn
    {
        get => GetValue(FadeInProperty);
        set => SetValue(FadeInProperty, value);
    }

    /// <summary>
    /// Shows the window and finishes once the fade in has. A window with no fade finishes at
    /// once, and one closed part way through finishes there, so nothing is left waiting on a
    /// splash that has gone.
    /// </summary>
    public Task ShowAsync()
    {
        Show();

        return _faded ?? Task.CompletedTask;
    }

    /// <summary>
    /// Says what is happening over an indeterminate bar and shows the progress row. Call it
    /// on the UI thread.
    /// </summary>
    /// <param name="status">The line a person reads.</param>
    /// <param name="step">The countable stage, such as 3 of 5, or nothing.</param>
    public void Report(string status, string? step = null) => Report(status, null, step);

    /// <summary>
    /// Says what is happening and how far along it is, and shows the progress row. Call it
    /// on the UI thread.
    /// </summary>
    /// <param name="status">The line a person reads.</param>
    /// <param name="progress">How far along, from 0 to 1, or null for indeterminate.</param>
    /// <param name="step">The countable stage, such as 3 of 5, or nothing.</param>
    public void Report(string status, double? progress, string? step = null)
    {
        Status = status;
        Step = step ?? string.Empty;
        Progress = progress;
        IsProgressVisible = true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _card = e.NameScope.Find<Border>("Card");

        ApplyFadeStart();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _opened = true;

        StartFade();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The frame here is always this window's own, whatever window.nativeChrome says. A
        // desktop title bar over a splash would carry a name, a close button and a task bar
        // entry that the card already answers for.
        if (change.Property == WindowDecorationsProperty
            && change.GetNewValue<WindowDecorations>() != WindowDecorations.None)
        {
            WindowDecorations = WindowDecorations.None;
        }
        else if (change.Property == MarkProperty)
        {
            _markImage.Source = Mark;
            _markFrame.IsVisible = Mark is not null;
        }
        else if (change.Property == ShowMarkFrameProperty)
        {
            ApplyMarkFrame();
        }
        else if (change.Property == AppNameProperty)
        {
            _name.Text = AppName.ToUpperInvariant();
        }
        else if (change.Property == AppVersionProperty)
        {
            _versionText.Text = AppVersion;
            _versionPill.IsVisible = !string.IsNullOrWhiteSpace(AppVersion);
        }
        else if (change.Property == DescriptionProperty)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrWhiteSpace(Description);
        }
        else if (change.Property == ShowBackdropProperty)
        {
            _backdrop.IsVisible = ShowBackdrop;
        }
        else if (change.Property == ShowCloseProperty)
        {
            _close.IsVisible = ShowClose;
        }
        else if (change.Property == IsProgressVisibleProperty)
        {
            _progress.IsVisible = IsProgressVisible;

            ApplyMotion();
        }
        else if (change.Property == ProgressProperty)
        {
            ApplyBar();
        }
        else if (change.Property == StatusProperty)
        {
            _status.Text = Status;
        }
        else if (change.Property == StepProperty)
        {
            _step.Text = Step;
            _step.IsVisible = !string.IsNullOrWhiteSpace(Step);
        }
        else if (change.Property == FadeInProperty)
        {
            ApplyFadeStart();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopSweep();
        StopShimmer();
        StopPulse();
        StopFade();

        base.OnClosed(e);

        if (_dismissed)
        {
            AbandonLaunch();
        }
    }

    /// <summary>
    /// A person dismissing the splash before the app has a window of its own is a person
    /// cancelling the launch, so the app goes with it whatever the host had planned. Nothing
    /// happens once another window is up, since the splash is then only a splash.
    /// </summary>
    private void AbandonLaunch()
    {
        // A window has no route to the lifetime, so this is the one place here that reaches
        // for the application. Shutdown ignores ShutdownMode, which is the point: a host
        // holding the app open explicitly must still let go of a launch nobody wants.
        if (Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.Windows.Any(window => window != this && window.IsVisible))
        {
            return;
        }

        desktop.Shutdown();
    }

    /// <summary>
    /// The frame, built here rather than in a theme, so the window stands up with no styles
    /// loaded at all. The name on the layer manager is what TopLevel looks for.
    /// </summary>
    private static IControlTemplate Frame { get; } =
        new FuncControlTemplate<SplashWindow>((window, scope) =>
        {
            var presenter = new ContentPresenter
            {
                Name = "PART_ContentPresenter",
            };

            presenter[~ContentPresenter.ContentProperty] = window[~ContentProperty];
            presenter.RegisterInNameScope(scope);

            var layers = new VisualLayerManager
            {
                Name = "PART_VisualLayerManager",
                Child = presenter,
            };

            layers.RegisterInNameScope(scope);

            var card = new Border
            {
                Name = "PART_ContentRoot",
                Background = SurfaceRoot,
                BorderBrush = LineWindow,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    ClipToBounds = true,
                    Child = layers,
                },
            };

            card.RegisterInNameScope(scope);

            var shadow = new Border
            {
                Name = "Card",
                Margin = new Thickness(Gutter),
                Effect = CardShadow,
                Child = card,
            };

            shadow.RegisterInNameScope(scope);

            return shadow;
        });

    private Panel BuildBody()
    {
        var head = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Thickness(32, 26, 32, 0),
            Children =
            {
                _markFrame,
                new StackPanel
                {
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 10,
                            Children = { _name, _versionPill },
                        },
                        _description,
                    },
                },
            },
        };

        // The mark is the only thing here that can be absent before the host says anything.
        _markFrame.IsVisible = false;

        return new Panel
        {
            Children =
            {
                _backdrop,
                new StackPanel
                {
                    Margin = new Thickness(0, 0, 0, 24),
                    Children = { head, _progress },
                },
                _close,
            },
        };
    }

    private StackPanel BuildProgress()
    {
        var line = new Grid
        {
            Margin = new Thickness(0, 11, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { _dot, _status },
                },
                _step,
            },
        };

        Grid.SetColumn(_step, 1);

        return new StackPanel
        {
            Name = "Progress",
            Margin = new Thickness(32, 20, 32, 0),
            IsVisible = false,
            Children = { _track, line },
        };
    }

    private void ApplyMarkFrame()
    {
        var framed = ShowMarkFrame;

        _markFrame.Background = framed ? SurfaceDrop : null;
        _markFrame.BorderThickness = new Thickness(framed ? 1 : 0);
        _markFrame.BoxShadow = framed ? MarkShadow : default;

        _markImage.Width = framed ? 26 : 48;
        _markImage.Height = framed ? 26 : 48;

        // A framed mark already sits on a ground of its own and takes neither.
        _markLight.IsVisible = !framed;
        _markShadow.Effect = framed ? null : MarkFalloff;
        _markImage.Effect = framed ? null : MarkContact;
    }

    /// <summary>
    /// What the sweep is asked to be. Its own Width transitions, so reading that back gives
    /// wherever the transition has got to rather than what it was set to.
    /// </summary>
    private double SweepWidth => _trackWidth * SweepShare;

    private void OnTrackSized(object? sender, SizeChangedEventArgs e)
    {
        _trackWidth = Math.Max(
            0,
            e.NewSize.Width - _track.BorderThickness.Left - _track.BorderThickness.Right);

        // Both journeys are measured from the track, so a new track means new journeys.
        StopSweep();
        StopShimmer();

        ApplyBar();
    }

    /// <summary>
    /// The bar in whichever form it is in. Indeterminate is a 30 percent sweep under a
    /// gradient, and a fraction is a flat accent fill of the track.
    /// </summary>
    private void ApplyBar()
    {
        if (_trackWidth <= 0)
        {
            return;
        }

        var fraction = Progress;

        _shimmer.IsVisible = fraction is not null;

        if (fraction is null)
        {
            _sweep.Background = SweepFill;
            _sweep.Width = SweepWidth;
        }
        else
        {
            _sweep.Background = Accent;
            _sweep.Width = _trackWidth * Math.Clamp(fraction.Value, 0, 1);
        }

        ApplyMotion();
    }

    /// <summary>
    /// Starts and stops each animation on its own. The pulse follows the row, since it says
    /// work is happening either way, and the sweep follows the bar having no fraction.
    /// </summary>
    private void ApplyMotion()
    {
        if (IsProgressVisible)
        {
            StartPulse();
        }
        else
        {
            StopPulse();
        }

        if (IsProgressVisible && Progress is null)
        {
            StartSweep();
        }
        else
        {
            StopSweep();
        }

        if (IsProgressVisible && Progress is not null)
        {
            StartShimmer();
        }
        else
        {
            StopShimmer();
        }
    }

    /// <summary>
    /// Puts the card on nothing before the window is on screen, so no frame of a solid card
    /// is drawn ahead of the fade.
    /// </summary>
    private void ApplyFadeStart()
    {
        if (_card is null || _opened)
        {
            return;
        }

        _card.Opacity = FadeIn > TimeSpan.Zero ? 0 : 1;
    }

    /// <summary>
    /// Fades the whole card up over FadeIn, its shadow with it. Zero leaves the card alone
    /// and runs no animation at all.
    /// </summary>
    private void StartFade()
    {
        if (_card is null || FadeIn <= TimeSpan.Zero)
        {
            return;
        }

        _fading = new CancellationTokenSource();

        // Forward keeps the value the fade ends on. Without it the card lands back on the
        // 0 its own opacity still holds.
        var fade = new Animation
        {
            Duration = FadeIn,
            FillMode = FillMode.Forward,
            Easing = new SineEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0) } },
            },
        };

        _faded = fade.RunAsync(_card, _fading.Token);
    }

    private void StopFade()
    {
        _fading?.Cancel();
        _fading?.Dispose();
        _fading = null;
    }

    private void StartPulse()
    {
        if (_pulse is not null)
        {
            return;
        }

        _pulse = new CancellationTokenSource();

        var pulse = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.6),
            IterationCount = IterationCount.Infinite,
            Easing = new SineEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.55) } },
                new KeyFrame { Cue = new Cue(0.5), Setters = { new Setter(OpacityProperty, 1.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.55) } },
            },
        };

        _ = pulse.RunAsync(_dot, _pulse.Token);
    }

    private void StopPulse()
    {
        _pulse?.Cancel();
        _pulse?.Dispose();
        _pulse = null;

        _dot.Opacity = 1;
    }

    /// <summary>
    /// The sweep travels from just off the left edge to just past the right, so its journey
    /// is its own width and the track's alone.
    /// </summary>
    private void StartSweep()
    {
        var travel = SweepWidth;

        if (_sweeping is not null || travel <= 0)
        {
            return;
        }

        _sweeping = new CancellationTokenSource();

        // Linear, and from just off one edge to just off the other. An eased crossing spends
        // its slow ends off the track, and one that stops short leaves a lit sliver at the
        // right to vanish, so either way the eye reads a rush and then a wait.
        var crossing = new Animation
        {
            Duration = SweepCycle,
            IterationCount = IterationCount.Infinite,
            Easing = new LinearEasing(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(TranslateTransform.XProperty, -travel) },
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(TranslateTransform.XProperty, _trackWidth) },
                },
            },
        };

        _ = crossing.RunAsync(_sweep, _sweeping.Token);
    }

    /// <summary>
    /// The sheen on a determinate fill. It crosses the whole track and then waits, so what a
    /// person sees is an occasional pass over however much is filled.
    /// </summary>
    private void StartShimmer()
    {
        if (_shimmering is not null || _trackWidth <= 0)
        {
            return;
        }

        _shimmering = new CancellationTokenSource();

        var shimmer = new Animation
        {
            Duration = TimeSpan.FromSeconds(2.6),
            IterationCount = IterationCount.Infinite,
            Easing = new SineEaseInOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(TranslateTransform.XProperty, -ShimmerWidth) },
                },
                new KeyFrame
                {
                    Cue = new Cue(0.6),
                    Setters = { new Setter(TranslateTransform.XProperty, _trackWidth) },
                },

                // Held off the end, so the passes are spaced rather than continuous.
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(TranslateTransform.XProperty, _trackWidth) },
                },
            },
        };

        _ = shimmer.RunAsync(_shimmer, _shimmering.Token);
    }

    private void StopShimmer()
    {
        _shimmering?.Cancel();
        _shimmering?.Dispose();
        _shimmering = null;

        _shimmer.RenderTransform = null;
    }

    /// <summary>
    /// A cancelled animation leaves its last frame behind, so the transform goes with it or
    /// a determinate fill starts wherever the sweep happened to stop.
    /// </summary>
    private void StopSweep()
    {
        _sweeping?.Cancel();
        _sweeping?.Dispose();
        _sweeping = null;

        _sweep.RenderTransform = null;
    }

    /// <summary>The fill and the stroke that thickens it are always the same brush.</summary>
    private void InkCloseGlyph(IBrush ink)
    {
        _closeGlyph.Fill = ink;
        _closeGlyph.Stroke = ink;
    }

    /// <summary>
    /// Moves the window. The second click of a double click is ignored, since there is
    /// nothing here for one to do and a splash cannot be maximised.
    /// </summary>
    private void OnBodyPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 1 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    /// <summary>Whether the pointer is over the close mark, while it holds the pointer.</summary>
    private bool OverClose(PointerEventArgs e) =>
        new Rect(_close.Bounds.Size).Contains(e.GetPosition(_close));

    private void OnCloseEntered(object? sender, PointerEventArgs e)
    {
        if (!_closeHeld)
        {
            InkCloseGlyph(InkCaption);
        }
    }

    private void OnCloseExited(object? sender, PointerEventArgs e)
    {
        if (!_closeHeld)
        {
            InkCloseGlyph(InkMuted);
        }
    }

    /// <summary>
    /// Takes the pointer and answers on release rather than on press, the way a button does,
    /// so a press that wanders off the mark is a press that changed its mind. Handling it
    /// here is also what keeps the press from starting a window drag.
    /// </summary>
    private void OnClosePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_close).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _closeHeld = true;
        e.Handled = true;

        e.Pointer.Capture(_close);

        InkCloseGlyph(InkCaption);
    }

    private void OnCloseMoved(object? sender, PointerEventArgs e)
    {
        if (_closeHeld)
        {
            InkCloseGlyph(OverClose(e) ? InkCaption : InkMuted);
        }
    }

    private void OnCloseReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_closeHeld)
        {
            return;
        }

        _closeHeld = false;
        e.Handled = true;

        e.Pointer.Capture(null);

        var over = OverClose(e);

        InkCloseGlyph(over ? InkCaption : InkMuted);

        if (over)
        {
            _dismissed = true;

            Close();
        }
    }

    private void OnCloseCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _closeHeld = false;

        InkCloseGlyph(InkMuted);
    }
}
