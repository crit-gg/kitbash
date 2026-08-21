using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Workspaces;

namespace Kitbash.Core.Tests.Settings;

/// <summary>
/// What a settings file that will not parse does to a start. It used to throw at whoever
/// asked for a setting, which the launcher reaches only once the splash is already up.
/// </summary>
public class CorruptSettingsTests : IDisposable
{
    /// <summary>
    /// The shape a crash leaves on NTFS: the right length, no content. A rename is
    /// journalled and the bytes it moves are not.
    /// </summary>
    private const string Zeroed = "\0\0\0\0\0\0\0\0";

    private const string Nonsense = "[workspaces\nknown = ";

    private readonly string _home =
        Path.Combine(Path.GetTempPath(), "kitbash-corrupt-" + Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Theory]
    [InlineData(Zeroed)]
    [InlineData(Nonsense)]
    public void ASettingsFileThatWillNotParseReadsAsDefaults(string content)
    {
        var provider = Services();
        var path = Settings(provider);
        var windows = provider.GetRequiredService<IWindowSettings>();

        Write(path, "window.nativeChrome = true\n");

        Assert.True(windows.UseNativeChrome);

        Write(path, content);
        provider.GetRequiredService<IApplicationSettings>().Reload();

        // Chrome is the read the launcher makes on its way to opening the window, and the
        // splash is already up by then, so this throwing is what left one up for ever.
        Assert.False(windows.UseNativeChrome);
        Assert.Equal(provider.GetRequiredService<WindowChromeRule>().Off, windows.Chrome);
    }

    [Fact]
    public void AStateFileThatWillNotParseStillLetsTheWorkspaceListBeBuilt()
    {
        var provider = Services();

        Write(State(provider), Zeroed);

        // The registry reads the state file in its own constructor, so resolving it at all
        // is the thing that used to throw.
        Assert.Empty(provider.GetRequiredService<IWorkspaceRegistry>().All);
    }

    [Fact]
    public void ReplaceMovesABrokenFileAsideAndLeavesNoneInItsPlace()
    {
        var provider = Services();
        var path = Settings(provider);

        Write(path, Zeroed);

        var broken = provider.GetRequiredService<ISettingsRepair>().Replace(path);

        Assert.Equal(path + ".broken", broken);
        Assert.False(File.Exists(path));
        Assert.Equal(Zeroed, File.ReadAllText(broken!));
    }

    [Fact]
    public void ReplaceLeavesAFileThatReadsAlone()
    {
        var provider = Services();
        var path = Settings(provider);

        Write(path, "window.nativeChrome = true\n");

        Assert.Null(provider.GetRequiredService<ISettingsRepair>().Replace(path));
        Assert.True(File.Exists(path));
        Assert.True(provider.GetRequiredService<IWindowSettings>().UseNativeChrome);
    }

    [Fact]
    public void ReplaceHasNothingToDoForAFileThatIsNotThere()
    {
        var provider = Services();

        Assert.Null(provider.GetRequiredService<ISettingsRepair>().Replace(Settings(provider)));
    }

    [Fact]
    public void AWriteAfterARepairMakesTheFileAgain()
    {
        var provider = Services();
        var path = Settings(provider);

        Write(path, Zeroed);
        provider.GetRequiredService<ISettingsRepair>().Replace(path);

        var settings = provider.GetRequiredService<IApplicationSettings>();

        settings.Set(SettingsScope.Global, "window.nativeChrome", true);
        settings.Reload();

        Assert.True(File.Exists(path));
        Assert.True(provider.GetRequiredService<IWindowSettings>().UseNativeChrome);
    }

    /// <summary>
    /// A write is still refused, since the file is read again first and an edit landing on
    /// one nobody could read would drop whatever it said.
    /// </summary>
    [Fact]
    public void AWriteOntoABrokenFileIsStillRefused()
    {
        var provider = Services();
        var path = Settings(provider);

        Write(path, Nonsense);

        Assert.Throws<SettingsFileUnreadableException>(
            () => provider.GetRequiredService<IApplicationSettings>()
                .Set(SettingsScope.Global, "window.nativeChrome", true));
    }

    private static string Settings(IServiceProvider provider) =>
        provider.GetRequiredService<ApplicationPaths>().SettingsFileFor(SettingsScope.Global);

    private static string State(IServiceProvider provider) =>
        provider.GetRequiredService<ApplicationPaths>().StateFileFor(SettingsScope.Global);

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// A provider built the way the launcher builds one, since Core keeps its
    /// implementations internal. Every user directory lands under one temporary root.
    /// </summary>
    private ServiceProvider Services()
    {
        var services = new ServiceCollection();

        // First, so the TryAdd inside AddKitbashIO leaves it alone.
        services.TryAddSingleton<IUserDirectories>(new TestDirectories(_home));

        return services
            .AddKitbashWorkspaces()
            .BuildServiceProvider();
    }

    private sealed class TestDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "run");
    }
}
