using Kitbash.Core.IO;
using Kitbash.Core.Platform.Openers;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Openers;

/// <summary>
/// Hiding a tool Kitbash found, over a real config file in a temporary home, and the
/// filtering the Open in menu reads through.
/// </summary>
public sealed class HiddenOpenersTests : IDisposable
{
    private static readonly WorkspaceOpener[] Found =
    [
        new("vscode", "Visual Studio Code", "vscode", "/usr/bin/code", WorkspaceOpenerKind.Editor),
        new("jetbrains.RD", "Rider", "jetbrains/RD", "/opt/rider/bin/rider", WorkspaceOpenerKind.Editor),
        new("konsole", "Konsole", "konsole", "/usr/bin/konsole", WorkspaceOpenerKind.Terminal),
    ];

    private readonly string _home = Directory.CreateTempSubdirectory("kitbash-hidden-").FullName;
    private readonly ServiceProvider _provider;

    public HiddenOpenersTests()
    {
        var services = new ServiceCollection();

        // TryAdd means the first registration wins, so both of these replace what
        // AddKitbashWorkspaceOpeners would have used.
        services.AddSingleton<IEnvironment>(new TempEnvironment(_home));
        services.AddSingleton<IWorkspaceOpenerFinder>(new FakeFinder(Found));
        services.AddKitbashWorkspaceOpeners();

        _provider = services.BuildServiceProvider();
    }

    private IHiddenOpeners Hidden => _provider.GetRequiredService<IHiddenOpeners>();

    private IWorkspaceOpeners Openers => _provider.GetRequiredService<IWorkspaceOpeners>();

    private string ConfigFile =>
        Directory.EnumerateFiles(_home, "*.toml", SearchOption.AllDirectories).Single();

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void NothingHiddenReadsAsNothing()
    {
        Assert.Empty(Hidden.Read());
    }

    [Fact]
    public void AnIdSurvivesTheRoundTrip()
    {
        Hidden.Write(["jetbrains.RD"]);

        Assert.Equal(["jetbrains.RD"], Hidden.Read());

        // A plain array under the table the key names, not an array of tables.
        Assert.Contains(
            "hidden = [\"jetbrains.RD\"]",
            File.ReadAllText(ConfigFile),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHiddenToolIsNotOfferedAndIsStillFound()
    {
        Hidden.Write(["konsole"]);

        var offered = await Openers.ReadAsync(TestContext.Current.CancellationToken);
        var all = await Openers.ReadAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["vscode", "jetbrains.RD"], offered.Select(opener => opener.Id));
        Assert.Equal(["vscode", "jetbrains.RD", "konsole"], all.Select(opener => opener.Id));
    }

    /// <summary>
    /// The list is read every call, so the settings window changes the menu without the
    /// launcher restarting and without detection running again.
    /// </summary>
    [Fact]
    public async Task ShowingOneAgainBringsItBack()
    {
        Hidden.Write(["vscode"]);

        Assert.DoesNotContain(
            await Openers.ReadAsync(TestContext.Current.CancellationToken),
            opener => opener.Id == "vscode");

        Hidden.Write([]);

        Assert.Contains(
            await Openers.ReadAsync(TestContext.Current.CancellationToken),
            opener => opener.Id == "vscode");
    }

    /// <summary>An id nothing answers to is kept, so uninstalling a tool forgets nothing.</summary>
    [Fact]
    public async Task AnIdNothingAnswersToIsKeptAndChangesNothing()
    {
        Hidden.Write(["emacs"]);

        var offered = await Openers.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Found.Length, offered.Count);
        Assert.Equal(["emacs"], Hidden.Read());
    }

    [Fact]
    public void TheSameIdTwiceIsWrittenOnce()
    {
        Hidden.Write(["konsole", "konsole", " "]);

        Assert.Equal(["konsole"], Hidden.Read());
    }

    private sealed class FakeFinder(IReadOnlyList<WorkspaceOpener> openers) : IWorkspaceOpenerFinder
    {
        public Task<IReadOnlyList<WorkspaceOpener>> FindAsync(CancellationToken cancellation) =>
            Task.FromResult(openers);
    }

    private sealed class TempEnvironment(string home) : IEnvironment
    {
        public string? GetVariable(string name) => name switch
        {
            "XDG_CONFIG_HOME" => Path.Combine(home, "config"),
            "XDG_DATA_HOME" => Path.Combine(home, "data"),
            "XDG_CACHE_HOME" => Path.Combine(home, "cache"),
            _ => null,
        };

        public string GetHomeDirectory() => home;

        public IReadOnlyList<string> GetProcessCommand() => [];
    }
}
