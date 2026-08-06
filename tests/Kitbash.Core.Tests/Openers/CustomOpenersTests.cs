using Kitbash.Core.IO;
using Kitbash.Core.Platform.Openers;
using Kitbash.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Core.Tests.Openers;

/// <summary>
/// Reads and writes a real config file in a temporary home, so what is checked is the
/// TOML that lands rather than a value held in memory.
/// </summary>
public sealed class CustomOpenersTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("kitbash-custom-").FullName;
    private readonly ServiceProvider _provider;

    public CustomOpenersTests()
    {
        var services = new ServiceCollection();

        // TryAdd means the first registration wins, so this replaces the real environment
        // and every directory lands under the temporary home.
        services.AddSingleton<IEnvironment>(new TempEnvironment(_home));
        services.AddKitbashWorkspaceOpeners();

        _provider = services.BuildServiceProvider();
    }

    private ICustomOpeners Openers => _provider.GetRequiredService<ICustomOpeners>();

    private string ConfigFile =>
        Directory.EnumerateFiles(_home, "*.toml", SearchOption.AllDirectories).Single();

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void NothingWrittenReadsAsNothing()
    {
        Assert.Empty(Openers.Read());
    }

    [Fact]
    public void AToolSurvivesTheRoundTrip()
    {
        Openers.Write([new CustomOpener("Emacs", "/usr/bin/emacs", "--no-splash {workspace}")]);

        var read = Assert.Single(Openers.Read());

        Assert.Equal("Emacs", read.Name);
        Assert.Equal("/usr/bin/emacs", read.Path);
        Assert.Equal("--no-splash {workspace}", read.Arguments);
    }

    [Fact]
    public void ItLandsAsAnArrayOfTables()
    {
        Openers.Write([new CustomOpener("Emacs", "/usr/bin/emacs", string.Empty)]);

        Assert.Contains("[[tools.custom]]", File.ReadAllText(ConfigFile), StringComparison.Ordinal);
    }

    [Fact]
    public void OrderIsKept()
    {
        Openers.Write([
            new CustomOpener("One", "/a", string.Empty),
            new CustomOpener("Two", "/b", string.Empty),
        ]);

        Assert.Equal(["One", "Two"], Openers.Read().Select(opener => opener.Name));
    }

    [Fact]
    public void AnEmptyListTakesEveryBlockOut()
    {
        Openers.Write([new CustomOpener("Emacs", "/usr/bin/emacs", string.Empty)]);
        Openers.Write([]);

        Assert.Empty(Openers.Read());
        Assert.DoesNotContain("[[tools.custom]]", File.ReadAllText(ConfigFile), StringComparison.Ordinal);
    }

    /// <summary>Everything else in the file is left exactly as it was.</summary>
    [Fact]
    public void AWriteLeavesTheRestOfTheFileAlone()
    {
        Openers.Write([new CustomOpener("One", "/a", string.Empty)]);

        var before = File.ReadAllText(ConfigFile);
        File.WriteAllText(ConfigFile, "# a comment somebody wrote\n" + before);
        var withComment = File.ReadAllText(ConfigFile);

        Openers.Write([new CustomOpener("One", "/a", string.Empty), new CustomOpener("Two", "/b", string.Empty)]);

        var after = File.ReadAllText(ConfigFile);

        Assert.Contains("# a comment somebody wrote", after, StringComparison.Ordinal);
        Assert.NotEqual(withComment, after);
        Assert.Equal(2, Openers.Read().Count);
    }

    /// <summary>A row missing either key says nothing usable, so it is passed over.</summary>
    [Fact]
    public void ARowWithNoNameOrNoPathIsSkipped()
    {
        Openers.Write([new CustomOpener("Good", "/a", string.Empty)]);

        File.AppendAllText(
            ConfigFile,
            "\n[[tools.custom]]\nname = \"No path\"\n\n[[tools.custom]]\npath = \"/b\"\n");

        Assert.Equal(["Good"], Openers.Read().Select(opener => opener.Name));
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
