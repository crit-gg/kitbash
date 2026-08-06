using Kitbash.Tools;

namespace Kitbash.Tests;

/// <summary>
/// What a script writes and what the modal makes of it. Both forms are read, and anything
/// that is neither stays a log line rather than being dropped.
/// </summary>
public sealed class ToolProgressReaderTests
{
    private readonly ToolProgressReader _reader = new();

    [Fact]
    public void APrefixedLineSaysWhatIsHappening()
    {
        Assert.Equal("Reading files", _reader.Read("@kitbash stage Reading files").Stage);
        Assert.Equal("12 / 30", _reader.Read("@kitbash detail 12 / 30").Detail);
        Assert.Equal("copied foo.png", _reader.Read("@kitbash log copied foo.png").Log);
    }

    [Fact]
    public void AnErrorLineIsAlogLineThatSaysItIsOne()
    {
        var step = _reader.Read("@kitbash error nothing to read");

        Assert.Equal("nothing to read", step.Log);
        Assert.True(step.IsError);
    }

    [Theory]
    [InlineData("@kitbash progress 40", 40)]
    [InlineData("@kitbash progress 40%", 40)]
    [InlineData("@kitbash: progress 12.5", 12.5)]
    [InlineData("@KITBASH PROGRESS 40", 40)]
    [InlineData("@kitbash progress 900", 100)]
    public void ANumberMovesTheBar(string line, double expected)
    {
        Assert.Equal(expected, _reader.Read(line).Progress);
    }

    /// <summary>Progress with no number is a script saying it no longer knows.</summary>
    [Fact]
    public void ProgressWithNoNumberPutsTheSpinnerBack()
    {
        var step = _reader.Read("@kitbash progress");

        Assert.Null(step.Progress);
        Assert.True(step.IsIndeterminate);
    }

    [Fact]
    public void AJsonLineCarryingTheMarkerIsReadTheSameWay()
    {
        var step = _reader.Read("""{"kitbash":1,"stage":"Reading","detail":"12 / 30","progress":40}""");

        Assert.Equal("Reading", step.Stage);
        Assert.Equal("12 / 30", step.Detail);
        Assert.Equal(40, step.Progress);
    }

    [Fact]
    public void AJsonErrorIsAlogLineThatSaysItIsOne()
    {
        var step = _reader.Read("""{"kitbash":1,"error":"nothing to read"}""");

        Assert.Equal("nothing to read", step.Log);
        Assert.True(step.IsError);
    }

    /// <summary>A script printing its own JSON is not reporting to us.</summary>
    [Fact]
    public void JsonWithoutTheMarkerStaysAlogLine()
    {
        var line = """{"stage":"Reading"}""";
        var step = _reader.Read(line);

        Assert.Null(step.Stage);
        Assert.Equal(line, step.Log);
    }

    [Theory]
    [InlineData("copied foo.png")]
    [InlineData("@kitbashing the tileset")]
    [InlineData("{ not json at all }")]
    public void AnythingElseIsAlogLine(string line)
    {
        var step = _reader.Read(line);

        Assert.Equal(line, step.Log);
        Assert.Null(step.Stage);
        Assert.Null(step.Progress);
    }

    /// <summary>A verb nobody knows keeps the whole line rather than losing it.</summary>
    [Fact]
    public void AnUnknownVerbKeepsTheLine()
    {
        const string Line = "@kitbash sing a song";

        Assert.Equal(Line, _reader.Read(Line).Log);
    }

    [Fact]
    public void ABlankLineSaysNothing()
    {
        Assert.True(_reader.Read("   ").IsEmpty);
        Assert.True(_reader.Read(null).IsEmpty);
    }
}
