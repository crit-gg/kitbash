using System.Globalization;

namespace Kitbash.Ui.Projects;

/// <summary>
/// How long ago a row was opened, in the words the design writes: relative while that
/// still means something, and a date once it does not.
/// </summary>
public static class RecentTime
{
    /// <summary>
    /// Everything is in whole units, since a row reports roughly when rather than
    /// exactly when. A stamp in the future reads as just now, which is what a clock that
    /// went backwards leaves behind.
    /// </summary>
    /// <param name="when">Null gives an empty string, so the column collapses.</param>
    /// <param name="now">The moment to measure against. Local time, as a person reads it.</param>
    public static string Describe(DateTimeOffset? when, DateTimeOffset now)
    {
        if (when is not { } stamp)
        {
            return string.Empty;
        }

        var local = stamp.ToLocalTime();
        var since = now - local;

        if (since < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (since < TimeSpan.FromHours(1))
        {
            return $"{(int)since.TotalMinutes}m ago";
        }

        // Calendar days rather than 24 hour blocks, so something opened last night reads
        // as yesterday in the morning rather than as 11h ago. Both sides are put in the
        // local zone first, since a date only means anything in one zone.
        var days = (now.ToLocalTime().Date - local.Date).Days;

        if (days <= 0)
        {
            return $"{(int)since.TotalHours}h ago";
        }

        if (days == 1)
        {
            return "yesterday";
        }

        if (days < 7)
        {
            return $"{days}d ago";
        }

        if (days < 14)
        {
            return "last week";
        }

        // The culture writes the date. Within this year the month and the day are enough,
        // and beyond it the year has to be there, so the culture's own two patterns are
        // what the two cases take.
        var culture = CultureInfo.CurrentCulture;

        return local.Year == now.ToLocalTime().Year
            ? local.ToString(culture.DateTimeFormat.MonthDayPattern, culture)
            : local.ToString("d", culture);
    }
}
