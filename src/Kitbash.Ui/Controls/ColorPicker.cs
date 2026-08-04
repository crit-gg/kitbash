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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

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
[TemplatePart(ShapePart, typeof(Control))]
[TemplatePart(RingPart, typeof(Ellipse))]
[TemplatePart(RingHandlePart, typeof(Control))]
[TemplatePart(DiscPart, typeof(Control))]
[TemplatePart(FlatPart, typeof(ColorSurface))]
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
    /// Whether alpha can be edited. Turning it off drops the alpha lane and the alpha row,
    /// and leaves whatever alpha the colour arrived with.
    /// </summary>
    public static readonly StyledProperty<bool> EditsAlphaProperty =
        AvaloniaProperty.Register<ColorPicker, bool>(nameof(EditsAlpha), true);

    /// <summary>
    /// Whether the exposure can be edited. Turning it off puts the stops back into the
    /// colour and drops the row, so a host that has no use for HDR sees none of it.
    /// </summary>
    public static readonly StyledProperty<bool> EditsExposureProperty =
        AvaloniaProperty.Register<ColorPicker, bool>(nameof(EditsExposure), true);

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

    /// <summary>A saved colour was right clicked, carrying the colour that was taken off.</summary>
    public static readonly RoutedEvent<ColorEventArgs> SwatchRemovedEvent =
        RoutedEvent.Register<ColorPicker, ColorEventArgs>(nameof(SwatchRemoved), RoutingStrategies.Bubble);

    private const string FramePart = "PART_Frame";
    private const string FieldPart = "PART_Field";
    private const string FieldHuePart = "PART_FieldHue";
    private const string FieldShadePart = "PART_FieldShade";
    private const string FieldHandlePart = "PART_FieldHandle";
    private const string ShapeRowPart = "PART_ShapeRow";
    private const string ShapePart = "PART_Shape";
    private const string RingPart = "PART_Ring";
    private const string RingHandlePart = "PART_RingHandle";
    private const string DiscPart = "PART_Disc";
    private const string DiscHuePart = "PART_DiscHue";
    private const string DiscBloomPart = "PART_DiscBloom";
    private const string DiscShadePart = "PART_DiscShade";
    private const string DiscOkPart = "PART_DiscOk";
    private const string FlatPart = "PART_Flat";
    private const string ShapeMarkPart = "PART_ShapeMark";
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
    private const string SwatchesPart = "PART_Swatches";
    private const string OverbrightPart = "PART_Overbright";
    private const string RevertPart = "PART_Revert";
    private const string CopyPart = "PART_Copy";
    private const string AddSwatchPart = "PART_AddSwatch";
    private const string ApplyPart = "PART_Apply";
    private const string CancelPart = "PART_Cancel";

    /// <summary>
    /// Where the hue ring gives way to the square inside it, as a fraction of the radius.
    /// Godot's WHEEL_RADIUS, and the square is inscribed in that circle.
    /// </summary>
    private const double RingInner = 0.84;

    /// <summary>Half the side of a square inscribed in a circle of radius 1.</summary>
    private const double Root = 0.70710678118654752;

    /// <summary>The stops the exposure ramp reaches, either side of nothing. Godot's own.</summary>
    private const double ExposureReach = 10;

    /// <summary>How many colours the recent row holds, which is Godot's own count.</summary>
    private const int RecentKept = 9;

    /// <summary>How far the exposure can be typed past the end of its ramp.</summary>
    private const double ExposureCeiling = 32;

    /// <summary>
    /// Hue stops one short of the turn, since 360 and 0 are the same colour and a spinbox
    /// that reaches both has a value it can never step off. Godot's max is 359 as well.
    /// </summary>
    private const double HueReach = 359;

    /// <summary>
    /// How far a channel can be typed past the end of its ramp. The excess is moved into
    /// the exposure as soon as it is typed, so these only bound the field itself.
    /// </summary>
    private const double ByteCeiling = 255 * 64;
    private const double LinearCeiling = 64;

    private Popover? _frame;
    private Control? _field;
    private Border? _fieldHue;
    private Border? _fieldShade;
    private Control? _fieldHandle;
    private Control? _shapeRow;
    private Control? _shape;
    private Ellipse? _ring;
    private Control? _ringHandle;
    private Control? _disc;
    private Ellipse? _discHue;
    private Ellipse? _discBloom;
    private Ellipse? _discShade;
    private ColorSurface? _discOk;
    private ColorSurface? _flat;
    private Icon? _shapeMark;
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
    private Control? _swatches;
    private Control? _overbright;
    private Control? _revert;

    /// <summary>
    /// The colour with the exposure taken out of it, which is what every control here draws
    /// and edits. Its three channels are inside 0 to 1, and the exposure carries the rest.
    /// </summary>
    private ColorValue _base = new(1, 1, 1, 1);

    /// <summary>
    /// Where the picker is standing in hue, saturation and value. A grey has no hue and
    /// black has no saturation, so both are held here rather than read back from the colour,
    /// which is what stops the field jumping while it is being dragged.
    /// </summary>
    private double _hue;
    private double _saturation;
    private double _value = 1;

    /// <summary>The same for the perceptual space, which has its own hue and saturation.</summary>
    private double _okHue;
    private double _okSaturation;
    private double _okLightness = 1;

    /// <summary>True while the picker is writing to its own parts, so nothing echoes back.</summary>
    private bool _quiet;

    /// <summary>What the colour was when the pointer went down, so a release can tell.</summary>
    private ColorValue _pressed;

    /// <summary>True while a drag is turning the hue ring rather than picking in the square.</summary>
    private bool _spinning;

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

    /// <inheritdoc cref="SwatchRemovedEvent"/>
    public event EventHandler<ColorEventArgs>? SwatchRemoved
    {
        add => AddHandler(SwatchRemovedEvent, value);
        remove => RemoveHandler(SwatchRemovedEvent, value);
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

    /// <inheritdoc cref="EditsAlphaProperty"/>
    public bool EditsAlpha
    {
        get => GetValue(EditsAlphaProperty);
        set => SetValue(EditsAlphaProperty, value);
    }

    /// <inheritdoc cref="EditsExposureProperty"/>
    public bool EditsExposure
    {
        get => GetValue(EditsExposureProperty);
        set => SetValue(EditsExposureProperty, value);
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
        _shapeRow = e.NameScope.Find<Control>(ShapeRowPart);
        _shape = e.NameScope.Find<Control>(ShapePart);
        _ring = e.NameScope.Find<Ellipse>(RingPart);
        _ringHandle = e.NameScope.Find<Control>(RingHandlePart);
        _disc = e.NameScope.Find<Control>(DiscPart);
        _discHue = e.NameScope.Find<Ellipse>(DiscHuePart);
        _discBloom = e.NameScope.Find<Ellipse>(DiscBloomPart);
        _discShade = e.NameScope.Find<Ellipse>(DiscShadePart);
        _discOk = e.NameScope.Find<ColorSurface>(DiscOkPart);
        _flat = e.NameScope.Find<ColorSurface>(FlatPart);
        _shapeMark = e.NameScope.Find<Icon>(ShapeMarkPart);
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
        _swatches = e.NameScope.Find<Control>(SwatchesPart);
        _overbright = e.NameScope.Find<Control>(OverbrightPart);
        _revert = e.NameScope.Find<Control>(RevertPart);

        if (e.NameScope.Find<ItemsControl>(ChannelsPart) is { } rows)
        {
            rows.ItemsSource = Channels;
        }

        // Each item sets its own shape. They live in a flyout, which is its own visual root,
        // so a click there never bubbles to this control and has to be taken at the item.
        foreach (var shape in Enum.GetValues<ColorShape>())
        {
            if (e.NameScope.Find<MenuItem>("PART_Shape" + shape) is { } item)
            {
                item.Click += (_, _) => SetCurrentValue(ShapeProperty, shape);
            }
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
            // An edit from inside has already done all of this. What is left is a colour
            // handed in from outside, which is split into a base and the stops over it.
            if (_quiet)
            {
                return;
            }

            Split();
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
        else if (change.Property == EditsAlphaProperty)
        {
            Build();
            Show();
        }
        else if (change.Property == EditsExposureProperty)
        {
            // The stops go back into the colour rather than being dropped, so turning the
            // row off never changes the value.
            if (!EditsExposure)
            {
                _base = Color;
                SetCurrentValue(ExposureProperty, 0d);
            }

            Build();
            Show();
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

        _pressed = Color;

        // A right click takes a saved colour off the list, which is Godot's own gesture.
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed
            && _swatches is not null
            && e.Source is Visual clicked
            && clicked.GetVisualAncestors().Contains(_swatches)
            && Ancestor(clicked) is { DataContext: ColorValue saved })
        {
            Forget(saved);
            e.Handled = true;

            return;
        }

        // A press on the old half of the chip puts that colour back, which is what the
        // revert mark on it says.
        if (_old is not null
            && e.Source is Visual source
            && (ReferenceEquals(source, _old) || source.GetVisualAncestors().Contains(_old)))
        {
            SetCurrentValue(ColorProperty, Previous);
            e.Handled = true;

            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || _shape is not { IsVisible: true } shape
            || !Inside(shape, e))
        {
            return;
        }

        // The ring is the only shape with two areas, so which one the press landed in is
        // decided once and held for the whole drag.
        _spinning = Shape == ColorShape.Wheel && OnRing(shape, e);

        e.Pointer.Capture(shape);
        Aim(shape, e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && _shape is { } shape
            && ReferenceEquals(e.Pointer.Captured, shape))
        {
            Aim(shape, e);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (ReferenceEquals(e.Pointer.Captured, _shape))
        {
            e.Pointer.Capture(null);
        }

        _spinning = false;

        // A colour a person landed on is a recent one, which is the end of an interaction
        // rather than every step of it. Godot does the same on the same events.
        if (!_pressed.Equals(Color))
        {
            Remember();
        }
    }

    /// <summary>Whether a press landed on the hue band rather than inside the square.</summary>
    private bool OnRing(Control shape, PointerEventArgs e)
    {
        var middle = Middle(shape);
        var at = e.GetPosition(shape);
        var radius = Math.Min(middle.X, middle.Y);
        var away = Math.Sqrt(Math.Pow(at.X - middle.X, 2) + Math.Pow(at.Y - middle.Y, 2));

        return away >= radius * RingInner;
    }

    /// <summary>Reads a pointer against whichever shape is up.</summary>
    private void Aim(Control shape, PointerEventArgs e)
    {
        var at = e.GetPosition(shape);
        var width = Math.Max(shape.Bounds.Width, 1);
        var height = Math.Max(shape.Bounds.Height, 1);

        switch (Shape)
        {
            case ColorShape.Wheel when _spinning:
                Turn(Angle(shape, at), _saturation, _value);
                break;

            case ColorShape.Wheel:
                var side = Math.Min(width, height) / 2 * RingInner * Root;
                var corner = Middle(shape) - new Point(side, side);

                Turn(
                    _hue,
                    Clamp01((at.X - corner.X) / (side * 2)),
                    Clamp01(1 - (at.Y - corner.Y) / (side * 2)));
                break;

            case ColorShape.Circle:
                Turn(Angle(shape, at), Reach(shape, at), _value);
                break;

            case ColorShape.OkhslCircle:
                Shift(Angle(shape, at), Reach(shape, at), _okLightness);
                break;

            case ColorShape.OkhsRectangle:
                Shift(at.X / width * 360, Clamp01(1 - at.Y / height), _okLightness);
                break;

            case ColorShape.OkhlRectangle:
                Shift(at.X / width * 360, _okSaturation, Clamp01(1 - at.Y / height));
                break;

            default:
                Turn(_hue, Clamp01(at.X / width), Clamp01(1 - at.Y / height));
                break;
        }
    }

    /// <summary>
    /// The hue a point stands at, read the way Godot reads it, which is clockwise from the
    /// right because the screen's own y runs down the page.
    /// </summary>
    private static double Angle(Control shape, Point at)
    {
        var middle = Middle(shape);
        var degrees = Math.Atan2(at.Y - middle.Y, at.X - middle.X) * 180 / Math.PI;

        return degrees < 0 ? degrees + 360 : degrees;
    }

    /// <summary>How far out of the middle a point is, as a fraction of the disc.</summary>
    private static double Reach(Control shape, Point at)
    {
        var middle = Middle(shape);
        var radius = Math.Max(Math.Min(middle.X, middle.Y), 1);

        return Math.Clamp(
            Math.Sqrt(Math.Pow(at.X - middle.X, 2) + Math.Pow(at.Y - middle.Y, 2)) / radius, 0, 1);
    }

    private static Point Middle(Control shape) =>
        new(shape.Bounds.Width / 2, shape.Bounds.Height / 2);

    /// <summary>The swatch a press landed in, which may be the tile or something inside it.</summary>
    private static Button? Ancestor(Visual from)
    {
        for (Visual? step = from; step is not null; step = step.GetVisualParent())
        {
            if (step is Button button)
            {
                return button;
            }
        }

        return null;
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
                Add("H", HueReach, 1, "0", value => Turn(value, _saturation, _value));
                Add("S", 100, 1, "0", value => Turn(_hue, value / 100, _value));
                Add("V", 100, 1, "0", value => Turn(_hue, _saturation, value / 100));
                Alpha(255, 1, "0");
                break;

            case ColorValueMode.Linear:
                Add("R", 1, 0.001, "0.000", value => Light(0, value), LinearCeiling);
                Add("G", 1, 0.001, "0.000", value => Light(1, value), LinearCeiling);
                Add("B", 1, 0.001, "0.000", value => Light(2, value), LinearCeiling);
                Alpha(1, 0.001, "0.000");
                break;

            case ColorValueMode.Okhsl:
                Add("H", HueReach, 1, "0", value => Shift(value, _okSaturation, _okLightness));
                Add("S", 100, 1, "0", value => Shift(_okHue, value / 100, _okLightness));
                Add("L", 100, 1, "0", value => Shift(_okHue, _okSaturation, value / 100));
                Alpha(255, 1, "0");
                break;

            default:
                Add("R", 255, 1, "0", value => Paint(0, value / 255), ByteCeiling);
                Add("G", 255, 1, "0", value => Paint(1, value / 255), ByteCeiling);
                Add("B", 255, 1, "0", value => Paint(2, value / 255), ByteCeiling);
                Alpha(255, 1, "0");
                break;
        }

        // Every mode carries it, so a value above 1 can be reached from any of them. Godot
        // calls this the intensity and the design calls it EV, and it is the same number.
        if (EditsExposure)
        {
            Channels.Add(new ColorChannel("EV", -ExposureCeiling, ExposureCeiling, 1, "+0.00;-0.00;+0.00")
            {
                RampMinimum = -ExposureReach,
                RampMaximum = ExposureReach,
                Edited = Expose,
            });
        }

        void Alpha(double ramp, double step, string format)
        {
            if (EditsAlpha)
            {
                Add("A", ramp, step, format, value => Write(_base.WithAlpha(value / ramp)));
            }
        }

        void Add(string label, double ramp, double step, string format, Action<double> edit, double? ceiling = null)
        {
            Channels.Add(new ColorChannel(label, 0, ceiling ?? ramp, step, format)
            {
                RampMaximum = ramp,
                Edited = edit,
            });
        }
    }

    /// <summary>One channel of the base colour, as it is written.</summary>
    private void Paint(int channel, double value) =>
        Write(new ColorValue(
            channel == 0 ? value : _base.R,
            channel == 1 ? value : _base.G,
            channel == 2 ? value : _base.B,
            _base.A));

    /// <summary>One channel of the base colour, as light.</summary>
    private void Light(int channel, double value)
    {
        var linear = _base.ToLinear();

        Write(new ColorValue(
            channel == 0 ? value : linear.R,
            channel == 1 ? value : linear.G,
            channel == 2 ? value : linear.B,
            linear.A).ToSrgb());
    }

    private void Turn(double hue, double saturation, double value)
    {
        _hue = hue;
        _saturation = Clamp01(saturation);
        _value = Clamp01(value);

        Write(ColorValue.FromHsv(_hue, _saturation, _value, _base.A));
    }

    private void Shift(double hue, double saturation, double lightness)
    {
        _okHue = hue;
        _okSaturation = Clamp01(saturation);
        _okLightness = Clamp01(lightness);

        Write(ColorValue.FromOkhsl(_okHue, _okSaturation, _okLightness, _base.A));
    }

    private void Expose(double stops)
    {
        SetCurrentValue(ExposureProperty, Math.Clamp(stops, -ExposureCeiling, ExposureCeiling));
        Compose();
        Show();
    }

    /// <summary>
    /// An edit to the base colour. A channel over 1 is not held there: the excess moves into
    /// the exposure, which is what keeps every other control able to draw the colour.
    /// </summary>
    private void Write(ColorValue color)
    {
        if (color.IsOverbright)
        {
            var linear = color.ToLinear();
            var over = Math.Max(1, Math.Max(linear.R, Math.Max(linear.G, linear.B)));

            color = new ColorValue(linear.R / over, linear.G / over, linear.B / over, linear.A).ToSrgb();
            SetCurrentValue(ExposureProperty, Math.Clamp(Exposure + Math.Log2(over), -ExposureCeiling, ExposureCeiling));
        }

        _base = color;

        Compose();
        Read();
        Show();
    }

    /// <summary>Puts the exposure back on the base colour, which is the value a host reads.</summary>
    private void Compose()
    {
        _quiet = true;

        try
        {
            SetCurrentValue(ColorProperty, Lit(_base, Exposure));
        }
        finally
        {
            _quiet = false;
        }
    }

    /// <summary>
    /// Takes the base colour and the stops out of a colour handed in. The multiplier is
    /// never below 1, so a colour a screen can show always reads as no stops at all.
    /// </summary>
    private void Split()
    {
        var linear = Color.ToLinear();
        var over = Math.Max(1, Math.Max(linear.R, Math.Max(linear.G, linear.B)));

        _base = new ColorValue(linear.R / over, linear.G / over, linear.B / over, linear.A).ToSrgb();

        SetCurrentValue(ExposureProperty, Math.Log2(over));
    }

    /// <summary>
    /// A colour under so many stops of exposure. The multiply is in light rather than in the
    /// numbers, which is what makes one stop twice as bright.
    /// </summary>
    private static ColorValue Lit(ColorValue color, double stops)
    {
        if (stops == 0)
        {
            return color;
        }

        var linear = color.ToLinear();
        var factor = Math.Pow(2, stops);

        return new ColorValue(linear.R * factor, linear.G * factor, linear.B * factor, linear.A).ToSrgb();
    }

    /// <summary>Takes the standing hue, saturation and value from the base colour.</summary>
    private void Read()
    {
        var (hue, saturation, value) = _base.ToHsv();

        // A grey has no hue and black has no saturation, so neither is taken from a colour
        // that cannot carry it. Godot caches the same two, for the same reason.
        if (saturation > 0)
        {
            _hue = hue;
        }

        if (value > 0)
        {
            _saturation = saturation;
        }

        _value = value;

        var (okHue, okSaturation, okLightness) = _base.ToOkhsl();

        if (okSaturation > 0)
        {
            _okHue = okHue;
        }

        if (okLightness is > 0 and < 1)
        {
            _okSaturation = okSaturation;
        }

        _okLightness = okLightness;
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

        if (_shape is not null)
        {
            _shape.PropertyChanged += OnPartResized;
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

        if (_shape is not null)
        {
            _shape.PropertyChanged -= OnPartResized;
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

        switch (Shape)
        {
            case ColorShape.Circle:
                Turn(_hue, _saturation, e.NewValue);
                break;

            case ColorShape.OkhslCircle or ColorShape.OkhsRectangle:
                Shift(_okHue, _okSaturation, e.NewValue);
                break;

            case ColorShape.OkhlRectangle:
                Shift(_okHue, e.NewValue, _okLightness);
                break;

            default:
                Turn(e.NewValue, _saturation, _value);
                break;
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
            await clipboard.SetValueAsync(DataFormat.Text, Color.ToText());
        }
    }

    /// <summary>
    /// Saves today's colour. One that is already saved moves to the end rather than landing
    /// twice, which is what Godot's own preset list does.
    /// </summary>
    private void Keep()
    {
        if (Swatches is { IsReadOnly: false } swatches)
        {
            swatches.Remove(Color);
            swatches.Add(Color);
        }

        RaiseEvent(new ColorEventArgs(SwatchAddedEvent, Color));
        Show();
    }

    /// <summary>
    /// Puts the colour at the front of the recent row. The row holds nine, which is Godot's
    /// own count, and the oldest falls off the end.
    /// </summary>
    private void Remember()
    {
        if (Recent is not { IsReadOnly: false } recent)
        {
            return;
        }

        recent.Remove(Color);
        recent.Insert(0, Color);

        while (recent.Count > RecentKept)
        {
            recent.RemoveAt(recent.Count - 1);
        }

        Show();
    }

    /// <summary>Takes a saved colour off the list, which is what a right click does.</summary>
    private void Forget(ColorValue color)
    {
        if (Swatches is { IsReadOnly: false } swatches && swatches.Remove(color))
        {
            RaiseEvent(new ColorEventArgs(SwatchRemovedEvent, color));
            Show();
        }
    }

    /// <summary>Paints every part from the colour, and never the other way about.</summary>
    private void Show()
    {
        _quiet = true;

        try
        {
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

            Shapes(hue);

            if (_alpha is not null)
            {
                _alpha.Background = Fade();
                _alpha.Value = _base.A;
            }

            if (_overbright is not null)
            {
                _overbright.IsVisible = Color.IsOverbright;
            }

            if (_revert is not null)
            {
                _revert.IsVisible = !Previous.Equals(Color);
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

    /// <summary>
    /// The hex while the colour has one and the expression otherwise, which is what Godot's
    /// own field does. The value is the whole colour, exposure and all.
    /// </summary>
    /// <summary>
    /// Which parts the shape needs, what the bar beside it carries, and the fills that
    /// follow the colour. Seven shapes over five parts, so the control does it rather than
    /// a selector for each pair.
    /// </summary>
    private void Shapes(Color hue)
    {
        var wheel = Shape == ColorShape.Wheel;
        var disc = Shape is ColorShape.Circle or ColorShape.OkhslCircle;
        var perceptual = Shape is ColorShape.OkhslCircle;
        var flat = Shape is ColorShape.OkhsRectangle or ColorShape.OkhlRectangle;

        if (_shapeRow is not null)
        {
            _shapeRow.IsVisible = Shape != ColorShape.Sliders;
        }

        if (_field is not null)
        {
            _field.IsVisible = Shape is ColorShape.Rectangle or ColorShape.Wheel;
        }

        if (_ring is not null)
        {
            _ring.IsVisible = wheel;
        }

        if (_ringHandle is not null)
        {
            _ringHandle.IsVisible = wheel;
        }

        if (_disc is not null)
        {
            _disc.IsVisible = disc;
        }

        if (_discHue is not null)
        {
            _discHue.IsVisible = !perceptual;
        }

        if (_discBloom is not null)
        {
            _discBloom.IsVisible = !perceptual;
        }

        if (_discShade is not null)
        {
            _discShade.IsVisible = !perceptual;
            _discShade.Opacity = 1 - _value;
        }

        if (_discOk is not null)
        {
            _discOk.IsVisible = perceptual;
            _discOk.Third = _okLightness;
        }

        if (_flat is not null)
        {
            _flat.IsVisible = flat;
            _flat.Kind = Shape == ColorShape.OkhlRectangle ? ColorShape.OkhlRectangle : ColorShape.OkhsRectangle;
            _flat.Third = Shape == ColorShape.OkhlRectangle ? _okSaturation : _okLightness;
        }

        if (_shapeMark is not null)
        {
            _shapeMark.Glyph = Shape switch
            {
                ColorShape.Wheel or ColorShape.Circle or ColorShape.OkhslCircle => IconGlyph.Crosshair,
                ColorShape.Sliders => IconGlyph.Columns,
                _ => IconGlyph.SelectAll,
            };
        }

        if (_bar is null)
        {
            return;
        }

        // The bar carries whatever the shape does not. The wheel carries all three itself.
        _bar.IsVisible = !wheel;
        _bar.IsDirectionReversed = false;
        _bar.Maximum = 1;

        switch (Shape)
        {
            case ColorShape.Circle:
                _bar.Background = Sweep(hue, Colors.Black, horizontal: false);
                _bar.Value = _value;
                break;

            case ColorShape.OkhslCircle or ColorShape.OkhsRectangle:
                _bar.Background = Steps(false, 8, step =>
                    ColorValue.FromOkhsl(_okHue, _okSaturation, 1 - step, 1).ToColor());
                _bar.Value = _okLightness;
                break;

            case ColorShape.OkhlRectangle:
                _bar.Background = Steps(false, 8, step =>
                    ColorValue.FromOkhsl(_okHue, 1 - step, _okLightness, 1).ToColor());
                _bar.Value = _okSaturation;
                break;

            default:
                _bar.Background = Rainbow(horizontal: false);
                _bar.IsDirectionReversed = true;
                _bar.Maximum = 360;
                _bar.Value = _hue;
                break;
        }
    }

    private void ShowHex()
    {
        if (_hex is not null && !_hex.IsFocused)
        {
            _hex.Text = Color.ToText();
        }
    }

    /// <summary>The line a person copies into Godot, which is where this colour is going.</summary>
    private string Literal() => Color.ToExpression();

    /// <summary>Puts today's numbers and ramps into the rows.</summary>
    private void Fill()
    {
        var linear = _base.ToLinear();

        foreach (var channel in Channels)
        {
            var (number, ramp) = (channel.Label, Mode) switch
            {
                ("EV", _) => (Exposure, Token("SurfaceWell", "InkTitle")),

                ("R", ColorValueMode.Linear) => (linear.R, Along(0)),
                ("G", ColorValueMode.Linear) => (linear.G, Along(1)),
                ("B", ColorValueMode.Linear) => (linear.B, Along(2)),
                ("A", ColorValueMode.Linear) => (_base.A, Fade()),

                ("R", _) => (_base.R * 255, Along(0)),
                ("G", _) => (_base.G * 255, Along(1)),
                ("B", _) => (_base.B * 255, Along(2)),

                ("H", ColorValueMode.Okhsl) => (_okHue, Curve(step => ColorValue.FromOkhsl(step * 360, _okSaturation, _okLightness, 1))),
                ("S", ColorValueMode.Okhsl) => (_okSaturation * 100, Curve(step => ColorValue.FromOkhsl(_okHue, step, _okLightness, 1))),
                ("L", ColorValueMode.Okhsl) => (_okLightness * 100, Curve(step => ColorValue.FromOkhsl(_okHue, _okSaturation, step, 1))),

                ("H", _) => (_hue, Rainbow(horizontal: true)),
                ("S", _) => (_saturation * 100, Line(ColorValue.FromHsv(_hue, 0, _value, 1), ColorValue.FromHsv(_hue, 1, _value, 1))),
                ("V", _) => (_value * 100, Line(ColorValue.FromHsv(_hue, _saturation, 0, 1), ColorValue.FromHsv(_hue, _saturation, 1, 1))),

                _ => (_base.A * 255, Fade()),
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
        if (_shape is null || _fieldHandle is null)
        {
            return;
        }

        var middle = new Point(_shape.Bounds.Width / 2, _shape.Bounds.Height / 2);
        var radius = Math.Min(middle.X, middle.Y);

        switch (Shape)
        {
            case ColorShape.Wheel:
                // The square is inscribed in the ring, and the ring's own handle rides the
                // middle of the band.
                var side = radius * RingInner * Root;
                var band = radius * (RingInner + (1 - RingInner) / 2);

                Stand(
                    _fieldHandle,
                    middle.X - side + _saturation * side * 2,
                    middle.Y - side + (1 - _value) * side * 2);

                if (_ringHandle is not null)
                {
                    Stand(_ringHandle, middle.X + Cos(_hue) * band, middle.Y + Sin(_hue) * band);
                }

                break;

            case ColorShape.Circle:
                Stand(
                    _fieldHandle,
                    middle.X + Cos(_hue) * radius * _saturation,
                    middle.Y + Sin(_hue) * radius * _saturation);
                break;

            case ColorShape.OkhslCircle:
                Stand(
                    _fieldHandle,
                    middle.X + Cos(_okHue) * radius * _okSaturation,
                    middle.Y + Sin(_okHue) * radius * _okSaturation);
                break;

            case ColorShape.OkhsRectangle:
                Stand(
                    _fieldHandle,
                    _okHue / 360 * _shape.Bounds.Width,
                    (1 - _okSaturation) * _shape.Bounds.Height);
                break;

            case ColorShape.OkhlRectangle:
                Stand(
                    _fieldHandle,
                    _okHue / 360 * _shape.Bounds.Width,
                    (1 - _okLightness) * _shape.Bounds.Height);
                break;

            default:
                Stand(_fieldHandle, _saturation * _shape.Bounds.Width, (1 - _value) * _shape.Bounds.Height);
                break;
        }

        // The square inside the ring is laid out here as well, since it is the one shape
        // where the field is not the whole area.
        if (_field is not null)
        {
            var inscribed = Shape == ColorShape.Wheel;

            _field.Width = inscribed ? radius * RingInner * Root * 2 : double.NaN;
            _field.Height = _field.Width;
            _field.HorizontalAlignment = inscribed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            _field.VerticalAlignment = inscribed ? VerticalAlignment.Center : VerticalAlignment.Stretch;
        }

        // The ring is a stroke rather than a fill, so its thickness is the band and its
        // bounds are pulled in by half of that.
        if (_ring is not null)
        {
            var thickness = radius * (1 - RingInner);

            _ring.StrokeThickness = thickness;
            _ring.Margin = new Thickness(thickness / 2);
        }

        static double Cos(double degrees) => Math.Cos(degrees * Math.PI / 180);

        static double Sin(double degrees) => Math.Sin(degrees * Math.PI / 180);

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

        // A conic brush starts at the top and sweeps clockwise, and hue zero is on the
        // right and rises clockwise, so both the disc and the ring take a quarter turn.
        var turn = Hues();

        if (_discHue is not null)
        {
            _discHue.Fill = turn;
        }

        if (_ring is not null)
        {
            _ring.Stroke = turn;
        }

        if (_discBloom is not null)
        {
            _discBloom.Fill = new RadialGradientBrush
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

        if (_discShade is not null)
        {
            _discShade.Fill = Brushes.Black;
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

    /// <summary>The hue turn, for the disc and for the ring around the square.</summary>
    private static ConicGradientBrush Hues()
    {
        var turn = new ConicGradientBrush { Center = RelativePoint.Center, Angle = 90 };

        // The first stop again at the end, so the sweep meets itself.
        for (var step = 0; step <= 12; step++)
        {
            var offset = (double)step / 12;

            turn.GradientStops.Add(new GradientStop(ColorValue.FromHsv(offset * 360, 1, 1, 1).ToColor(), offset));
        }

        return turn;
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

    /// <summary>
    /// One colour channel swept from nothing to all of it, with the others held. In the
    /// linear mode the sweep is in light, which is where that mode's numbers are even.
    /// </summary>
    private LinearGradientBrush Along(int channel)
    {
        if (Mode == ColorValueMode.Linear)
        {
            var linear = _base.ToLinear();

            return Curve(step => new ColorValue(
                channel == 0 ? step : linear.R,
                channel == 1 ? step : linear.G,
                channel == 2 ? step : linear.B,
                1).ToSrgb());
        }

        return Line(
            new ColorValue(channel == 0 ? 0 : _base.R, channel == 1 ? 0 : _base.G, channel == 2 ? 0 : _base.B, 1),
            new ColorValue(channel == 0 ? 1 : _base.R, channel == 1 ? 1 : _base.G, channel == 2 ? 1 : _base.B, 1));
    }

    private LinearGradientBrush Fade()
    {
        var opaque = new ColorValue(_base.R, _base.G, _base.B, 1).ToColor();

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
