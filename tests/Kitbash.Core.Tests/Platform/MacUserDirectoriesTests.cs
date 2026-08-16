using Kitbash.Core.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// Where one person's Kitbash keeps things on a Mac. Only the macOS branch registers
/// these, so every case here is asked on a Mac and skipped everywhere else.
/// </summary>
public sealed class MacUserDirectoriesTests
{
    public static bool OnMac => OperatingSystem.IsMacOS();

    private const string MacOnly = "These are the macOS directories.";

    private const string Home = "/Users/someone";

    private static IUserDirectories Directories(ServiceProvider provider) =>
        provider.GetRequiredService<IUserDirectories>();

    private static ServiceProvider Provider(string home = Home, string? temporary = null)
    {
        var services = new ServiceCollection();

        // TryAdd means the first registration wins, so this replaces the real environment.
        services.AddSingleton<IEnvironment>(new FakeEnvironment(home, temporary));
        services.AddKitbashIO();

        return services.BuildServiceProvider();
    }

    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void EverythingSitsUnderLibrary()
    {
        using var provider = Provider();
        var directories = Directories(provider);

        Assert.Equal($"{Home}/Library/Application Support/Kitbash/Config", directories.ConfigurationFor("Kitbash"));
        Assert.Equal($"{Home}/Library/Application Support/Kitbash/State", directories.StateFor("Kitbash"));
        Assert.Equal($"{Home}/Library/Caches/Kitbash", directories.CacheFor("Kitbash"));
    }

    /// <summary>
    /// Both files are called kitbash.toml, so settings and state sharing a directory would
    /// write one over the other.
    /// </summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void ConfigurationAndStateAreTwoPlaces()
    {
        using var provider = Provider();
        var directories = Directories(provider);

        Assert.NotEqual(directories.ConfigurationFor("Kitbash"), directories.StateFor("Kitbash"));
    }

    /// <summary>macOS has no runtime directory, so the lock goes where Linux puts it without one.</summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void TheRuntimeDirectoryIsTheCache()
    {
        using var provider = Provider();
        var directories = Directories(provider);

        Assert.Equal(directories.CacheFor("Kitbash"), directories.RuntimeFor("Kitbash"));
    }

    /// <summary>Every folder under Application Support carries its application's own spelling.</summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void TheNameKeepsItsCase()
    {
        using var provider = Provider();

        Assert.Contains("Kitbash", Directories(provider).StateFor("Kitbash"), StringComparison.Ordinal);
    }

    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void NoHomeDirectoryStillLandsSomewhereReal()
    {
        using var provider = Provider(home: string.Empty, temporary: "/var/folders/ab/T");

        Assert.StartsWith("/var/folders/ab/T/", Directories(provider).StateFor("Kitbash"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A relative value is no better than none, so the last resort is a directory macOS
    /// always has rather than a path resolved against wherever the app was started.
    /// </summary>
    [Fact(Skip = MacOnly, SkipUnless = nameof(OnMac))]
    public void NoHomeAndNoTemporaryFallsBackToTmp()
    {
        using var provider = Provider(home: string.Empty, temporary: "relative/path");

        Assert.StartsWith("/tmp/", Directories(provider).StateFor("Kitbash"), StringComparison.Ordinal);
    }

    private sealed class FakeEnvironment(string home, string? temporary) : IEnvironment
    {
        public string? GetVariable(string name) => name == "TMPDIR" ? temporary : null;

        public string GetHomeDirectory() => home;

        public IReadOnlyList<string> GetProcessCommand() => [];
    }
}
