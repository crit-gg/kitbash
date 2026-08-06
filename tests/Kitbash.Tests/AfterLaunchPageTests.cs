using Avalonia.Headless.XUnit;
using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Settings;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// The Launcher page drawn by the real settings window with no display behind it. It is
/// the first page in the app whose rows are segments, so picking one is checked here as
/// well as the file it lands in.
/// </summary>
public sealed class AfterLaunchPageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-page-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;
    private readonly AfterLaunchSettingsSchema _schema;

    public AfterLaunchPageTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashSettingsSchema()
            .AddSingleton<AfterLaunchSettingsSchema>()
            .AddSingleton<IApplicationRestart>(new FakeRestart())
            .BuildServiceProvider();

        _schema = _services.GetRequiredService<AfterLaunchSettingsSchema>();
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Four rows, each a segment of the same three options.</summary>
    [AvaloniaFact]
    public async Task ThePageDrawsEveryActionAsASegment()
    {
        var (window, model) = await Shown();

        try
        {
            var rows = Rows(model);

            Assert.Equal(4, rows.Count);

            foreach (var row in rows)
            {
                Assert.True(row.IsSegment);
                Assert.Equal(["Do nothing", "Minimize", "Close"], row.Options.Select(option => option.Label));
                Assert.Equal("Do nothing", row.ChosenOption?.Label);
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Picking one stages a change and saving writes the name into the file.</summary>
    [AvaloniaFact]
    public async Task PickingAnActionSavesItByName()
    {
        var (window, model) = await Shown();

        try
        {
            var row = Row(model, _schema.AfterExternalTool.Key);

            row.ChosenOption = row.Options.Single(option => option.Label == "Minimize");

            Assert.Equal(1, model.DirtyCount);
            Assert.True(model.CanSave);

            await model.Page!.SaveAsync(TestContext.Current.CancellationToken);

            var text = File.ReadAllText(SettingsFile);

            Assert.Contains("[launcher.after]", text, StringComparison.Ordinal);
            Assert.Contains("externalTool = \"Minimize\"", text, StringComparison.Ordinal);
            Assert.False(model.IsDirty);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Picking back what the file already says takes the change off again.</summary>
    [AvaloniaFact]
    public async Task PickingTheSavedActionAgainStagesNothing()
    {
        var (window, model) = await Shown();

        try
        {
            var row = Row(model, _schema.AfterPlay.Key);

            row.ChosenOption = row.Options.Single(option => option.Label == "Close");
            row.ChosenOption = row.Options.Single(option => option.Label == "Do nothing");

            Assert.Equal(0, model.DirtyCount);
        }
        finally
        {
            window.Close();
        }
    }

    private string SettingsFile =>
        _services.GetRequiredService<ApplicationPaths>().SettingsFileFor(SettingsScope.Global);

    private async Task<(SettingsWindow Window, SettingsWindowViewModel Model)> Shown()
    {
        var model = new SettingsWindowViewModel(
            new SettingsSchema(SettingsScope.Global, "Kitbash Settings", [_schema.Page]),
            _services.GetRequiredService<ISettingsInspector>(),
            _services.GetRequiredService<ISettingsWriter>(),
            _services.GetRequiredService<ISettingsValueConverter>(),
            _services.GetRequiredService<IPathShortener>(),
            _services.GetRequiredService<IApplicationRestart>());

        // Loaded before the window is up, since showing it opens the same page from its
        // own handler and a second open only puts back the one already read.
        await model.OpenAsync(model.First());

        Assert.Equal(string.Empty, model.Problem);

        var window = new SettingsWindow { DataContext = model };

        window.Show();

        return (window, model);
    }

    private static IReadOnlyList<SettingValueRowViewModel> Rows(SettingsWindowViewModel model) =>
    [
        .. model.Page!.Sections
            .SelectMany(section => section.Rows)
            .OfType<SettingValueRowViewModel>(),
    ];

    private static SettingValueRowViewModel Row(SettingsWindowViewModel model, string key) =>
        Rows(model).Single(row => row.Key == key);

    private sealed class FakeRestart : IApplicationRestart
    {
        public bool Restart() => throw new NotSupportedException();
    }

    private sealed class FakeUserDirectories(string root) : IUserDirectories
    {
        public string ConfigurationFor(string application) => Path.Combine(root, "config");

        public string StateFor(string application) => Path.Combine(root, "state");

        public string CacheFor(string application) => Path.Combine(root, "cache");

        public string RuntimeFor(string application) => Path.Combine(root, "runtime");
    }
}
