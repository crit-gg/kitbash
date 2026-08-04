using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kitbash.Core.Platform;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A well holding one colour, with a swatch that opens the picker. The edit is committed by
/// Apply in the popover, so dismissing it leaves the field as it was.
/// </summary>
[TemplatePart(SwatchPart, typeof(Border))]
[TemplatePart(TextPart, typeof(TextBlock))]
public class ColorField : Button
{
    /// <inheritdoc cref="ColorPicker.ColorProperty"/>
    public static readonly StyledProperty<ColorValue> ColorProperty =
        AvaloniaProperty.Register<ColorField, ColorValue>(
            nameof(Color), new ColorValue(1, 1, 1, 1), defaultBindingMode: BindingMode.TwoWay);

    /// <inheritdoc cref="ColorPicker.SwatchesProperty"/>
    public static readonly StyledProperty<IList<ColorValue>?> SwatchesProperty =
        AvaloniaProperty.Register<ColorField, IList<ColorValue>?>(nameof(Swatches));

    /// <inheritdoc cref="ColorPicker.RecentProperty"/>
    public static readonly StyledProperty<IList<ColorValue>?> RecentProperty =
        AvaloniaProperty.Register<ColorField, IList<ColorValue>?>(nameof(Recent));

    /// <inheritdoc cref="ColorPicker.ScreenColourProperty"/>
    public static readonly StyledProperty<IScreenColour?> ScreenColourProperty =
        AvaloniaProperty.Register<ColorField, IScreenColour?>(nameof(ScreenColour));

    /// <inheritdoc cref="ColorPicker.HeaderProperty"/>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<ColorField, object?>(nameof(Header), "COLOUR");

    private const string SwatchPart = "PART_Swatch";
    private const string TextPart = "PART_Text";

    /// <summary>How far a disabled swatch is taken down, since it cannot be greyed.</summary>
    private const double Dimmed = 0.5;

    private readonly ColorPicker _picker = new();
    private readonly Flyout _flyout;

    private Border? _swatch;
    private TextBlock? _text;

    public ColorField()
    {
        _flyout = new Flyout
        {
            Content = _picker,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            FlyoutPresenterClasses = { "popover" },
        };

        _flyout.Opening += OnOpening;
        _picker.Applied += OnApplied;
        _picker.Cancelled += OnCancelled;

        Flyout = _flyout;
    }

    /// <inheritdoc cref="ColorProperty"/>
    public ColorValue Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
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

    /// <inheritdoc cref="ScreenColourProperty"/>
    public IScreenColour? ScreenColour
    {
        get => GetValue(ScreenColourProperty);
        set => SetValue(ScreenColourProperty, value);
    }

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _swatch = e.NameScope.Find<Border>(SwatchPart);
        _text = e.NameScope.Find<TextBlock>(TextPart);

        Show();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColorProperty || change.Property == IsEnabledProperty)
        {
            Show();
        }
    }

    private void OnOpening(object? sender, EventArgs e)
    {
        _picker.Header = Header;
        _picker.ScreenColour = ScreenColour;
        _picker.Swatches = Swatches;
        _picker.Recent = Recent;
        _picker.Previous = Color;
        _picker.Color = Color;
    }

    private void OnApplied(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(ColorProperty, _picker.Color);
        _flyout.Hide();
    }

    private void OnCancelled(object? sender, RoutedEventArgs e) => _flyout.Hide();

    private void Show()
    {
        if (_swatch is not null)
        {
            _swatch.Background = new SolidColorBrush(
                (IsEnabled ? Color : Color.Scaled(Dimmed)).ToColor());
        }

        if (_text is not null)
        {
            // Six digits while the colour is opaque. Alpha is spelled out only when there
            // is some, so the field stays as short as it can.
            var hex = Color.ToHex();

            _text.Text = Color.A >= 1 ? hex[..7] : hex;
        }
    }
}
