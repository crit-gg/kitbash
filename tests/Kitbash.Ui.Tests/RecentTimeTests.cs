using System.Globalization;
using Kitbash.Ui.Projects;

namespace Kitbash.Ui.Tests;

/// <summary>
/// How long ago a row was opened. Relative while that still means something, and a date
/// once it does not.
/// </summary>
public class RecentTimeTests
{
    /// <summary>
    /// In the machine's own zone, since what the words say is a local date and a test
    /// pinned to UTC would read differently either side of the line.
    /// </summary>
    private static readonly DateTimeOffset Now =
        new(new DateTime(2026, 8, 6, 14, 30, 0, DateTimeKind.Local));

    [Fact]
    public void TheFirstMinuteIsJustNow()
    {
        Assert.Equal("just now", Ago(TimeSpan.Zero));
        Assert.Equal("just now", Ago(TimeSpan.FromSeconds(59)));
    }

    // A clock that went backwards leaves a stamp in the future, which is not a failure.
    [Fact]
    public void AStampInTheFutureIsJustNowRatherThanNonsense()
    {
        Assert.Equal("just now", Ago(TimeSpan.FromMinutes(-5)));
    }

    [Fact]
    public void MinutesAndHoursCountInWholeUnits()
    {
        Assert.Equal("1m ago", Ago(TimeSpan.FromSeconds(90)));
        Assert.Equal("34m ago", Ago(TimeSpan.FromMinutes(34)));
        Assert.Equal("2h ago", Ago(TimeSpan.FromHours(2)));
    }

    // Calendar days rather than 24 hour blocks, so last night reads as yesterday in the
    // morning rather than as 11h ago.
    [Fact]
    public void TheDayBeforeIsYesterdayHoweverManyHoursThatIs()
    {
        Assert.Equal("yesterday", Ago(TimeSpan.FromHours(15)));
        Assert.Equal("yesterday", Ago(TimeSpan.FromHours(38)));
    }

    [Fact]
    public void ThenDaysThenTheWeek()
    {
        Assert.Equal("2d ago", Ago(TimeSpan.FromDays(2)));
        Assert.Equal("6d ago", Ago(TimeSpan.FromDays(6)));
        Assert.Equal("last week", Ago(TimeSpan.FromDays(7)));
        Assert.Equal("last week", Ago(TimeSpan.FromDays(13)));
    }

    // The culture writes the date, and this repository writes no date format of its own.
    [Fact]
    public void OlderThanAFortnightIsADateInTheCulturesOwnWriting()
    {
        var culture = new CultureInfo("en-US");
        var was = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = culture;

            Assert.Equal("June 14", Ago(TimeSpan.FromDays(53)));

            // Beyond this year the year has to be there, so the short date takes over.
            Assert.Equal("8/6/2025", Ago(TimeSpan.FromDays(365)));
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }

    [Fact]
    public void NoStampAtAllCollapsesTheColumn()
    {
        Assert.Equal(string.Empty, RecentTime.Describe(null, Now));
    }

    private static string Ago(TimeSpan since) => RecentTime.Describe(Now - since, Now);
}
