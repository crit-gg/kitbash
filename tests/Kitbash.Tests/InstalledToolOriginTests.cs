using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Kitbash.Core.Settings;
using Kitbash.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// The repository an installed version came from, written to and read back from the tool's
/// own state file. A real directory, since the folder layout is half of what is being read.
/// </summary>
public sealed class InstalledToolOriginTests : IDisposable
{
    private const string Foundry = "https://github.com/owner/foundry";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-tool-origin-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;

    public InstalledToolOriginTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashIO()
            .AddKitbashPlatform()
            .AddKitbashApplicationStorage()
            .AddSingleton<ToolLog>()
            .AddSingleton<IToolManifestReader, ToolManifestReader>()
            .AddSingleton<IToolRuntime, ToolRuntime>()
            .AddSingleton<IToolFolderReader, ToolFolderReader>()
            .AddSingleton<IInstalledTools, InstalledTools>()
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>A workspace list offered it, so the flag says the global list did not.</summary>
    [Fact]
    public void AWorkspaceInstallRecordsItsRepository()
    {
        Install("github.foundry");

        Tools.SetOrigin(Id("github.foundry"), Source("the Art workspace", "Art"));

        var origin = Assert.Single(Tools.Read()).Origin;

        Assert.NotNull(origin);
        Assert.Equal(Foundry, origin.Url.ToString());
        Assert.False(origin.Global);
    }

    /// <summary>The global list offered it, which is the bit no later read could work out.</summary>
    [Fact]
    public void AGlobalInstallRecordsThatItWasGlobal()
    {
        Install("github.foundry");

        Tools.SetOrigin(Id("github.foundry"), Source("the global config"));

        Assert.True(Assert.Single(Tools.Read()).Origin?.Global);
    }

    /// <summary>
    /// A tool installed before any of this was written records nothing, and that reads as
    /// nothing rather than as a guess.
    /// </summary>
    [Fact]
    public void AToolThatRecordedNothingHasNoOrigin()
    {
        Install("github.foundry");

        Assert.Null(Assert.Single(Tools.Read()).Origin);
    }

    /// <summary>
    /// An update installs from whichever list offers the version that is here now, so the
    /// second write is what the tool reads back as.
    /// </summary>
    [Fact]
    public void TheLastInstallIsWhatIsRecorded()
    {
        Install("github.foundry");

        var id = Id("github.foundry");

        Tools.SetOrigin(id, Source("the Art workspace", "Art"));
        Tools.SetOrigin(id, Source("the global config"));

        Assert.True(Assert.Single(Tools.Read()).Origin?.Global);
    }

    /// <summary>
    /// A hand edited state file naming something that is not an address reads as nothing,
    /// the way a bad row in a repository list is skipped rather than failing the read.
    /// </summary>
    [Fact]
    public void AnAddressThatWillNotParseReadsAsNothing()
    {
        Install("github.foundry");

        var paths = _services.GetRequiredService<ApplicationPaths>();

        File.WriteAllText(
            paths.StateFileFor(SettingsScope.ForTool("github.foundry")),
            $"[install]{Environment.NewLine}repository = \"not an address\"{Environment.NewLine}");

        Assert.Null(Assert.Single(Tools.Read()).Origin);
    }

    private IInstalledTools Tools => _services.GetRequiredService<IInstalledTools>();

    private static ToolId Id(string value)
    {
        Assert.True(ToolId.TryParse(value, out var id));

        return id;
    }

    private static ToolRepositorySource Source(string origin, string? workspace = null) =>
        new(ToolRepositorySource.GitHub, WebAddress.Parse(Foundry), origin, workspace);

    /// <summary>One version folder holding the manifest an install would have written.</summary>
    private void Install(string id)
    {
        var paths = _services.GetRequiredService<ApplicationPaths>();
        var version = Path.Combine(paths.ToolDirectoryFor(id), "1.0.0");

        Directory.CreateDirectory(version);

        File.WriteAllText(
            Path.Combine(version, ToolManifestReader.FileName),
            """
            {
              "manifest": 1,
              "id": "foundry",
              "name": "Foundry",
              "summary": "A tool",
              "category": "Tools",
              "version": "1.0.0",
              "payloads": [{ "runtime": "any", "executable": "run" }]
            }
            """);
    }

    private sealed class FakeUserDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "runtime");
    }
}
