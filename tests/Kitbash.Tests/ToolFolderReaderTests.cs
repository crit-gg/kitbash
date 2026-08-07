using Kitbash.Core.IO;
using Kitbash.Tools;

namespace Kitbash.Tests;

/// <summary>
/// What a folder on this machine may say it is. A real folder in a temporary directory,
/// since which file is there is the whole of what the reader decides on.
/// </summary>
public sealed class ToolFolderReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kitbash-folder-").FullName;

    private readonly ToolFolderReader _reader =
        new(new ToolManifestReader(new FileSystem()), new ToolRuntime(), new FileSystem());

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void AProjectOnItsOwnIsATool()
    {
        var project = Write("Sprite Import.csproj", string.Empty);

        var tool = _reader.Read(_root);

        Assert.Equal("local.sprite-import", tool.Id.Value);
        Assert.Equal("Sprite Import", tool.Name);
        Assert.Equal("0.0.0-dev", tool.Version.ToString());
        Assert.True(tool.IsLinked);
        Assert.Equal("dotnet", tool.Executable);
        Assert.Equal(["run", "--project", project, "--"], tool.Arguments);
    }

    /// <summary>Nothing guesses which of two projects was meant.</summary>
    [Fact]
    public void TwoProjectsAreRefused()
    {
        Write("One.csproj", string.Empty);
        Write("Two.csproj", string.Empty);

        var refusal = Assert.Throws<ToolManifestException>(() => _reader.Read(_root));

        Assert.Contains("2 projects", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderHoldingNothingIsRefused()
    {
        var refusal = Assert.Throws<ToolManifestException>(() => _reader.Read(_root));

        Assert.Contains(ToolManifestReader.DevelopmentFileName, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADevelopmentFileNamesItsOwnCommand()
    {
        Development(
            """
            {
              "id": "sprites",
              "name": "Sprite import",
              "command": ["dotnet", "run", "--project", "src/Sprites", "--"]
            }
            """);

        var tool = _reader.Read(_root);

        Assert.Equal("local.sprites", tool.Id.Value);
        Assert.Equal("Sprite import", tool.Name);
        Assert.Equal("dotnet", tool.Executable);
        Assert.Equal(["run", "--project", "src/Sprites", "--"], tool.Arguments);
    }

    /// <summary>The loose form is what a folder is read as, whatever it publishes.</summary>
    [Fact]
    public void ADevelopmentFileWinsOverAManifest()
    {
        Write(ToolManifestReader.FileName, Manifest);
        Development("""{ "command": ["dotnet", "run"] }""");

        var tool = _reader.Read(_root);

        Assert.Equal("dotnet", tool.Executable);
        Assert.Equal(ToolManifestReader.DevelopmentVersion, tool.Version);
    }

    /// <summary>The folder names the tool when the file does not.</summary>
    [Fact]
    public void ADevelopmentFileTakesItsNameFromTheFolder()
    {
        Development("""{ "command": ["dotnet"] }""");

        var tool = _reader.Read(_root);

        Assert.Equal(Path.GetFileName(_root), tool.Name);
        Assert.Equal("local." + ToolManifestReader.Slug(Path.GetFileName(_root)), tool.Id.Value);
    }

    [Fact]
    public void ADevelopmentFileMayDeclareAScriptAndItsInputs()
    {
        Development(
            """
            {
              "id": "sprites",
              "kind": "script",
              "command": ["python3", "sprites.py"],
              "inputs": [ { "key": "scale", "type": "number", "argument": "--scale" } ]
            }
            """);

        var tool = _reader.Read(_root);

        Assert.True(tool.Manifest.IsScript);
        Assert.Equal("scale", Assert.Single(tool.Manifest.Inputs).Key);
    }

    /// <summary>
    /// A program a file in the folder is named after is that file, so a script beside the
    /// declaration runs without PATH being involved.
    /// </summary>
    [Fact]
    public void AProgramBesideTheFileIsThatFile()
    {
        var script = Write("run.sh", string.Empty);
        Development("""{ "id": "sprites", "command": ["run.sh"] }""");

        var tool = _reader.Read(_root);

        Assert.Equal(script, tool.Executable);
        Assert.True(tool.Command.IsFile);
    }

    [Fact]
    public void AProgramNothingIsNamedAfterIsLookedUpOnPath()
    {
        Development("""{ "id": "sprites", "command": ["python3"] }""");

        Assert.False(_reader.Read(_root).Command.IsFile);
    }

    [Fact]
    public void ADevelopmentFileNamingNeitherACommandNorAPayloadIsRefused()
    {
        Development("""{ "id": "sprites" }""");

        Assert.Throws<ToolManifestException>(() => _reader.Read(_root));
    }

    [Fact]
    public void ADevelopmentFileMayNamePayloadsInstead()
    {
        Write("run", string.Empty);
        Development("""{ "id": "sprites", "payloads": [ { "runtime": "any", "executable": "run" } ] }""");

        Assert.Equal(Path.Combine(_root, "run"), _reader.Read(_root).Executable);
    }

    /// <summary>A published manifest is still read the strict way when it is the only file.</summary>
    [Fact]
    public void AManifestAloneRunsItsPayload()
    {
        Write(ToolManifestReader.FileName, Manifest);

        var tool = _reader.Read(_root);

        Assert.Equal("local.foundry", tool.Id.Value);
        Assert.Equal(Path.Combine(_root, "run.sh"), tool.Executable);
        Assert.Empty(tool.Arguments);
    }

    /// <summary>The one thing the loose form may say that a published manifest may not.</summary>
    [Fact]
    public void APublishedManifestNamingACommandIsRefused()
    {
        Write(
            ToolManifestReader.FileName,
            """
            {
              "manifest": 2,
              "id": "foundry",
              "name": "Foundry",
              "version": "1.0.0",
              "command": ["dotnet", "run"],
              "payloads": [ { "runtime": "any", "executable": "run.sh" } ]
            }
            """);

        var refusal = Assert.Throws<ToolManifestException>(() => _reader.Read(_root));

        Assert.Contains(ToolManifestReader.DevelopmentFileName, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWordOfTheCommandBeingEmptyIsRefused()
    {
        Development("""{ "id": "sprites", "command": ["dotnet", "  "] }""");

        Assert.Throws<ToolManifestException>(() => _reader.Read(_root));
    }

    private const string Manifest =
        """
        {
          "manifest": 1,
          "id": "foundry",
          "name": "Foundry",
          "version": "1.0.0",
          "payloads": [ { "runtime": "any", "executable": "run.sh" } ]
        }
        """;

    private void Development(string json) => Write(ToolManifestReader.DevelopmentFileName, json);

    private string Write(string name, string contents)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, contents);

        return path;
    }
}
