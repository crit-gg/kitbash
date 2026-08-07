using Kitbash.Core.IO;
using Kitbash.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// The correction a child is given when this copy is running from an AppImage. Windows
/// has no bundle and registers the empty overlay, so none of this is asked there.
/// </summary>
public sealed class BundleEnvironmentTests
{
    public static bool OnLinux => OperatingSystem.IsLinux();

    private const string LinuxOnly = "There is no AppImage on Windows.";

    private const string Mount = "/tmp/.mount_kitbash";

    private static ServiceProvider Provider(Dictionary<string, string> variables)
    {
        var services = new ServiceCollection();

        // TryAdd means the first registration wins, so this replaces the real environment
        // and the overlay is built from the variables named here.
        services.AddSingleton<IEnvironment>(new FakeEnvironment(variables));
        services.AddKitbashPlatform();

        return services.BuildServiceProvider();
    }

    private static IReadOnlyDictionary<string, string> Outside(Dictionary<string, string> variables)
    {
        using var provider = Provider(variables);

        return provider.GetRequiredService<IBundleEnvironment>().Outside;
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void NoMountPointCorrectsNothing()
    {
        Assert.Empty(Outside(new() { ["PATH"] = $"{Mount}/usr/bin:/usr/bin" }));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void TheMountPointComesOffPath()
    {
        var outside = Outside(new()
        {
            ["APPDIR"] = Mount,
            ["PATH"] = $"{Mount}/usr/bin:/usr/bin:/bin",
        });

        Assert.Equal("/usr/bin:/bin", outside["PATH"]);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void AVariableHoldingNothingOfOursIsLeftAlone()
    {
        var outside = Outside(new()
        {
            ["APPDIR"] = Mount,
            ["PATH"] = "/usr/bin:/bin",
        });

        Assert.False(outside.ContainsKey("PATH"));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void AVariableHoldingOnlyOursIsUnset()
    {
        var outside = Outside(new()
        {
            ["APPDIR"] = Mount,
            ["LD_LIBRARY_PATH"] = $"{Mount}/usr/lib",
        });

        Assert.Equal(string.Empty, outside["LD_LIBRARY_PATH"]);
    }

    /// <summary>
    /// Plasma's task manager reads APPDIR out of a window's own process and draws it as
    /// whatever desktop file that directory holds, which is ours.
    /// </summary>
    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void TheMountPointItselfIsUnset()
    {
        Assert.Equal(string.Empty, Outside(new() { ["APPDIR"] = Mount })["APPDIR"]);
    }

    /// <summary>The AppImage runtime sets these and nothing reads them to name an app.</summary>
    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void ThePathOfTheAppImageIsLeftForVelopack()
    {
        var outside = Outside(new()
        {
            ["APPDIR"] = Mount,
            ["APPIMAGE"] = "/home/someone/kitbash.appimage",
            ["OWD"] = "/home/someone",
        });

        Assert.False(outside.ContainsKey("APPIMAGE"));
        Assert.False(outside.ContainsKey("OWD"));
    }

    /// <summary>
    /// The other half of the empty value. A child has to see no variable at all, and in
    /// sh an unset name is the only one that takes the word after the dash.
    /// </summary>
    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public async Task AnUnsetVariableIsAbsentFromAChild()
    {
        var held = Environment.GetEnvironmentVariable("APPDIR");
        Environment.SetEnvironmentVariable("APPDIR", Mount);

        try
        {
            using var provider = Provider(new() { ["APPDIR"] = Mount });

            var output = await provider.GetRequiredService<IProcessRunner>().ReadAsync(
                ProcessRequest.Command("/bin/sh", "-c", "echo ${APPDIR-absent}"),
                TestContext.Current.CancellationToken);

            Assert.Equal("absent", output.StandardOutput.Trim());
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPDIR", held);
        }
    }

    private sealed class FakeEnvironment(Dictionary<string, string> variables) : IEnvironment
    {
        public string? GetVariable(string name) =>
            variables.TryGetValue(name, out var value) ? value : null;

        public string GetHomeDirectory() => "/home/someone";

        public IReadOnlyList<string> GetProcessCommand() => [];
    }
}
