using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// The first setting the app stores as an enum, over a real settings file in a temporary
/// folder, so the value is written and read the way it will be on a person's machine.
/// </summary>
public sealed class AfterLaunchSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-after-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;
    private readonly AfterLaunchSettingsSchema _schema;
    private readonly IApplicationSettings _settings;

    public AfterLaunchSettingsTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashApplicationStorage()
            .AddSingleton<AfterLaunchSettingsSchema>()
            .BuildServiceProvider();

        _schema = _services.GetRequiredService<AfterLaunchSettingsSchema>();
        _settings = _services.GetRequiredService<IApplicationSettings>();
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Nothing set means the launcher stays where it is.</summary>
    [Fact]
    public void NothingSetMeansDoNothing()
    {
        Assert.Equal(AfterLaunchAction.DoNothing, _schema.AfterEditor.Read(_settings.Global));
        Assert.Equal(AfterLaunchAction.DoNothing, _schema.AfterExternalTool.Read(_settings.Global));
    }

    /// <summary>A written value is spelled out in the file and reads back as itself.</summary>
    [Fact]
    public void AWrittenActionIsStoredByName()
    {
        Write(_schema.AfterExternalTool.Key, AfterLaunchAction.Minimize);

        Assert.Contains("Minimize", File.ReadAllText(SettingsFile), StringComparison.Ordinal);
        Assert.Equal(AfterLaunchAction.Minimize, _schema.AfterExternalTool.Read(_settings.Global));
    }

    /// <summary>Each key is its own answer, so writing one leaves the rest alone.</summary>
    [Fact]
    public void OneKeyDoesNotFollowAnother()
    {
        Write(_schema.AfterPlay.Key, AfterLaunchAction.Close);

        Assert.Equal(AfterLaunchAction.Close, _schema.AfterPlay.Read(_settings.Global));
        Assert.Equal(AfterLaunchAction.DoNothing, _schema.AfterEditor.Read(_settings.Global));
    }

    /// <summary>A hand edited file is parsed by name, and case is not part of the name.</summary>
    [Fact]
    public void AHandWrittenNameIsReadWhateverItsCase()
    {
        WriteFile($"""
            [launcher.after]
            editor = "close"
            """);

        Assert.Equal(AfterLaunchAction.Close, _schema.AfterEditor.Read(_settings.Global));
    }

    /// <summary>A name the enum does not have counts as absent, so the default decides.</summary>
    [Fact]
    public void ANameOutsideTheSetFallsBack()
    {
        WriteFile($"""
            [launcher.after]
            play = "quit"
            """);

        Assert.Equal(AfterLaunchAction.DoNothing, _schema.AfterPlay.Read(_settings.Global));
    }

    /// <summary>The three options are what the window offers, drawn as one row of segments.</summary>
    [Fact]
    public async Task EveryActionIsOffered()
    {
        var offered = await _schema.AfterEditor.Offer(TestContext.Current.CancellationToken);

        Assert.Equal(
            [AfterLaunchAction.DoNothing, AfterLaunchAction.Minimize, AfterLaunchAction.Close],
            offered.Select(choice => choice.Value));

        Assert.Equal(SettingEditor.Segment, _schema.AfterEditor.Editor);
    }

    private string SettingsFile =>
        _services.GetRequiredService<ApplicationPaths>().SettingsFileFor(SettingsScope.Global);

    private void Write(string key, AfterLaunchAction action)
    {
        _settings.Apply(SettingsScope.Global, [SettingsEdit.Set(key, action)]);
        _settings.Reload();
    }

    private void WriteFile(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, text);
        _settings.Reload();
    }

    private sealed class FakeUserDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "runtime");
    }
}
