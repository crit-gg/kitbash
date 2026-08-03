using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace Workbench.Ui.Controls;

/// <summary>
/// What the running culture says about telling the time, for the controls that have to
/// ask rather than assume, and the one column that is a choice rather than a list.
/// </summary>
public class Clock : IValueConverter
{
    /// <summary>Formats a <see cref="TimeSpan"/> the way the culture writes a time.</summary>
    public static readonly IValueConverter Reads = new Clock();

    /// <summary>
    /// This column holds two values, so a wheel turns it over rather than scrolling it.
    /// </summary>
    public static readonly AttachedProperty<bool> TurnsProperty =
        AvaloniaProperty.RegisterAttached<Clock, DateTimePickerPanel, bool>("Turns");

    public static bool GetTurns(DateTimePickerPanel panel) => panel.GetValue(TurnsProperty);

    public static void SetTurns(DateTimePickerPanel panel, bool value) =>
        panel.SetValue(TurnsProperty, value);

    static Clock()
    {
        TurnsProperty.Changed.AddClassHandler<DateTimePickerPanel, bool>(OnTurnsChanged);
    }

    /// <summary>
    /// What <c>TimePicker.ClockIdentifier</c> should be. Avalonia defaults to twelve hours
    /// whatever the culture says, so this is asked for rather than left alone.
    /// </summary>
    public static string Identifier => IsTwelveHour(CultureInfo.CurrentCulture)
        ? "12HourClock"
        : "24HourClock";

    /// <summary>
    /// What the culture calls the first half of the day, for the column that picks it.
    /// Empty on a culture that has no such word, which is every culture that tells the
    /// time in twenty four hours.
    /// </summary>
    public static string Period => CultureInfo.CurrentCulture.DateTimeFormat.AMDesignator;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan time)
        {
            return null;
        }

        // Formatted against the running culture rather than the one the binding was given,
        // which is the invariant one unless a view says otherwise.
        var running = CultureInfo.CurrentCulture;

        return DateTime.Today.Add(time).ToString(running.DateTimeFormat.ShortTimePattern, running);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        AvaloniaProperty.UnsetValue;

    private static void OnTurnsChanged(DateTimePickerPanel panel, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        panel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);

        if (change.GetNewValue<bool>())
        {
            panel.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, handledEventsToo: true);
        }
    }

    // Either way turns it over, since there is only one other value to reach.
    private static void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not DateTimePickerPanel panel)
        {
            return;
        }

        panel.SelectedValue = panel.SelectedValue == panel.MinimumValue
            ? panel.MaximumValue
            : panel.MinimumValue;

        e.Handled = true;
    }

    // A lower case h is the twelve hour hour, an upper case H the twenty four hour one.
    // Read off the pattern rather than off the designator, since a culture can name the
    // halves of the day and still write the time in twenty four hours.
    private static bool IsTwelveHour(CultureInfo culture) =>
        culture.DateTimeFormat.ShortTimePattern.Contains('h', StringComparison.Ordinal);
}
