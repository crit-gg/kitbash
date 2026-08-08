using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Platform.Openers;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Settings;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Kitbash.ViewModels;
using Kitbash.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// The Open in page drawn by the real settings window with no display behind it, over a
/// fixed list of found tools and a real config file in a temporary folder.
/// </summary>
public sealed class HiddenToolsPageTests : IDisposable
{
    private static readonly WorkspaceOpener[] Found =
    [
        new("vscode", "Visual Studio Code", "vscode", "/usr/bin/code", WorkspaceOpenerKind.Editor),
        new("jetbrains.RD", "Rider", "jetbrains/RD", "/opt/rider/bin/rider", WorkspaceOpenerKind.Editor),
        new("konsole", "Konsole", "konsole", "/usr/bin/konsole", WorkspaceOpenerKind.Terminal),
        new("custom.0", "Emacs", "custom", "/usr/bin/emacs", WorkspaceOpenerKind.Custom),
    ];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-hidden-page-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;
    private readonly CustomToolsSettingsSchema _schema;

    public HiddenToolsPageTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddSingleton<IWorkspaceOpeners>(new FakeOpeners(Found))
            .AddKitbashSettingsSchema()
            .AddKitbashWorkspaceOpeners()
            .AddSingleton<CustomToolsEditor>()
            .AddSingleton<HiddenToolsEditor>()
            .AddSingleton<CustomToolsSettingsSchema>()
            .AddSingleton<IApplicationRestart>(new FakeRestart())
            .BuildServiceProvider();

        _schema = _services.GetRequiredService<CustomToolsSettingsSchema>();
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>A row per found tool, all offered, and nothing for a tool a person added.</summary>
    [AvaloniaFact]
    public async Task EveryFoundToolIsARowAndACustomOneIsNot()
    {
        var (window, _) = await Shown();

        try
        {
            Assert.Equal(["Konsole", "Rider", "Visual Studio Code"], Editor.Rows.Select(row => row.Name));
            Assert.All(Editor.Rows, row => Assert.True(row.IsOffered));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Turning one off is one change, and saving writes its id and nothing else.</summary>
    [AvaloniaFact]
    public async Task TurningOneOffWritesItsId()
    {
        var (window, model) = await Shown();

        try
        {
            Row("Rider").IsOffered = false;

            Assert.Equal(1, model.DirtyCount);

            await model.Page!.SaveAsync(TestContext.Current.CancellationToken);

            Assert.Equal(["jetbrains.RD"], Hidden.Read());
            Assert.False(model.IsDirty);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Turning it back on before a save takes the change off again.</summary>
    [AvaloniaFact]
    public async Task TurningOneOffAndOnAgainStagesNothing()
    {
        var (window, model) = await Shown();

        try
        {
            Row("Konsole").IsOffered = false;
            Row("Konsole").IsOffered = true;

            Assert.Equal(0, model.DirtyCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>What the file says is what the page opens on.</summary>
    [AvaloniaFact]
    public async Task AStoredIdOpensTheRowTurnedOff()
    {
        Hidden.Write(["vscode"]);

        var (window, model) = await Shown();

        try
        {
            Assert.False(Row("Visual Studio Code").IsOffered);
            Assert.True(Row("Rider").IsOffered);
            Assert.Equal(0, model.DirtyCount);

            Row("Visual Studio Code").IsOffered = true;

            Assert.Equal(1, model.DirtyCount);

            await model.Page!.SaveAsync(TestContext.Current.CancellationToken);

            Assert.Empty(Hidden.Read());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>An id with no row is kept, so uninstalling a tool does not forget the answer.</summary>
    [AvaloniaFact]
    public async Task AnIdNothingAnswersToSurvivesASave()
    {
        Hidden.Write(["emacs"]);

        var (window, model) = await Shown();

        try
        {
            Assert.All(Editor.Rows, row => Assert.True(row.IsOffered));

            Row("Konsole").IsOffered = false;

            await model.Page!.SaveAsync(TestContext.Current.CancellationToken);

            Assert.Equal(["emacs", "konsole"], Hidden.Read());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Discard puts every row back to what the file says.</summary>
    [AvaloniaFact]
    public async Task DiscardingPutsTheRowsBack()
    {
        Hidden.Write(["konsole"]);

        var (window, model) = await Shown();

        try
        {
            Row("Konsole").IsOffered = true;
            Row("Rider").IsOffered = false;

            model.Page!.Discard();

            Assert.False(Row("Konsole").IsOffered);
            Assert.True(Row("Rider").IsOffered);
            Assert.Equal(0, model.DirtyCount);
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
    public async Task TheEditorDrawsAToggleForEveryFoundTool()
    {
        var (settings, _) = await Shown();

        var view = new HiddenToolsEditorView { DataContext = Editor };
        var window = new Window { Content = view, Width = 700, Height = 300 };

        try
        {
            window.Show();

            var toggles = view.GetVisualDescendants().OfType<ToggleSwitch>().ToArray();

            Assert.Equal(Editor.Rows.Count, toggles.Length);
            Assert.All(toggles, toggle => Assert.True(toggle.IsChecked));

            // Pressing the toggle is what the row reads, so the drawn control and the model
            // are checked against each other rather than the model alone.
            toggles[0].IsChecked = false;

            Assert.False(Editor.Rows[0].IsOffered);
        }
        finally
        {
            window.Close();
            settings.Close();
        }
    }

    /// <summary>
    /// The name is drawn in a token brush and carries the program as its tip. A resource key
    /// that does not resolve leaves the foreground unset, which draws black on this surface.
    /// </summary>
    [AvaloniaFact]
    public async Task TheNameIsLitAndCarriesTheProgramAsATip()
    {
        var (settings, _) = await Shown();

        var view = new HiddenToolsEditorView { DataContext = Editor };
        var window = new Window { Content = view, Width = 700, Height = 300 };

        try
        {
            window.Show();

            Assert.True(view.TryFindResource("InkPrimary", out var ink));

            var names = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => Editor.Rows.Any(row => row.Name == text.Text))
                .ToArray();

            Assert.Equal(Editor.Rows.Count, names.Length);
            Assert.All(names, text => Assert.Same(ink, text.Foreground));

            foreach (var text in names)
            {
                var row = Editor.Rows.Single(candidate => candidate.Name == text.Text);

                Assert.Equal(row.Program, ToolTip.GetTip(text));
            }
        }
        finally
        {
            window.Close();
            settings.Close();
        }
    }

    private HiddenToolsEditor Editor => _services.GetRequiredService<HiddenToolsEditor>();

    private IHiddenOpeners Hidden => _services.GetRequiredService<IHiddenOpeners>();

    private HiddenToolRowViewModel Row(string name) => Editor.Rows.Single(row => row.Name == name);

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

    private sealed class FakeOpeners(IReadOnlyList<WorkspaceOpener> openers) : IWorkspaceOpeners
    {
        public Task<IReadOnlyList<WorkspaceOpener>> ReadAsync(CancellationToken cancellation = default) =>
            Task.FromResult(openers);

        public Task<IReadOnlyList<WorkspaceOpener>> ReadAllAsync(CancellationToken cancellation = default) =>
            Task.FromResult(openers);

        public IReadOnlyList<OpenChoice> ChoicesFor(WorkspaceOpener opener, string workspaceRoot) => [];

        public void Open(WorkspaceOpener opener, OpenChoice? choice, string workspaceRoot) =>
            throw new NotSupportedException();
    }

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
