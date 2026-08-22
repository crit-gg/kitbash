using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// Taking a kitbash link apart. Everything here comes off a web page, so the refusals
/// matter as much as the parses.
/// </summary>
public sealed class DeepLinkTests
{
    [Fact]
    public void TheHostIsTheVerb()
    {
        Assert.True(DeepLink.TryParse("kitbash://clone?repo=https://example.com/team/game.git", out var link));

        Assert.Equal("clone", link.Verb);
        Assert.Equal("https://example.com/team/game.git", link.Value("repo"));
    }

    /// <summary>Both shapes reach the same place, since a browser may hand over either.</summary>
    [Fact]
    public void AVerbInThePathReadsTheSame()
    {
        Assert.True(DeepLink.TryParse("kitbash:clone?repo=https://example.com/team/game.git", out var link));

        Assert.Equal("clone", link.Verb);
        Assert.Equal("https://example.com/team/game.git", link.Value("repo"));
    }

    [Fact]
    public void ThePathAfterTheVerbIsTheSegments()
    {
        Assert.True(DeepLink.TryParse("kitbash://tools/repository?add=https%3A%2F%2Fexample.com%2Ft", out var link));

        Assert.Equal("tools", link.Verb);
        Assert.Equal("repository", link.First);
        Assert.Equal("https://example.com/t", link.Value("add"));
    }

    /// <summary>A page id is camel case and a version has dots, so neither may be folded.</summary>
    [Fact]
    public void ASegmentKeepsItsCase()
    {
        Assert.True(DeepLink.TryParse("kitbash://settings/toolRepositories", out var link));

        Assert.Equal("toolRepositories", link.First);
    }

    [Fact]
    public void TheVerbIsFoldedSoAnyCaseWorks()
    {
        Assert.True(DeepLink.TryParse("KITBASH://Engine/4.7.1", out var link));

        Assert.Equal("engine", link.Verb);
        Assert.Equal("4.7.1", link.First);
    }

    [Fact]
    public void AQueryNameIsFoundWhateverItsCase()
    {
        Assert.True(DeepLink.TryParse("kitbash://clone?REPO=x", out var link));

        Assert.Equal("x", link.Value("repo"));
    }

    /// <summary>A second one is a mistake or somebody talking past a check.</summary>
    [Fact]
    public void ARepeatedNameKeepsTheFirst()
    {
        Assert.True(DeepLink.TryParse("kitbash://clone?repo=first&repo=second", out var link));

        Assert.Equal("first", link.Value("repo"));
    }

    [Fact]
    public void NoSegmentsIsAnEmptyList()
    {
        Assert.True(DeepLink.TryParse("kitbash://clone", out var link));

        Assert.Empty(link.Segments);
        Assert.Null(link.First);
        Assert.Null(link.Value("repo"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("https://example.com")]
    [InlineData("kitbashy://clone")]
    [InlineData("kitbash://")]
    [InlineData("kitbash:")]
    [InlineData("clone")]
    [InlineData("--upload-pack=touch /tmp/x")]
    public void AnythingThatIsNotALinkIsRefused(string? text)
    {
        Assert.False(DeepLink.TryParse(text, out var link));
        Assert.Null(link);
    }

    /// <summary>The desktop chose the length, so this is where it stops.</summary>
    [Fact]
    public void SomethingEnormousIsRefused()
    {
        var long_ = "kitbash://clone?repo=" + new string('a', 4096);

        Assert.False(DeepLink.TryParse(long_, out _));
    }
}
