using Kitbash.Core.IO;
using Kitbash.Tools;

namespace Kitbash.Tests;

/// <summary>
/// What a manifest may say about being a script and about what it asks for, and what it is
/// refused for saying. The reader is the only guard here, since a manifest comes off the
/// internet and names a program the launcher then runs.
/// </summary>
public sealed class ToolManifestScriptTests
{
    private const string Payloads =
        """
        "payloads": [ { "runtime": "any", "executable": "run.sh" } ]
        """;

    private readonly ToolManifestReader _reader = new(new FileSystem());

    [Fact]
    public void AManifestSayingNothingIsAnAppWithNoInputs()
    {
        var manifest = _reader.Read(Manifest(1, string.Empty));

        Assert.Equal(ToolKind.App, manifest.Kind);
        Assert.False(manifest.IsScript);
        Assert.Empty(manifest.Inputs);
    }

    [Fact]
    public void AScriptSaysSoAndItsInputsAreReadInOrder()
    {
        var manifest = _reader.Read(Manifest(
            2,
            """
            "kind": "script",
            "inputs": [
              { "key": "source", "name": "Source folder", "type": "folder",
                "argument": "--source", "required": true, "remember": true,
                "default": "{workspace}/art" },
              { "key": "scale", "type": "number", "argument": "--scale",
                "default": 1.5, "minimum": 0.5, "maximum": 4 },
              { "key": "dry", "type": "boolean", "argument": "--dry-run", "default": true },
              { "key": "mode", "type": "choice", "argument": "--mode=",
                "choices": [ { "value": "fast", "label": "Fast" }, { "value": "careful" } ] },
              { "key": "target" }
            ],
            """));

        Assert.True(manifest.IsScript);
        Assert.Equal(["source", "scale", "dry", "mode", "target"], manifest.Inputs.Select(input => input.Key));

        var source = manifest.Inputs[0];

        Assert.Equal("Source folder", source.Name);
        Assert.Equal(ToolInputKind.Folder, source.Kind);
        Assert.Equal("--source", source.Argument);
        Assert.True(source.Required);
        Assert.True(source.Remember);
        Assert.Equal("{workspace}/art", source.Default);

        // A number keeps the text it was written as, since a command line is text.
        Assert.Equal("1.5", manifest.Inputs[1].Default);
        Assert.Equal(0.5, manifest.Inputs[1].Minimum);
        Assert.Equal(4, manifest.Inputs[1].Maximum);

        Assert.Equal("true", manifest.Inputs[2].Default);

        // An option with no label of its own is labelled by its value.
        Assert.Equal(["Fast", "careful"], manifest.Inputs[3].Choices.Select(choice => choice.Label));

        // Everything a manifest leaves out has an answer.
        var target = manifest.Inputs[4];

        Assert.Equal("target", target.Name);
        Assert.Equal(ToolInputKind.Text, target.Kind);
        Assert.Null(target.Argument);
        Assert.False(target.Required);
        Assert.False(target.Remember);
        Assert.Equal(string.Empty, target.Default);
    }

    /// <summary>
    /// An older launcher reads format 1 and knows nothing of either field, so a manifest
    /// carrying one at format 1 would run there as a window that never opens.
    /// </summary>
    [Theory]
    [InlineData("\"kind\": \"script\",")]
    [InlineData("\"inputs\": [ { \"key\": \"a\" } ],")]
    public void NeitherFieldIsAllowedAtTheOlderFormat(string body)
    {
        var refusal = Assert.Throws<ToolManifestException>(() => _reader.Read(Manifest(1, body)));

        Assert.Contains("format 2", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFormatAboveWhatThisLauncherReadsIsStillRefused()
    {
        var refusal = Assert.Throws<ToolManifestException>(() => _reader.Read(Manifest(3, string.Empty)));

        Assert.Contains("format 3", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"kind\": \"widget\",", "not a kind of tool")]
    [InlineData("\"inputs\": [ { \"key\": \"a\" }, { \"key\": \"a\" } ],", "both called")]
    [InlineData("\"inputs\": [ { \"key\": \"a b\" } ],", "not an input key")]
    [InlineData("\"inputs\": [ { \"key\": \"a\", \"type\": \"colour\" } ],", "not a kind of input")]
    [InlineData("\"inputs\": [ { \"key\": \"a\", \"argument\": \"--one two\" } ],", "holds a space")]
    [InlineData("\"inputs\": [ { \"key\": \"a\", \"type\": \"choice\" } ],", "offers no options")]
    [InlineData("\"inputs\": [ { \"key\": \"a\", \"type\": \"number\", \"minimum\": 4, \"maximum\": 1 } ],", "ends below")]
    [InlineData("\"inputs\": [ { \"key\": \"a\", \"type\": \"number\", \"default\": \"soon\" } ],", "not usable")]
    [InlineData("\"inputs\": [ { \"key\": \"a\", \"type\": \"integer\", \"default\": 1.5 } ],", "whole number")]
    public void AManifestThatCannotBeDrawnIsRefused(string body, string says)
    {
        var refusal = Assert.Throws<ToolManifestException>(() => _reader.Read(Manifest(2, body)));

        Assert.Contains(says, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>The workspace is not known when a manifest is read, so a default naming it stands.</summary>
    [Fact]
    public void ADefaultNamingTheWorkspaceIsNotChecked()
    {
        var manifest = _reader.Read(Manifest(
            2,
            """
            "inputs": [ { "key": "mode", "type": "choice", "default": "{workspace}",
                          "choices": [ { "value": "fast" } ] } ],
            """));

        Assert.Equal("{workspace}", manifest.Inputs[0].Default);
    }

    private static string Manifest(int format, string body) =>
        $$"""
        {
          "manifest": {{format}},
          "id": "sprites",
          "name": "Sprites",
          "version": "1.0.0",
          {{body}}
          {{Payloads}}
        }
        """;
}
