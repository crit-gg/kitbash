using Kitbash.Core.Godot;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Godot;

/// <summary>How a repository build is named, pinned and addressed. Nothing here touches a disk.</summary>
public class EngineIdentityTests
{
    [Fact]
    public void AnOfficialNameReadsAsItAlwaysHas()
    {
        var id = EngineId.Parse("4.7.1-stable-mono");

        Assert.True(id.IsOfficial);
        Assert.True(id.IsMono);
        Assert.Equal("4.7.1-stable-mono", id.ToString());
    }

    [Fact]
    public void ARepositoryNameRoundTrips()
    {
        var id = EngineId.Parse("github/crit-gg/godot-slopworks/4.7.2+18d5d19-mono");

        Assert.False(id.IsOfficial);
        Assert.Equal("github/crit-gg/godot-slopworks", id.Repository!.ToString());
        Assert.Equal("18d5d19", id.Build);
        Assert.Equal(EngineChannel.Custom, id.Tag.Channel);
        Assert.Equal("github/crit-gg/godot-slopworks/4.7.2+18d5d19-mono", id.ToString());
    }

    [Theory]
    [InlineData("4.7.2-custom")]
    [InlineData("github/crit-gg/godot-slopworks/4.7.2")]
    [InlineData("github/crit-gg/4.7.2+18d5d19")]
    [InlineData("gitlab/crit-gg/godot-slopworks/4.7.2+18d5d19")]
    public void NamesThatAreNeitherAreRefused(string text) =>
        Assert.False(EngineId.TryParse(text, out _));

    [Fact]
    public void AZeroPatchInARepositoryTagReadsAsGodotWritesIt()
    {
        Assert.True(EngineTag.TryParseNumber("4.7.0", out var tag));

        Assert.Equal(0, tag.Patch);
        Assert.Equal("4.7-custom", tag.ToString());
    }

    [Theory]
    [InlineData("https://github.com/Crit-GG/Godot-Slopworks", "github/crit-gg/godot-slopworks")]
    [InlineData("https://github.com/crit-gg/godot-slopworks.git", "github/crit-gg/godot-slopworks")]
    [InlineData("https://www.github.com/crit-gg/godot-slopworks/", "github/crit-gg/godot-slopworks")]
    public void AnAddressIsFoldedToOneSpelling(string url, string expected) =>
        Assert.Equal(expected, EngineRepositoryAddress.ForGitHub(WebAddress.Parse(url))!.ToString());

    [Theory]
    [InlineData("https://gitlab.com/crit-gg/godot-slopworks")]
    [InlineData("https://github.com/crit-gg")]
    [InlineData("https://github.com/crit-gg/godot-slopworks/releases")]
    public void AnythingButARepositoryOnGitHubHasNoAddress(string url) =>
        Assert.Null(EngineRepositoryAddress.ForGitHub(WebAddress.Parse(url)));

    [Fact]
    public void ANewestPinHasNoBuild()
    {
        Assert.True(EngineVersionPattern.TryParseForRepository("4.7.2-mono", out var pin));

        Assert.False(pin.IsExact);
        Assert.True(pin.NeedsDotnet);
        Assert.Equal("4.7.2-mono", pin.ToString());
    }

    [Fact]
    public void AnExactPinNamesOneBuild()
    {
        Assert.True(EngineVersionPattern.TryParseForRepository("4.7.2+18d5d19", out var pin));

        Assert.True(pin.IsExact);
        Assert.Equal("18d5d19", pin.Build);
        Assert.True(pin.Matches(EngineId.Parse("github/a/b/4.7.2+18d5d19")));
        Assert.False(pin.Matches(EngineId.Parse("github/a/b/4.7.2+abc1234")));
    }

    [Theory]
    [InlineData("4.7.2-stable")]
    [InlineData("4+18d5d19")]
    [InlineData("4.7.2+18d-5d19")]
    public void ARepositoryPinRefusesWhatARepositoryCannotPublish(string text) =>
        Assert.False(EngineVersionPattern.TryParseForRepository(text, out _));

    [Fact]
    public void AnOfficialPinRefusesABuild() =>
        Assert.False(EngineVersionPattern.TryParse("4.7.2+18d5d19", out _));

    [Fact]
    public void AnOfficialPinStillRefusesMonoWithoutAChannel() =>
        Assert.False(EngineVersionPattern.TryParse("4.7-mono", out _));
}
