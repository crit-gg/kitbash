using System.Globalization;
using Avalonia.Data.Converters;

namespace Workbench.Ui.Controls;

/// <summary>
/// What the running culture says about telling the time, for the controls that have to
/// ask rather than assume.
/// </summary>
/// <remarks>
/// A time field cannot be written down once. Whether it reads twelve hours or twenty four,
/// where the period sits, and what the period is even called all come from the culture, and
/// a data tool is used in more than one.
/// <para>
/// The readout goes through the culture's own short time pattern rather than a format
/// written here, so the order of the parts is the culture's too. A pattern that puts the
/// period first gets it first.
/// </para>
/// </remarks>
public class Clock : IValueConverter
{
    /// <summary>Formats a <see cref="TimeSpan"/> the way the culture writes a time.</summary>
    public static readonly IValueConverter Reads = new Clock();

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
        Avalonia.AvaloniaProperty.UnsetValue;

    // A lower case h is the twelve hour hour, an upper case H the twenty four hour one.
    // Read off the pattern rather than off the designator, since a culture can name the
    // halves of the day and still write the time in twenty four hours.
    private static bool IsTwelveHour(CultureInfo culture) =>
        culture.DateTimeFormat.ShortTimePattern.Contains('h', StringComparison.Ordinal);
}
