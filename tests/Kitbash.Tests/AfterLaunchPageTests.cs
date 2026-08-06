using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Settings;
using Kitbash.Tools;
using Kitbash.ViewModels;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Kitbash.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// The Launcher page drawn by the real settings window with no display behind it. It is
/// the first page in the app whose rows are segments, so picking one is checked here as
/// well as the file it lands in.
/// </summary>
public sealed class AfterLaunchPageTests : IDisposable
{
    /// <summary>What the fake says is installed. Named the way a real id has to be.</summary>
    private static readonly string[] InstalledIds = ["splice", "hoard"];

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
            .AddSingleton<IInstalledTools>(new FakeInstalledTools(InstalledIds))
            .AddSingleton<IAfterLaunchOverrides, AfterLaunchOverrides>()
            .AddSingleton<ToolActionsEditor>()
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

    /// <summary>Five rows, each a segment of the same three options.</summary>
    [AvaloniaFact]
    public async Task ThePageDrawsEveryActionAsASegment()
    {
        var (window, model) = await Shown();

        try
        {
            var rows = Rows(model);

            Assert.Equal(5, rows.Count);

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

    /// <summary>Every installed tool gets a row, by name, following the default at first.</summary>
    [AvaloniaFact]
    public async Task EveryInstalledToolIsARow()
    {
        var (window, _) = await Shown();

        try
        {
            var rows = Editor.Rows;

            Assert.Equal(["Hoard", "Splice"], rows.Select(row => row.Name));
            Assert.All(rows, row => Assert.Null(row.Action));
            Assert.All(rows, row => Assert.Equal(
                ["Default", "Do nothing", "Minimize", "Close"],
                row.Options.Select(option => option.Label)));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>One tool answering for itself is one change, written as its own block.</summary>
    [AvaloniaFact]
    public async Task PickingForOneToolWritesOneBlock()
    {
        var (window, model) = await Shown();

        try
        {
            Choose("Splice", "Close");

            Assert.Equal(1, model.DirtyCount);

            await model.Page!.SaveAsync(TestContext.Current.CancellationToken);

            var text = File.ReadAllText(SettingsFile);

            Assert.Contains("id = \"splice\"", text, StringComparison.Ordinal);
            Assert.Contains("action = \"Close\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("hoard", text, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Putting a tool back on Default takes its block out again.</summary>
    [AvaloniaFact]
    public async Task ChoosingDefaultAgainTakesTheBlockOut()
    {
        _services.GetRequiredService<IAfterLaunchOverrides>()
            .Write([new AfterLaunchOverride(Id("splice"), AfterLaunchAction.Close)]);

        var (window, model) = await Shown();

        try
        {
            Assert.Equal(AfterLaunchAction.Close, Row("Splice").Action);

            Choose("Splice", "Default");

            Assert.Equal(1, model.DirtyCount);

            await model.Page!.SaveAsync(TestContext.Current.CancellationToken);

            Assert.Empty(_services.GetRequiredService<IAfterLaunchOverrides>().Read());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A tool that is not installed has no row and keeps its answer through a save.</summary>
    [AvaloniaFact]
    public async Task AToolThatIsGoneKeepsItsAnswer()
    {
        var absent = new AfterLaunchOverride(Id("forge"), AfterLaunchAction.Minimize);

        _services.GetRequiredService<IAfterLaunchOverrides>().Write([absent]);

        var (window, model) = await Shown();

        try
        {
            Assert.DoesNotContain(Editor.Rows, row => row.Name == "Forge");

            Choose("Hoard", "Close");

            await model.Page!.SaveAsync(TestContext.Current.CancellationToken);

            Assert.Equal(
                [absent, new AfterLaunchOverride(Id("hoard"), AfterLaunchAction.Close)],
                _services.GetRequiredService<IAfterLaunchOverrides>().Read());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The editor's own markup, drawn for real. The settings window reaches it through a
    /// template in App.axaml, which a headless app does not load, so it is shown directly.
    /// </summary>
    [AvaloniaFact]
    public async Task TheEditorDrawsASegmentForEveryTool()
    {
        var (settings, _) = await Shown();

        var view = new ToolActionsEditorView { DataContext = Editor };
        var window = new Window { Content = view, Width = 700, Height = 300 };

        try
        {
            window.Show();

            var segments = view.GetVisualDescendants().OfType<Segmented>().ToArray();

            Assert.Equal(Editor.Rows.Count, segments.Length);
            Assert.All(segments, segment => Assert.Equal(4, segment.ItemCount));

            // Pressing a radio is what the row reads, so the drawn control and the model
            // are checked against each other rather than the model alone.
            var radios = segments[0].GetVisualDescendants().OfType<RadioButton>().ToArray();

            Assert.Equal(4, radios.Length);

            radios[3].IsChecked = true;

            Assert.Equal(AfterLaunchAction.Close, Editor.Rows[0].Action);
        }
        finally
        {
            window.Close();
            settings.Close();
        }
    }

    private ToolActionsEditor Editor => _services.GetRequiredService<ToolActionsEditor>();

    private ToolActionRowViewModel Row(string name) =>
        Editor.Rows.Single(row => row.Name == name);

    /// <summary>Presses a segment the way the radio inside it does, by marking it chosen.</summary>
    private void Choose(string name, string label) =>
        Row(name).Options.Single(option => option.Label == label).IsChosen = true;

    private static ToolId Id(string value)
    {
        Assert.True(ToolId.TryParse(value, out var id));

        return id;
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
