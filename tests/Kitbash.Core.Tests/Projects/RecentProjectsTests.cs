using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kitbash.Core.IO;
using Kitbash.Core.Projects;
using Kitbash.Core.Settings;

namespace Kitbash.Core.Tests.Projects;

/// <summary>
/// The list an app keeps of what it has opened, over a real state file in a temporary
/// folder. The store never decides what a project is, so nothing here is about that.
/// </summary>
public class RecentProjectsTests : IDisposable
{
    private readonly string _home =
        Path.Combine(Path.GetTempPath(), "kitbash-recent-" + Guid.NewGuid().ToString("n"));

    private readonly IFileSystem _files = new FileSystem();

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

    [Fact]
    public void RememberingPutsAPathAtTheFrontAndKeepsItThroughAReread()
    {
        var store = Store();

        store.Remember(Folder("one"), "One");
        store.Remember(Folder("two"), "Two");

        Assert.Equal(["Two", "One"], store.All.Select(entry => entry.Name));

        // A second app over the same file reads the same list, which is the only thing
        // that makes the list worth keeping.
        Assert.Equal(["Two", "One"], Store().All.Select(entry => entry.Name));
    }

    [Fact]
    public void RememberingOneAlreadyOnTheListMovesItRatherThanAddingItTwice()
    {
        var store = Store();

        store.Remember(Folder("one"), "One");
        store.Remember(Folder("two"), "Two");
        store.Remember(Folder("one"), "One again");

        Assert.Equal(["One again", "Two"], store.All.Select(entry => entry.Name));
    }

    [Fact]
    public void RenamingLeavesTheOrderAlone()
    {
        var store = Store();

        store.Remember(Folder("one"), "One");
        store.Remember(Folder("two"), "Two");
        store.Rename(Folder("one"), "Renamed");

        Assert.Equal(["Two", "Renamed"], store.All.Select(entry => entry.Name));

        // A path nothing knows about is not an error, it is a list that has moved on.
        store.Rename(Folder("nowhere"), "Nothing");

        Assert.Equal(2, store.All.Count);
    }

    [Fact]
    public void ForgettingTakesOnlyThatEntryAndLeavesTheFolderAlone()
    {
        var store = Store();

        store.Remember(Folder("one"), "One");
        store.Remember(Folder("two"), "Two");
        store.Forget(Folder("one"));

        Assert.Equal(["Two"], store.All.Select(entry => entry.Name));
        Assert.True(_files.DirectoryExists(Path.Combine(_home, "one")));
    }

    [Fact]
    public void APathThatHasGoneIsKeptAndReadsAsMissing()
    {
        var store = Store();

        var folder = Folder("one");
        store.Remember(folder, "One");
        Directory.Delete(folder);

        store.Refresh();

        var entry = store.All.Single();

        Assert.True(entry.IsMissing);
        Assert.Equal("One", entry.Name);
    }

    // A project is a folder for most apps and a file for some.
    [Fact]
    public void AFileCountsAsBeingThere()
    {
        var store = Store();

        _files.CreateDirectory(_home);
        var file = Path.Combine(_home, "one.project");
        _files.WriteAllText(file, "{}");

        Assert.True(store.Remember(file, "One").Exists);
    }

    [Fact]
    public void WhenItIsOpenedIsKeptAndReadsBackAsTheSameInstant()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        Store().Remember(Folder("one"), "One");

        var stamp = Store().All.Single().LastOpened;

        Assert.NotNull(stamp);
        Assert.InRange(stamp.Value, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void TheOldestFallOffTheEnd()
    {
        var store = Store(services =>
            services.AddSingleton(new RecentProjectsOptions(SettingsScope.Global, Limit: 3)));

        foreach (var name in new[] { "one", "two", "three", "four" })
        {
            store.Remember(Folder(name), name);
        }

        Assert.Equal(["four", "three", "two"], store.All.Select(entry => entry.Name));
    }

    // The three arrays are written together, so they can only be out of step if somebody
    // edited the file. A short one truncates rather than pairing a path with another
    // entry's name.
    [Fact]
    public void AHandEditedFileWithArraysOutOfStepTruncatesRatherThanPairingWrongly()
    {
        var provider = Services();

        provider.GetRequiredService<IApplicationState>().Apply(SettingsScope.Global,
        [
            SettingsEdit.Set("projects.recent.paths", new[] { Folder("one"), Folder("two") }),
            SettingsEdit.Set("projects.recent.names", new[] { "One" }),
            SettingsEdit.Set("projects.recent.opened", Array.Empty<string>()),
        ]);

        var all = provider.GetRequiredService<IRecentProjects>().All;

        Assert.Equal(2, all.Count);
        Assert.Equal("One", all[0].Name);

        // No name of its own, so the last segment stands in, and no stamp at all reads as
        // no stamp rather than as a wrong one.
        Assert.Equal("two", all[1].Name);
        Assert.Null(all[1].LastOpened);
    }

    [Fact]
    public void ABlankPathIsDroppedRatherThanKeptAsAnEntryNothingCanBeAskedAbout()
    {
        var provider = Services();

        provider.GetRequiredService<IApplicationState>().Apply(SettingsScope.Global,
        [
            SettingsEdit.Set("projects.recent.paths", new[] { string.Empty, Folder("one") }),
            SettingsEdit.Set("projects.recent.names", new[] { "Blank", "One" }),
        ]);

        Assert.Equal(
            ["One"],
            provider.GetRequiredService<IRecentProjects>().All.Select(entry => entry.Name));
    }

    [Fact]
    public void AToolsListIsItsOwnAndNeverTheGlobalOne()
    {
        var mine = Store(services =>
            services.AddSingleton(new RecentProjectsOptions(SettingsScope.ForTool("hoard"))));

        mine.Remember(Folder("one"), "One");

        Assert.Equal(["One"], mine.All.Select(entry => entry.Name));
        Assert.Empty(Store().All);
    }

    private IRecentProjects Store(Action<IServiceCollection>? extra = null) =>
        Services(extra).GetRequiredService<IRecentProjects>();

    /// <summary>
    /// A provider built the way an executable builds one, since Core keeps its
    /// implementations internal. Every user directory lands under one temporary root.
    /// </summary>
    private ServiceProvider Services(Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();

        // First, so the TryAdd inside AddKitbashIO leaves it alone.
        services.TryAddSingleton<IUserDirectories>(new TestDirectories(_home));

        extra?.Invoke(services);

        return services
            .AddKitbashRecentProjects(SettingsScope.Global)
            .BuildServiceProvider();
    }

    private string Folder(string name)
    {
        var path = Path.Combine(_home, name);
        _files.CreateDirectory(path);

        return path;
    }

    private sealed class TestDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "run");
    }
}
