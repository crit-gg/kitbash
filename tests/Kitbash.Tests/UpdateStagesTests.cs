using Kitbash.Updates;

namespace Kitbash.Tests;

/// <summary>
/// What the splash says while an update is fetched. The wording is here rather than in the
/// window, so the two rules it carries can be read without drawing anything.
/// </summary>
public class UpdateStagesTests
{
    private static UpdateStages Stages(long size = 12_900_000) =>
        new(new AvailableUpdate("0.6.1", size));

    [Fact]
    public void ItNamesTheVersionAndTheSizeTheFeedStated()
    {
        var stage = Stages().Downloading(45);

        Assert.Equal("Downloading Kitbash 0.6.1", stage.Status);
        Assert.Equal("45% of 12.3 MB", stage.Detail);
        Assert.Equal(0.45, stage.Fraction!.Value, 3);
    }

    [Fact]
    public void NothingFetchedYetIsStillTheDownload()
    {
        var stage = Stages().Downloading(0);

        Assert.Equal("Downloading Kitbash 0.6.1", stage.Status);
        Assert.Equal(0, stage.Fraction);
    }

    [Fact]
    public void EverythingAfterAHundredIsTheChecksumRatherThanTheFetch()
    {
        var stages = Stages();

        Assert.Equal("Downloading Kitbash 0.6.1", stages.Downloading(99).Status);

        var full = stages.Downloading(100);

        Assert.Equal("Checking the download", full.Status);
        Assert.Equal(1, full.Fraction);
        Assert.Equal("100% of 12.3 MB", full.Detail);
    }

    [Fact]
    public void APercentOutsideItsRangeIsPulledBackIntoIt()
    {
        var stages = Stages();

        Assert.Equal(0, stages.Downloading(-5).Fraction);
        Assert.Equal(1, stages.Downloading(400).Fraction);
    }

    [Fact]
    public void RestartingGoesBackToSayingOnlyThatWorkIsHappening()
    {
        var stage = Stages().Restarting();

        Assert.Equal("Restarting Kitbash", stage.Status);
        Assert.Null(stage.Fraction);
        Assert.Equal(string.Empty, stage.Detail);
    }
}
