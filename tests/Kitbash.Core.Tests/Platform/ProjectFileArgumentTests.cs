using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Platform;

public sealed class ProjectFileArgumentTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "game");

    [Fact]
    public void AFullPathIsTaken()
    {
        var path = Path.Combine(Folder, "project.godot");

        Assert.True(ProjectFileArgument.TryParse(path, out var argument));
        Assert.Equal(path, argument.Path);
    }

    [Fact]
    public void AFileUrlIsTakenAsItsPath()
    {
        var path = Path.Combine(Folder, "my game", "project.godot");

        Assert.True(ProjectFileArgument.TryParse(new Uri(path).AbsoluteUri, out var argument));
        Assert.Equal(path, argument.Path);
    }

    [Theory]
    [InlineData("project.godot")]
    [InlineData("game/project.godot")]
    [InlineData("kitbash://workspace/game")]
    [InlineData("https://example.com/project.godot")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingButAFullPathIsRefused(string? text)
    {
        Assert.False(ProjectFileArgument.TryParse(text, out _));
    }

    [Theory]
    [InlineData("export_presets.cfg")]
    [InlineData("other.godot")]
    [InlineData("project.godot.bak")]
    public void AnotherFileIsRefused(string name)
    {
        Assert.False(ProjectFileArgument.TryParse(Path.Combine(Folder, name), out _));
    }
}
