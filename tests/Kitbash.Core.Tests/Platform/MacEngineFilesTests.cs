using Kitbash.Core.Godot;
using Kitbash.Core.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// Godot ships macOS as an application bundle, so the editor is inside a directory rather
/// than beside one, which is the whole reason this differs from the Linux answer.
/// </summary>
public sealed class MacEngineFilesTests : IDisposable
{
    public static bool OnMac => OperatingSystem.IsMacOS();

    private const string MacOnly = "Godot ships a bundle only on macOS.";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "kitbash-engine-" + Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static IEngineFiles Files(ServiceProvider provider) =>
        provider.GetRequiredService<IEngineFiles>();

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddKitbashEngines();

        return services.BuildServiceProvider();
    }

    /// <summary>An executable file, the way an archive leaves the editor.</summary>
    private static string Program(string directory, string name)
    {
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, name);

        File.WriteAllText(path, string.Empty);

        // Through the seam, which already knows the mode means nothing on Windows.
        new FileSystem().MakeExecutableFile(path);

        return path;
    }

    /// <summary>
    /// Godot publishes one macOS build carrying every processor, and a release card offers
    /// only what matches this. Answering the host's processor would hide every macOS build.
    /// </summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void TheArchitectureIsUniversal()
    {
        using var provider = Provider();

        Assert.Equal(EngineArchitecture.Universal, Files(provider).Architecture);
        Assert.Equal(EnginePlatform.MacOS, Files(provider).Platform);
    }

    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void TheEditorIsFoundInsideAnInstalledBundle()
    {
        var expected = Program(Path.Combine(_root, "Godot.app", "Contents", "MacOS"), "Godot");

        using var provider = Provider();

        Assert.Equal(expected, Files(provider).FindEditor(_root));
    }

    /// <summary>The .NET build is Godot_mono.app and names its program the same way.</summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void TheDotnetBundleIsFoundToo()
    {
        var expected = Program(Path.Combine(_root, "Godot_mono.app", "Contents", "MacOS"), "Godot");

        using var provider = Provider();

        Assert.Equal(expected, Files(provider).FindEditor(_root));
    }

    /// <summary>Importing an engine somebody already has means pointing at the bundle itself.</summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void TheDirectoryCanBeTheBundle()
    {
        var bundle = Path.Combine(_root, "Godot.app");
        var expected = Program(Path.Combine(bundle, "Contents", "MacOS"), "Godot");

        using var provider = Provider();

        Assert.Equal(expected, Files(provider).FindEditor(bundle));
    }

    /// <summary>A loose binary somebody put in a folder, which is the shape Linux ships.</summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void ALooseProgramIsStillFound()
    {
        var expected = Program(_root, "Godot");

        using var provider = Provider();

        Assert.Equal(expected, Files(provider).FindEditor(_root));
    }

    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void NothingThatLooksLikeAnEditorIsNull()
    {
        Directory.CreateDirectory(_root);

        using var provider = Provider();

        Assert.Null(Files(provider).FindEditor(_root));
    }

    /// <summary>A bundle with nothing runnable in it is not an editor.</summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void AnEmptyBundleIsNull()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Godot.app", "Contents", "MacOS"));

        using var provider = Provider();

        Assert.Null(Files(provider).FindEditor(_root));
    }
}
