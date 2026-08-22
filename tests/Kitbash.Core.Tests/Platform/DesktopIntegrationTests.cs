using Microsoft.Extensions.DependencyInjection;
using Kitbash.Core.IO;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Tests.Platform;

/// <summary>
/// The menu entry an AppImage writes for itself, over a real data root in a temporary
/// folder. Windows registers the do nothing implementation, so none of this runs there.
/// </summary>
public sealed class DesktopIntegrationTests : IDisposable
{
    public static bool OnLinux => OperatingSystem.IsLinux();

    private const string LinuxOnly = "There is no AppImage on Windows.";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "kitbash-desktop-" + Guid.NewGuid().ToString("n"));

    private string DataHome => Path.Combine(_root, "data");

    private string ConfigHome => Path.Combine(_root, "config");

    private string Mount => Path.Combine(_root, "mount");

    private string Image => Path.Combine(_root, "kitbash.appimage");

    private string EntryFile => Path.Combine(DataHome, "applications", "kitbash.desktop");

    private string IconFile =>
        Path.Combine(DataHome, "icons", "hicolor", "256x256", "apps", "kitbash.png");

    private string AssociationFile => Path.Combine(ConfigHome, "mimeapps.list");

    private readonly List<ServiceProvider> _providers = [];

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void AFirstInstallWritesAnEntryAManagerCanRead()
    {
        Bundle();
        Integration().Install();

        var entry = File.ReadAllText(EntryFile);

        // The rule an AppImage manager applies is that TryExec names a file that is
        // there, so the check is the file rather than the line.
        Assert.Contains($"TryExec={Image}", entry, StringComparison.Ordinal);
        Assert.True(File.Exists(TryExecOf(entry)));

        Assert.Contains($"Exec=\"{Image}\"", entry, StringComparison.Ordinal);
        Assert.Contains("X-Kitbash-Entry=true", entry, StringComparison.Ordinal);
        Assert.True(File.Exists(IconFile));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void AnEntrySomebodyElseWroteIsLeftAlone()
    {
        Bundle();

        // What Gearlever writes when it installs the AppImage, keys and all.
        var theirs = $"""
            [Desktop Entry]
            Type=Application
            Name=Kitbash (1.2.3)
            Icon={_root}/.icons/kitbash
            TryExec={Image}
            Exec=env DESKTOPINTEGRATION=1 {Image}
            Terminal=false
            X-AppImage-Version=1.2.3
            """;

        Directory.CreateDirectory(Path.GetDirectoryName(EntryFile)!);
        File.WriteAllText(EntryFile, theirs);

        Integration().Install();

        Assert.Equal(theirs, File.ReadAllText(EntryFile));

        // Nothing of ours goes beside it either, so no icon is left behind for an entry
        // that points at its own.
        Assert.False(File.Exists(IconFile));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void AnEntryOfOursIsRewrittenWhenTheImageMoves()
    {
        Bundle();

        Directory.CreateDirectory(Path.GetDirectoryName(EntryFile)!);
        File.WriteAllText(EntryFile, $"""
            [Desktop Entry]
            Type=Application
            Name=Kitbash
            Exec="{_root}/old/kitbash.appimage"
            TryExec={_root}/old/kitbash.appimage
            X-Kitbash-Entry=true
            """);

        Integration().Install();

        Assert.Contains($"TryExec={Image}", File.ReadAllText(EntryFile), StringComparison.Ordinal);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void NoBundleWritesNothing()
    {
        Bundle();
        Integration(null).Install();

        Assert.False(File.Exists(EntryFile));
        Assert.False(File.Exists(IconFile));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void TheEntrySaysItOpensAKitbashLink()
    {
        Bundle();
        Integration().Install();

        var entry = File.ReadAllText(EntryFile);

        // The field code carries the url. Without it the desktop drops the link and
        // Kitbash opens with nothing to show.
        Assert.Contains($"Exec=\"{Image}\" %u", entry, StringComparison.Ordinal);
        Assert.Contains("MimeType=x-scheme-handler/kitbash;", entry, StringComparison.Ordinal);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void TheEntryIsMadeTheDefaultForTheScheme()
    {
        Bundle();
        Integration().Install();

        var list = File.ReadAllText(AssociationFile);

        Assert.Contains("[Default Applications]", list, StringComparison.Ordinal);
        Assert.Contains("x-scheme-handler/kitbash=kitbash.desktop", list, StringComparison.Ordinal);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void EverythingElseInTheListIsLeftAlone()
    {
        Bundle();

        var theirs = """
            [Added Associations]
            text/plain=someone.desktop;

            [Default Applications]
            text/plain=someone.desktop
            x-scheme-handler/https=browser.desktop

            [Removed Associations]
            application/pdf=old.desktop;
            """;

        Directory.CreateDirectory(ConfigHome);
        File.WriteAllText(AssociationFile, theirs);

        Integration().Install();

        var list = File.ReadAllText(AssociationFile);

        Assert.Contains("[Added Associations]", list, StringComparison.Ordinal);
        Assert.Contains("text/plain=someone.desktop;", list, StringComparison.Ordinal);
        Assert.Contains("x-scheme-handler/https=browser.desktop", list, StringComparison.Ordinal);
        Assert.Contains("[Removed Associations]", list, StringComparison.Ordinal);
        Assert.Contains("application/pdf=old.desktop;", list, StringComparison.Ordinal);

        // Under the keys already in the section rather than against the next heading,
        // which parses the same and reads like somebody wrote it.
        Assert.Contains(
            "x-scheme-handler/https=browser.desktop\nx-scheme-handler/kitbash=kitbash.desktop",
            list,
            StringComparison.Ordinal);
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void TheSchemeIsNamedOnceHoweverOftenItRuns()
    {
        Bundle();

        var integration = Integration();

        integration.Install();
        integration.Install();

        var lines = File.ReadAllText(AssociationFile).Split('\n');

        Assert.Single(lines, line =>
            line.StartsWith("x-scheme-handler/kitbash=", StringComparison.Ordinal));
    }

    [Fact(Skip = LinuxOnly, SkipUnless = nameof(OnLinux))]
    public void AnEntrySomebodyElseWroteTakesNoAssociationEither()
    {
        Bundle();

        Directory.CreateDirectory(Path.GetDirectoryName(EntryFile)!);
        File.WriteAllText(EntryFile, "[Desktop Entry]\nName=Kitbash\n");

        Integration().Install();

        Assert.False(File.Exists(AssociationFile));
    }

    /// <summary>The files the AppImage runtime would have mounted and pointed at.</summary>
    private void Bundle()
    {
        Directory.CreateDirectory(Mount);
        File.WriteAllBytes(Path.Combine(Mount, ".DirIcon"), [0x89, 0x50, 0x4E, 0x47]);
        File.WriteAllText(Image, "not really an AppImage");
    }

    private IDesktopIntegration Integration() => Integration(Image);

    private IDesktopIntegration Integration(string? image)
    {
        var variables = new Dictionary<string, string>
        {
            ["XDG_DATA_HOME"] = DataHome,
            ["XDG_CONFIG_HOME"] = ConfigHome,
            ["APPDIR"] = Mount,
        };

        if (image is not null)
        {
            variables["APPIMAGE"] = image;
        }

        var services = new ServiceCollection();

        // TryAdd means the first registration wins, so this is the environment the
        // integration reads and the real filesystem writes under it.
        services.AddSingleton<IEnvironment>(new FakeEnvironment(variables));
        services.AddKitbashPlatform();

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        return provider.GetRequiredService<IDesktopIntegration>();
    }

    private static string TryExecOf(string entry) =>
        entry
            .Split('\n')
            .First(line => line.StartsWith("TryExec=", StringComparison.Ordinal))
            ["TryExec=".Length..]
            .Trim();

    private sealed class FakeEnvironment(Dictionary<string, string> variables) : IEnvironment
    {
        public string? GetVariable(string name) =>
            variables.TryGetValue(name, out var value) ? value : null;

        public string GetHomeDirectory() => "/home/someone";

        public IReadOnlyList<string> GetProcessCommand() => [];
    }
}
