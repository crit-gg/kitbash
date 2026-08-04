using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// The colour editor. One body, and the host decides the frame: floating it is a card with a
/// shadow and a Cancel and Apply footer, and <see cref="IsInPanel"/> drops all three so the
/// same body sits in a property panel and applies live.
/// </summary>
/// <example>
/// <code>
/// &lt;ui:ColorPicker Header="FILL COLOUR" IsInPanel="True" Color="{Binding Tint}" /&gt;
/// </code>
/// </example>
[TemplatePart(FramePart, typeof(Popover))]
[TemplatePart(FieldPart, typeof(Control))]
[TemplatePart(FieldHuePart, typeof(Border))]
[TemplatePart(FieldShadePart, typeof(Border))]
[TemplatePart(FieldHandlePart, typeof(Control))]
[TemplatePart(WheelPart, typeof(Control))]
[TemplatePart(WheelHuePart, typeof(Ellipse))]
[TemplatePart(WheelBloomPart, typeof(Ellipse))]
[TemplatePart(WheelShadePart, typeof(Ellipse))]
[TemplatePart(WheelHandlePart, typeof(Control))]
[TemplatePart(BarPart, typeof(Slider))]
[TemplatePart(AlphaPart, typeof(Slider))]
[TemplatePart(CheckerPart, typeof(Border))]
[TemplatePart(NewPart, typeof(Border))]
[TemplatePart(OldPart, typeof(Border))]
[TemplatePart(HexPart, typeof(TextBox))]
[TemplatePart(LiteralPart, typeof(TextBlock))]
[TemplatePart(LiteralFootPart, typeof(TextBlock))]
[TemplatePart(ChannelsPart, typeof(ItemsControl))]
[TemplatePart(RecentPart, typeof(Control))]
[TemplatePart(ActionsPart, typeof(Control))]
public class ColorPicker : TemplatedControl
{
    /// <summary>The colour being edited. Red, green and blue may go above 1.</summary>
    public static readonly StyledProperty<ColorValue> ColorProperty =
        AvaloniaProperty.Register<ColorPicker, ColorValue>(
            nameof(Color), new ColorValue(1, 1, 1, 1), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The colour this one replaces, which is the left half of the chip.</summary>
    public static readonly StyledProperty<ColorValue> PreviousProperty =
        AvaloniaProperty.Register<ColorPicker, ColorValue>(nameof(Previous), new ColorValue(1, 1, 1, 1));

    /// <summary>
    /// Stops. Moving it multiplies the three colour channels by two to the power of the
    /// change, and it is the one control here that takes a colour above 1.
    /// </summary>
    public static readonly StyledProperty<double> ExposureProperty =
        AvaloniaProperty.Register<ColorPicker, double>(nameof(Exposure));

    public static readonly StyledProperty<ColorShape> ShapeProperty =
        AvaloniaProperty.Register<ColorPicker, ColorShape>(nameof(Shape));

    public static readonly StyledProperty<ColorValueMode> ModeProperty =
        AvaloniaProperty.Register<ColorPicker, ColorValueMode>(nameof(Mode));

    /// <summary>The word over the picker, such as COLOUR or FILL COLOUR.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<ColorPicker, object?>(nameof(Header), "COLOUR");

    /// <summary>
    /// The colours a person saved. It starts empty and the picker never seeds it. A list
    /// that can be written to gains the colour when the add tile is clicked.
    /// </summary>
    public static readonly StyledProperty<IList<ColorValue>?> SwatchesProperty =
        AvaloniaProperty.Register<ColorPicker, IList<ColorValue>?>(nameof(Swatches));

    /// <summary>The colours a person picked lately. Empty until a host fills it.</summary>
    public static readonly StyledProperty<IList<ColorValue>?> RecentProperty =
        AvaloniaProperty.Register<ColorPicker, IList<ColorValue>?>(nameof(Recent));

    /// <summary>
    /// True when the picker is part of a panel rather than floating over one. It drops the
    /// frame, the shadow and the two buttons, and the footer reads the literal back. It is a
    /// property rather than a class because the frame it has to reach is inside the template.
    /// </summary>
    public static readonly StyledProperty<bool> IsInPanelProperty =
        AvaloniaProperty.Register<ColorPicker, bool>(nameof(IsInPanel));

    /// <summary>The Apply button was clicked. A popover host commits and closes on this.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> AppliedEvent =
        RoutedEvent.Register<ColorPicker, RoutedEventArgs>(nameof(Applied), RoutingStrategies.Bubble);

    /// <summary>The Cancel button was clicked.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CancelledEvent =
        RoutedEvent.Register<ColorPicker, RoutedEventArgs>(nameof(Cancelled), RoutingStrategies.Bubble);

    /// <summary>The add tile was clicked, carrying the colour that was added.</summary>
    public static readonly RoutedEvent<ColorEventArgs> SwatchAddedEvent =
        RoutedEvent.Register<ColorPicker, ColorEventArgs>(nameof(SwatchAdded), RoutingStrategies.Bubble);

    private const string FramePart = "PART_Frame";
    private const string FieldPart = "PART_Field";
    private const string FieldHuePart = "PART_FieldHue";
    private const string FieldShadePart = "PART_FieldShade";
    private const string FieldHandlePart = "PART_FieldHandle";
    private const string WheelPart = "PART_Wheel";
    private const string WheelHuePart = "PART_WheelHue";
    private const string WheelBloomPart = "PART_WheelBloom";
    private const string WheelShadePart = "PART_WheelShade";
    private const string WheelHandlePart = "PART_WheelHandle";
    private const string BarPart = "PART_Bar";
    private const string AlphaPart = "PART_Alpha";
    private const string CheckerPart = "PART_Checker";
    private const string NewPart = "PART_New";
    private const string OldPart = "PART_Old";
    private const string HexPart = "PART_Hex";
    private const string LiteralPart = "PART_Literal";
    private const string LiteralFootPart = "PART_LiteralFoot";
    private const string ChannelsPart = "PART_Channels";
    private const string RecentPart = "PART_Recent";
    private const string ActionsPart = "PART_Actions";
    private const string CopyPart = "PART_Copy";
    private const string AddSwatchPart = "PART_AddSwatch";
    private const string ApplyPart = "PART_Apply";
    private const string CancelPart = "PART_Cancel";

    /// <summary>The stops the exposure row reaches, either side of nothing.</summary>
    private const double ExposureReach = 10;

    /// <summary>Two to the power of the reach, which is as far as a raw channel goes.</summary>
    private const double RawCeiling = 1024;

    private Popover? _frame;
    private Control? _field;
    private Border? _fieldHue;
    private Border? _fieldShade;
    private Control? _fieldHandle;
    private Control? _wheel;
    private Ellipse? _wheelHue;
    private Ellipse? _wheelBloom;
    private Ellipse? _wheelShade;
    private Control? _wheelHandle;
    private Slider? _bar;
    private Slider? _alpha;
    private Border? _checker;
    private Border? _new;
    private Border? _old;
    private TextBox? _hex;
    private TextBlock? _literal;
    private TextBlock? _literalFoot;
    private Control? _recent;
    private Control? _actions;

    /// <summary>
    /// Where the picker is standing in hue, saturation and value. A grey has no hue and
    /// black has no saturation, so both are held here rather than read back from the colour,
    /// which is what stops the field jumping while it is being dragged.
    /// </summary>
    private double _hue;
    private double _saturation;
    private double _value = 1;

    /// <summary>True while the picker is writing to its own parts, so nothing echoes back.</summary>
    private bool _quiet;

    public ColorPicker()
    {
        Channels = [];
        Build();
    }

    /// <inheritdoc cref="AppliedEvent"/>
    public event EventHandler<RoutedEventArgs>? Applied
    {
        add => AddHandler(AppliedEvent, value);
        remove => RemoveHandler(AppliedEvent, value);
    }

    /// <inheritdoc cref="CancelledEvent"/>
    public event EventHandler<RoutedEventArgs>? Cancelled
    {
        add => AddHandler(CancelledEvent, value);
        remove => RemoveHandler(CancelledEvent, value);
    }

    /// <inheritdoc cref="SwatchAddedEvent"/>
    public event EventHandler<ColorEventArgs>? SwatchAdded
    {
        add => AddHandler(SwatchAddedEvent, value);
        remove => RemoveHandler(SwatchAddedEvent, value);
    }

    /// <inheritdoc cref="ColorProperty"/>
    public ColorValue Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <inheritdoc cref="PreviousProperty"/>
    public ColorValue Previous
    {
        get => GetValue(PreviousProperty);
        set => SetValue(PreviousProperty, value);
    }

    /// <inheritdoc cref="ExposureProperty"/>
    public double Exposure
    {
        get => GetValue(ExposureProperty);
        set => SetValue(ExposureProperty, value);
    }

    /// <inheritdoc cref="ShapeProperty"/>
    public ColorShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    /// <inheritdoc cref="ModeProperty"/>
    public ColorValueMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="SwatchesProperty"/>
    public IList<ColorValue>? Swatches
    {
        get => GetValue(SwatchesProperty);
        set => SetValue(SwatchesProperty, value);
    }

    /// <inheritdoc cref="RecentProperty"/>
    public IList<ColorValue>? Recent
    {
        get => GetValue(RecentProperty);
        set => SetValue(RecentProperty, value);
    }

    /// <inheritdoc cref="IsInPanelProperty"/>
    public bool IsInPanel
    {
        get => GetValue(IsInPanelProperty);
        set => SetValue(IsInPanelProperty, value);
    }

    /// <summary>The rows under the shape. The picker owns them and rebuilds them per mode.</summary>
    public ObservableCollection<ColorChannel> Channels { get; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Unwatch();

        _frame = e.NameScope.Find<Popover>(FramePart);
        _field = e.NameScope.Find<Control>(FieldPart);
        _fieldHue = e.NameScope.Find<Border>(FieldHuePart);
        _fieldShade = e.NameScope.Find<Border>(FieldShadePart);
        _fieldHandle = e.NameScope.Find<Control>(FieldHandlePart);
        _wheel = e.NameScope.Find<Control>(WheelPart);
        _wheelHue = e.NameScope.Find<Ellipse>(WheelHuePart);
        _wheelBloom = e.NameScope.Find<Ellipse>(WheelBloomPart);
        _wheelShade = e.NameScope.Find<Ellipse>(WheelShadePart);
        _wheelHandle = e.NameScope.Find<Control>(WheelHandlePart);
        _bar = e.NameScope.Find<Slider>(BarPart);
        _alpha = e.NameScope.Find<Slider>(AlphaPart);
        _checker = e.NameScope.Find<Border>(CheckerPart);
        _new = e.NameScope.Find<Border>(NewPart);
        _old = e.NameScope.Find<Border>(OldPart);
        _hex = e.NameScope.Find<TextBox>(HexPart);
        _literal = e.NameScope.Find<TextBlock>(LiteralPart);
        _literalFoot = e.NameScope.Find<TextBlock>(LiteralFootPart);
        _recent = e.NameScope.Find<Control>(RecentPart);
        _actions = e.NameScope.Find<Control>(ActionsPart);

        if (e.NameScope.Find<ItemsControl>(ChannelsPart) is { } rows)
        {
            rows.ItemsSource = Channels;
        }

        Watch();
        Dress();
        Read();
        Show();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColorProperty)
        {
            // A colour handed in from outside brings its own exposure with it, since a
            // channel above 1 is already that many stops over.
            if (!_quiet)
            {
                var peak = Color.Peak;

                SetCurrentValue(ExposureProperty, peak > 1 ? Math.Log2(peak) : 0);
            }

            Read();
            Show();
        }
        else if (change.Property == ModeProperty)
        {
            Build();
            Show();
        }
        else if (change.Property == ShapeProperty || change.Property == PreviousProperty)
        {
            Show();
        }
        else if (change.Property == IsInPanelProperty)
        {
            Host();
        }
        else if (change.Property == SwatchesProperty || change.Property == RecentProperty)
        {
            Rewatch(change.OldValue as IList<ColorValue>, change.NewValue as IList<ColorValue>);
            Show();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_field is { IsVisible: true } field && Inside(field, e))
        {
            Pick(field, e);
        }
        else if (_wheel is { IsVisible: true } wheel && Inside(wheel, e))
        {
            Spin(wheel, e);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_field is { } field && ReferenceEquals(e.Pointer.Captured, field))
        {
            Pick(field, e);
        }
        else if (_wheel is { } wheel && ReferenceEquals(e.Pointer.Captured, wheel))
        {
            Spin(wheel, e);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (ReferenceEquals(e.Pointer.Captured, _field) || ReferenceEquals(e.Pointer.Captured, _wheel))
        {
            e.Pointer.Capture(null);
        }
    }

    private static bool Inside(Control part, PointerEventArgs e)
    {
        var at = e.GetPosition(part);

        return at.X >= 0 && at.Y >= 0 && at.X <= part.Bounds.Width && at.Y <= part.Bounds.Height;
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    /// <summary>Builds the rows the mode calls for. Numbers and ramps are filled in later.</summary>
    private void Build()
    {
        foreach (var channel in Channels)
        {
            channel.Edited = null;
        }

        Channels.Clear();

        switch (Mode)
        {
            case ColorValueMode.Hsv:
                Add("H", 360, 1, "0", value => Turn(value, _saturation, _value));
                Add("S", 100, 1, "0", value => Turn(_hue, value / 100, _value));
                Add("V", 100, 1, "0", value => Turn(_hue, _saturation, value / 100));
                Add("A", 255, 1, "0", value => Write(Color.WithAlpha(value / 255)));
                break;

            case ColorValueMode.Raw:
                Add("R", 1, 0.01, "0.000", value => Write(new ColorValue(value, Color.G, Color.B, Color.A)), RawCeiling);
                Add("G", 1, 0.01, "0.000", value => Write(new ColorValue(Color.R, value, Color.B, Color.A)), RawCeiling);
                Add("B", 1, 0.01, "0.000", value => Write(new ColorValue(Color.R, Color.G, value, Color.A)), RawCeiling);
                Add("A", 1, 0.01, "0.000", value => Write(Color.WithAlpha(value)));
                break;

            case ColorValueMode.Okhsl:
                Add("H", 360, 1, "0", value => Shift(value, Okhsl().Saturation, Okhsl().Lightness));
                Add("S", 100, 1, "0", value => Shift(Okhsl().Hue, value / 100, Okhsl().Lightness));
                Add("L", 100, 1, "0", value => Shift(Okhsl().Hue, Okhsl().Saturation, value / 100));
                Add("A", 100, 1, "0", value => Write(Color.WithAlpha(value / 100)));
                break;

            default:
                Add("R", 255, 1, "0", value => Write(new ColorValue(value / 255, Color.G, Color.B, Color.A)));
                Add("G", 255, 1, "0", value => Write(new ColorValue(Color.R, value / 255, Color.B, Color.A)));
                Add("B", 255, 1, "0", value => Write(new ColorValue(Color.R, Color.G, value / 255, Color.A)));
                Add("A", 255, 1, "0", value => Write(Color.WithAlpha(value / 255)));
                break;
        }

        // Every mode carries it, so a value above 1 can be reached from any of them.
        var exposure = new ColorChannel("EV", -ExposureReach, ExposureReach, 0.1, "+0.00;-0.00;+0.00")
        {
            RampMinimum = -ExposureReach,
            RampMaximum = ExposureReach,
            Edited = Expose,
        };

        Channels.Add(exposure);

        void Add(string label, double ramp, double step, string format, Action<double> edit, double? ceiling = null)
        {
            Channels.Add(new ColorChannel(label, 0, ceiling ?? ramp, step, format)
            {
                RampMaximum = ramp,
                Edited = edit,
            });
        }
    }

    private (double Hue, double Saturation, double Lightness) Okhsl() => Color.ToOkhsl();

    private void Turn(double hue, double saturation, double value)
    {
        _hue = hue;
        _saturation = Clamp01(saturation);
        _value = Clamp01(value);

        Write(ColorValue.FromHsv(_hue, _saturation, _value, Color.A));
    }

    private void Shift(double hue, double saturation, double lightness) =>
        Write(ColorValue.FromOkhsl(hue, saturation, lightness, Color.A));

    private void Expose(double stops)
    {
        var factor = Math.Pow(2, stops - Exposure);

        SetCurrentValue(ExposureProperty, stops);
        Write(Color.Scaled(factor));
    }

    /// <summary>An edit from inside, which keeps the exposure the person set.</summary>
    private void Write(ColorValue color)
    {
        _quiet = true;

        try
        {
            SetCurrentValue(ColorProperty, color);
        }
        finally
        {
            _quiet = false;
        }
    }

    /// <summary>Takes the standing hue, saturation and value from the colour.</summary>
    private void Read()
    {
        var (hue, saturation, value) = Color.ToHsv();

        if (saturation > 0)
        {
            _hue = hue;
        }

        if (value > 0)
        {
            _saturation = saturation;
        }

        _value = value;
    }

    private void Watch()
    {
        if (_bar is not null)
        {
            _bar.ValueChanged += OnBarChanged;
        }

        if (_alpha is not null)
        {
            _alpha.ValueChanged += OnAlphaChanged;
        }

        if (_field is not null)
        {
            _field.PropertyChanged += OnPartResized;
        }

        if (_wheel is not null)
        {
            _wheel.PropertyChanged += OnPartResized;
        }

        if (_hex is not null)
        {
            _hex.KeyDown += OnHexKey;
            _hex.LostFocus += OnHexLeft;
        }

        AddHandler(Button.ClickEvent, OnClick);
    }

    private void Unwatch()
    {
        if (_bar is not null)
        {
            _bar.ValueChanged -= OnBarChanged;
        }

        if (_alpha is not null)
        {
            _alpha.ValueChanged -= OnAlphaChanged;
        }

        if (_field is not null)
        {
            _field.PropertyChanged -= OnPartResized;
        }

        if (_wheel is not null)
        {
            _wheel.PropertyChanged -= OnPartResized;
        }

        if (_hex is not null)
        {
            _hex.KeyDown -= OnHexKey;
            _hex.LostFocus -= OnHexLeft;
        }

        RemoveHandler(Button.ClickEvent, OnClick);
    }

    /// <summary>
    /// A list that reports its own changes is followed, so a host that adds a colour while
    /// the picker is open does not have to tell it.
    /// </summary>
    private void Rewatch(IList<ColorValue>? old, IList<ColorValue>? fresh)
    {
        if (old is INotifyCollectionChanged before)
        {
            before.CollectionChanged -= OnListChanged;
        }

        if (fresh is INotifyCollectionChanged after)
        {
            after.CollectionChanged += OnListChanged;
        }
    }

    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e) => Show();

    private void OnPartResized(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            Place();
        }
    }

    private void OnBarChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_quiet)
        {
            return;
        }

        if (Shape == ColorShape.Wheel)
        {
            Turn(_hue, _saturation, e.NewValue);
        }
        else
        {
            Turn(e.NewValue, _saturation, _value);
        }
    }

    private void OnAlphaChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_quiet)
        {
            Write(Color.WithAlpha(e.NewValue));
        }
    }

    private void OnHexKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter)
        {
            TakeHex();
            e.Handled = true;
        }
        else if (e.Key is Key.Escape)
        {
            ShowHex();
            e.Handled = true;
        }
    }

    private void OnHexLeft(object? sender, RoutedEventArgs e) => TakeHex();

    private void TakeHex()
    {
        if (ColorValue.TryParse(_hex?.Text, out var parsed))
        {
            Write(parsed);
        }

        // Whatever was typed, the field goes back to reading the colour.
        ShowHex();
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        switch (e.Source)
        {
            case Button { Name: ApplyPart }:
                RaiseEvent(new RoutedEventArgs(AppliedEvent));
                break;

            case Button { Name: CancelPart }:
                RaiseEvent(new RoutedEventArgs(CancelledEvent));
                break;

            case Button { Name: CopyPart }:
                Copy();
                break;

            case Button { Name: AddSwatchPart }:
                Keep();
                break;

            // A swatch or a recent, which carry their colour as their data.
            case Button { DataContext: ColorValue swatch }:
                SetCurrentValue(ColorProperty, swatch);
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    private async void Copy()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetValueAsync(DataFormat.Text, Color.ToHex());
        }
    }

    private void Keep()
    {
        if (Swatches is { IsReadOnly: false } swatches && !swatches.Contains(Color))
        {
            swatches.Add(Color);
        }

        RaiseEvent(new ColorEventArgs(SwatchAddedEvent, Color));
    }

    private void Pick(Control field, PointerEventArgs e)
    {
        e.Pointer.Capture(field);

        var at = e.GetPosition(field);
        var width = Math.Max(field.Bounds.Width, 1);
        var height = Math.Max(field.Bounds.Height, 1);

        Turn(_hue, Clamp01(at.X / width), Clamp01(1 - at.Y / height));
    }

    private void Spin(Control wheel, PointerEventArgs e)
    {
        e.Pointer.Capture(wheel);

        var at = e.GetPosition(wheel);
        var radius = Math.Max(Math.Min(wheel.Bounds.Width, wheel.Bounds.Height) / 2, 1);
        var x = at.X - wheel.Bounds.Width / 2;
        var y = at.Y - wheel.Bounds.Height / 2;

        // Screen coordinates run down the page, so the angle is negated to turn the way a
        // colour wheel is read.
        var hue = Math.Atan2(-y, x) * 180 / Math.PI;

        Turn(hue < 0 ? hue + 360 : hue, Clamp01(Math.Sqrt(x * x + y * y) / radius), _value);
    }

    /// <summary>Paints every part from the colour, and never the other way about.</summary>
    private void Show()
    {
        _quiet = true;

        try
        {
            var opaque = new ColorValue(Color.R, Color.G, Color.B, 1).ToColor();
            var hue = ColorValue.FromHsv(_hue, 1, 1, 1).ToColor();

            if (_fieldHue is not null)
            {
                _fieldHue.Background = Sweep(Colors.White, hue, horizontal: true);
            }

            if (_new is not null)
            {
                _new.Background = new SolidColorBrush(Color.ToColor());
            }

            if (_old is not null)
            {
                _old.Background = new SolidColorBrush(Previous.ToColor());
            }

            if (_wheelShade is not null)
            {
                _wheelShade.Opacity = 1 - _value;
            }

            if (_bar is not null)
            {
                var wheel = Shape == ColorShape.Wheel;

                // The wheel already carries hue, so the bar beside it is value instead,
                // full at the top either way.
                _bar.Background = wheel ? Sweep(hue, Colors.Black, horizontal: false) : Rainbow(horizontal: false);
                _bar.IsDirectionReversed = !wheel;
                _bar.Maximum = wheel ? 1 : 360;
                _bar.Value = wheel ? _value : _hue;
            }

            if (_alpha is not null)
            {
                _alpha.Background = Fade();
                _alpha.Value = Color.A;
            }

            if (_recent is not null)
            {
                _recent.IsVisible = Recent is { Count: > 0 };
            }

            var literal = Literal();

            if (_literal is not null)
            {
                _literal.Text = literal;
            }

            if (_literalFoot is not null)
            {
                _literalFoot.Text = literal;
            }

            ShowHex();
            Fill();
            Place();
        }
        finally
        {
            _quiet = false;
        }
    }

    private void ShowHex()
    {
        if (_hex is not null && !_hex.IsFocused)
        {
            _hex.Text = Color.ToHex();
        }
    }

    /// <summary>The line a person copies into Godot, which is where this colour is going.</summary>
    private string Literal() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Color({Color.R:0.###}, {Color.G:0.###}, {Color.B:0.###}, {Color.A:0.###})");

    /// <summary>Puts today's numbers and ramps into the rows.</summary>
    private void Fill()
    {
        var (okHue, okSaturation, okLightness) = Color.ToOkhsl();

        foreach (var channel in Channels)
        {
            var (number, ramp) = (channel.Label, Mode) switch
            {
                ("EV", _) => (Exposure, Token("SurfaceWell", "InkTitle")),

                ("R", ColorValueMode.Raw) => (Color.R, Along(0)),
                ("G", ColorValueMode.Raw) => (Color.G, Along(1)),
                ("B", ColorValueMode.Raw) => (Color.B, Along(2)),
                ("A", ColorValueMode.Raw) => (Color.A, Fade()),

                ("R", _) => (Color.R * 255, Along(0)),
                ("G", _) => (Color.G * 255, Along(1)),
                ("B", _) => (Color.B * 255, Along(2)),

                ("H", ColorValueMode.Okhsl) => (okHue, Curve(step => ColorValue.FromOkhsl(step * 360, okSaturation, okLightness, 1))),
                ("S", ColorValueMode.Okhsl) => (okSaturation * 100, Curve(step => ColorValue.FromOkhsl(okHue, step, okLightness, 1))),
                ("L", ColorValueMode.Okhsl) => (okLightness * 100, Curve(step => ColorValue.FromOkhsl(okHue, okSaturation, step, 1))),
                ("A", ColorValueMode.Okhsl) => (Color.A * 100, Fade()),

                ("H", _) => (_hue, Rainbow(horizontal: true)),
                ("S", _) => (_saturation * 100, Line(ColorValue.FromHsv(_hue, 0, _value, 1), ColorValue.FromHsv(_hue, 1, _value, 1))),
                ("V", _) => (_value * 100, Line(ColorValue.FromHsv(_hue, _saturation, 0, 1), ColorValue.FromHsv(_hue, _saturation, 1, 1))),

                _ => (Color.A * 255, Fade()),
            };

            channel.IsQuiet = true;
            channel.Value = number;
            channel.Position = Clamp01(
                (number - channel.RampMinimum) / Math.Max(channel.RampMaximum - channel.RampMinimum, double.Epsilon));
            channel.Ramp = ramp;
            channel.IsQuiet = false;
        }
    }

    /// <summary>
    /// Stands both handles where the colour says. A canvas places a child by its corner, so
    /// each one is pulled back by half of itself to sit on the point.
    /// </summary>
    private void Place()
    {
        if (_field is not null && _fieldHandle is not null)
        {
            Stand(_fieldHandle, _saturation * _field.Bounds.Width, (1 - _value) * _field.Bounds.Height);
        }

        if (_wheel is not null && _wheelHandle is not null)
        {
            var radius = Math.Min(_wheel.Bounds.Width, _wheel.Bounds.Height) / 2;
            var angle = _hue * Math.PI / 180;

            Stand(
                _wheelHandle,
                radius + Math.Cos(angle) * radius * _saturation,
                radius - Math.Sin(angle) * radius * _saturation);
        }

        static void Stand(Control handle, double x, double y)
        {
            Canvas.SetLeft(handle, x - handle.Bounds.Width / 2);
            Canvas.SetTop(handle, y - handle.Bounds.Height / 2);
        }
    }

    /// <summary>
    /// The fills that never change with the colour, put on once. The wheel and the shade
    /// are built here rather than written as brushes, since a hue is computed and the two
    /// that come from the palette are looked up by key.
    /// </summary>
    private void Dress()
    {
        Host();

        if (_wheelHue is not null)
        {
            // A conic brush starts at the top and sweeps clockwise, so a quarter turn puts
            // hue zero on the right where the handle's own angle expects it.
            var wheel = new ConicGradientBrush { Center = RelativePoint.Center, Angle = 90 };

            // Anticlockwise from the right, which is the way a hue circle is read, and the
            // first stop again at the end so the sweep meets itself.
            for (var step = 0; step <= 12; step++)
            {
                var offset = (double)step / 12;

                wheel.GradientStops.Add(new GradientStop(
                    ColorValue.FromHsv(-offset * 360, 1, 1, 1).ToColor(), offset));
            }

            _wheelHue.Fill = wheel;
        }

        if (_wheelBloom is not null)
        {
            _wheelBloom.Fill = new RadialGradientBrush
            {
                Center = RelativePoint.Center,
                GradientOrigin = RelativePoint.Center,
                GradientStops =
                {
                    new GradientStop(Colors.White, 0),
                    new GradientStop(Avalonia.Media.Color.FromArgb(0, 255, 255, 255), 1),
                },
            };
        }

        if (_wheelShade is not null)
        {
            _wheelShade.Fill = Brushes.Black;
        }

        if (_fieldShade is not null)
        {
            _fieldShade.Background = Sweep(Colors.Transparent, Colors.Black, horizontal: false);
        }

        if (_checker is not null)
        {
            _checker.Background = Checker();
        }
    }

    /// <summary>
    /// The three things the host decides. The frame is the shell's own class, and the two
    /// halves of the footer are swapped here because they sit inside the frame's footer,
    /// which a template selector on this control does not reach.
    /// </summary>
    private void Host()
    {
        _frame?.Classes.Set("inPanel", IsInPanel);

        if (_actions is not null)
        {
            _actions.IsVisible = !IsInPanel;
        }

        if (_literalFoot is not null)
        {
            _literalFoot.IsVisible = IsInPanel;
        }
    }

    /// <summary>The squares behind a colour that is not opaque.</summary>
    private IBrush? Checker()
    {
        if (Resource("CheckerDark") is not ISolidColorBrush dark
            || Resource("CheckerLight") is not ISolidColorBrush light
            || !this.TryFindResource("SizeColorChecker", out var size)
            || size is not double square)
        {
            return null;
        }

        var tile = new Rect(0, 0, square * 2, square * 2);

        return new DrawingBrush
        {
            TileMode = TileMode.Tile,
            SourceRect = new RelativeRect(tile, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(tile, RelativeUnit.Absolute),
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing
                    {
                        Brush = new SolidColorBrush(dark.Color),
                        Geometry = new RectangleGeometry(tile),
                    },
                    new GeometryDrawing
                    {
                        Brush = new SolidColorBrush(light.Color),
                        Geometry = new RectangleGeometry(new Rect(0, 0, square, square)),
                    },
                    new GeometryDrawing
                    {
                        Brush = new SolidColorBrush(light.Color),
                        Geometry = new RectangleGeometry(new Rect(square, square, square, square)),
                    },
                },
            },
        };
    }

    /// <summary>A ramp between two palette entries, so their hex stays in Tokens.axaml.</summary>
    private IBrush? Token(string from, string to) =>
        Resource(from) is ISolidColorBrush start && Resource(to) is ISolidColorBrush end
            ? Sweep(start.Color, end.Color, horizontal: true)
            : null;

    private IBrush? Resource(string key) =>
        this.TryFindResource(key, out var found) ? found as IBrush : null;

    /// <summary>One colour channel swept from nothing to all of it, with the others held.</summary>
    private LinearGradientBrush Along(int channel) =>
        Line(
            new ColorValue(channel == 0 ? 0 : Color.R, channel == 1 ? 0 : Color.G, channel == 2 ? 0 : Color.B, 1),
            new ColorValue(channel == 0 ? 1 : Color.R, channel == 1 ? 1 : Color.G, channel == 2 ? 1 : Color.B, 1));

    private LinearGradientBrush Fade()
    {
        var opaque = new ColorValue(Color.R, Color.G, Color.B, 1).ToColor();

        return Sweep(
            Avalonia.Media.Color.FromArgb(0, opaque.R, opaque.G, opaque.B), opaque, horizontal: true);
    }

    private static LinearGradientBrush Sweep(Color from, Color to, bool horizontal) =>
        new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(horizontal ? 1 : 0, horizontal ? 0 : 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(from, 0),
                new GradientStop(to, 1),
            },
        };

    private static LinearGradientBrush Line(ColorValue from, ColorValue to) =>
        Sweep(from.ToColor(), to.ToColor(), horizontal: true);

    /// <summary>A ramp sampled along a curve, for a space that is not straight in sRGB.</summary>
    private static LinearGradientBrush Curve(Func<double, ColorValue> along)
    {
        var brush = Steps(horizontal: true, 12, step => along(step).ToColor());

        return brush;
    }

    private static LinearGradientBrush Rainbow(bool horizontal) =>
        Steps(horizontal, 6, step => ColorValue.FromHsv(step * 360, 1, 1, 1).ToColor());

    private static LinearGradientBrush Steps(bool horizontal, int count, Func<double, Color> at)
    {
        var brush = Sweep(Colors.Black, Colors.White, horizontal);

        brush.GradientStops.Clear();

        for (var step = 0; step <= count; step++)
        {
            var offset = (double)step / count;

            brush.GradientStops.Add(new GradientStop(at(offset), offset));
        }

        return brush;
    }
}
