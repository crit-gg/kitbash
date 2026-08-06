using Kitbash.Core.IO;
using Kitbash.Tools;

namespace Kitbash.Tests;

/// <summary>
/// What a manifest may name as its icon. The name reaches a file path, so the reader is
/// what keeps a manifest off the internet from pointing at anything but its own asset.
/// </summary>
public sealed class ToolManifestIconTests
{
    private readonly ToolManifestReader _reader = new(new FileSystem());

    [Fact]
    public void AManifestNamingNoIconHasNone() => Assert.Null(_reader.Read(Manifest(null)).Icon);

    [Theory]
    [InlineData("foundry.png")]
    [InlineData("icon.jpg")]
    [InlineData("icon.jpeg")]
    [InlineData("mark.webp")]
    [InlineData("  spaced.png  ")]
    public void AnImageBesideTheManifestIsTheIcon(string named) =>
        Assert.Equal(named.Trim(), _reader.Read(Manifest(named)).Icon);

    /// <summary>
    /// Both separators, whichever platform is reading, since only one of them separates
    /// here and a manifest is written on the other machine as often as on this one.
    /// </summary>
    [Theory]
    [InlineData("../../secrets.png")]
    [InlineData("..\\..\\secrets.png")]
    [InlineData("art/foundry.png")]
    [InlineData("art\\foundry.png")]
    [InlineData("/etc/passwd.png")]
    [InlineData("C:\\Windows\\thing.png")]
    public void ANameCarryingAPathIsDropped(string named) => Assert.Null(_reader.Read(Manifest(named)).Icon);

    /// <summary>Nothing else is fetched under the name icon, whatever the manifest calls it.</summary>
    [Theory]
    [InlineData("foundry.exe")]
    [InlineData("foundry")]
    [InlineData("foundry.svg")]
    [InlineData("   ")]
    public void ANameThatIsNotAnImageIsDropped(string named) => Assert.Null(_reader.Read(Manifest(named)).Icon);

    /// <summary>The icon is decoration, so a bad one leaves the rest of the manifest standing.</summary>
    [Fact]
    public void AManifestWithABadIconIsStillRead()
    {
        var manifest = _reader.Read(Manifest("../evil.png"));

        Assert.Equal("Sprites", manifest.Name);
        Assert.Null(manifest.Icon);
    }

    private static string Manifest(string? icon) =>
        $$"""
        {
          "manifest": 1,
          "id": "sprites",
          "name": "Sprites",
          "version": "1.0.0",
          {{(icon is null ? string.Empty : $"\"icon\": {System.Text.Json.JsonSerializer.Serialize(icon)},")}}
          "payloads": [ { "runtime": "any", "executable": "run.sh" } ]
        }
        """;
}
