using Kitbash.Core.Godot;
using Kitbash.Core.Platform;
using Kitbash.Workspaces;

namespace Kitbash.Tests;

/// <summary>Reading and writing <c>kitbash://workspace</c> links.</summary>
public sealed class WorkspaceDeepLinkTests
{
    [Theory]
    [InlineData("kitbash://workspace/Slopworks", null, true)]
    [InlineData("kitbash://workspace/Slopworks/edit", GodotLaunchMode.Editor, true)]
    [InlineData("kitbash://workspace/Slopworks/edit?build=false", GodotLaunchMode.Editor, false)]
    [InlineData("kitbash://workspace/Slopworks/edit?build=FALSE", GodotLaunchMode.Editor, false)]
    [InlineData("kitbash://workspace/Slopworks/edit?build=no", GodotLaunchMode.Editor, true)]
    [InlineData("kitbash://workspace/Slopworks/PLAY", GodotLaunchMode.Play, true)]
    [InlineData("kitbash://workspace/Slopworks/rebuild", GodotLaunchMode.Rebuild, true)]
    [InlineData("kitbash:workspace/Slopworks/play", GodotLaunchMode.Play, true)]
    public void EveryShapeReads(string text, GodotLaunchMode? mode, bool builds)
    {
        var link = Parse(text);

        Assert.NotNull(link);
        Assert.Equal("Slopworks", link.Name);
        Assert.Equal(mode, link.Mode);
        Assert.Equal(builds, link.Builds);
    }

    /// <summary>Skipping the build means something only to the editor.</summary>
    [Fact]
    public void OnlyAnEditSkipsTheBuild()
    {
        Assert.True(Parse("kitbash://workspace/Slopworks/play?build=false")?.Builds);
    }

    [Theory]
    [InlineData("kitbash://workspace")]
    [InlineData("kitbash://workspace/Slopworks/delete")]
    [InlineData("kitbash://workspace/Slopworks/play/now")]
    [InlineData("kitbash://workspace/%20/play")]
    [InlineData("kitbash://engine/Slopworks")]
    public void AnythingElseIsRefused(string text)
    {
        Assert.Null(Parse(text));
    }

    [Theory]
    [InlineData("Slopworks", null, true)]
    [InlineData("Slop works", GodotLaunchMode.Editor, true)]
    [InlineData("team/slop", GodotLaunchMode.Editor, false)]
    [InlineData("3f9c2a1e8b7d6c5f4a3b2c1d0e9f8a7b6c5d4e3f", GodotLaunchMode.Rebuild, true)]
    public void AWrittenLinkReadsBack(string name, GodotLaunchMode? mode, bool builds)
    {
        var link = Parse(WorkspaceDeepLink.Write(name, mode, builds));

        Assert.NotNull(link);
        Assert.Equal(name, link.Name);
        Assert.Equal(mode, link.Mode);
        Assert.Equal(builds, link.Builds);
    }

    [Fact]
    public void TheBuildIsWrittenOnlyWhenSkipped()
    {
        Assert.Equal("kitbash://workspace/Slopworks/edit", WorkspaceDeepLink.Write("Slopworks", GodotLaunchMode.Editor));
        Assert.Equal("kitbash://workspace/Slopworks/play", WorkspaceDeepLink.Write("Slopworks", GodotLaunchMode.Play, builds: false));
        Assert.Equal(
            "kitbash://workspace/Slopworks/edit?build=false",
            WorkspaceDeepLink.Write("Slopworks", GodotLaunchMode.Editor, builds: false));
    }

    private static WorkspaceDeepLink? Parse(string text)
    {
        Assert.True(DeepLink.TryParse(text, out var link));

        return WorkspaceDeepLink.TryParse(link, out var parsed) ? parsed : null;
    }
}
