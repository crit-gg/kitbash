using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// Where a row puts its editor, drawn by the real settings window with no display behind
/// it. A page carrying both layouts, so the two are read off one drawn page.
/// </summary>
public sealed class SettingsRowLayoutTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-layout-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;
    private readonly SettingsPage _page;

    public SettingsRowLayoutTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashSettingsSchema()
            .AddSingleton<IApplicationRestart>(new FakeRestart())
            .BuildServiceProvider();

        _page = new SettingsPage
        {
            Id = "layout",
            Title = "Layout",
            Home = SettingsHome.Application,
            Sections =
            [
                new SettingsSection("Rows", [Text("beside", SettingsRowLayout.Beside), Wide()]),
            ],
        };
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Beside is the default, so a row that names no layout does not move.</summary>
    [Fact]
    public void BesideIsWhatARowGetsWithoutAsking()
    {
        var row = Text("plain", SettingsRowLayout.Beside);

        Assert.Equal(SettingsRowLayout.Beside, row.Layout);
        Assert.Equal(SettingsRowLayout.Beside, new SettingsEditorRow
        {
            Name = "Editor",
            Editor = new FakeEditor(),
        }.Layout);
    }

    /// <summary>
    /// The two halves land where the layout says: beside is one line in two columns, and
    /// below is the name across both with the editor on the line under it.
    /// </summary>
    [AvaloniaFact]
    public async Task ARowPutsItsEditorWhereItsLayoutSays()
    {
        var (window, model) = await Shown();

        try
        {
            var beside = Row(model, "beside");
            var below = Row(model, "wide");

            Assert.False(beside.IsBelow);
            Assert.Equal((1, 0, 1, 1), (beside.HeadSpan, beside.BodyRow, beside.BodyColumn, beside.BodySpan));

            Assert.True(below.IsBelow);
            Assert.Equal((2, 1, 0, 2), (below.HeadSpan, below.BodyRow, below.BodyColumn, below.BodySpan));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Drawn for real: a row sitting below is as wide as the whole row, and one beside it
    /// is the narrower column it has always been.
    /// </summary>
    [AvaloniaFact]
    public async Task ARowBelowIsDrawnAcrossTheWholeRow()
    {
        var (window, _) = await Shown();

        try
        {
            var rows = window.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Classes.Contains("settingRow"))
                .ToArray();

            Assert.Equal(2, rows.Length);

            var beside = Body(rows[0]);
            var below = Body(rows[1]);

            Assert.True(beside.Bounds.Width > 0, "the beside row was never laid out");
            Assert.True(below.Bounds.Width > beside.Bounds.Width, "the below row is no wider");

            // Beside sits level with the name and below starts under it.
            Assert.Equal(0, beside.Bounds.Y);
            Assert.True(below.Bounds.Y > 0, "the below row is still on the first line");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The grid a row draws into, whichever kind of row it is.</summary>
    private static Control Body(Border row)
    {
        var grid = Assert.IsType<Grid>(row.Child);

        // The name is always the first child, so the body is the other one.
        return (Control)grid.Children[1];
    }

    private static SettingDescriptor<string> Text(string name, SettingsRowLayout layout) => new()
    {
        Key = $"layout.{name}",
        Name = name,
        Description = "A setting drawn for this test.",
        Default = string.Empty,
        Layout = layout,
    };

    private static SettingDescriptor<string> Wide() => new()
    {
        Key = "layout.wide",
        Name = "wide",
        Description = "A setting too wide to read beside its name.",
        Default = string.Empty,
        Layout = SettingsRowLayout.Below,
    };

    private static SettingsRowViewModel Row(SettingsWindowViewModel model, string name) =>
        model.Page!.Sections.SelectMany(section => section.Rows).Single(row => row.Name == name);

    private async Task<(SettingsWindow Window, SettingsWindowViewModel Model)> Shown()
    {
        var model = new SettingsWindowViewModel(
            new SettingsSchema(SettingsScope.Global, "Kitbash Settings", [_page]),
            _services.GetRequiredService<ISettingsInspector>(),
            _services.GetRequiredService<ISettingsWriter>(),
            _services.GetRequiredService<ISettingsValueConverter>(),
            _services.GetRequiredService<IPathShortener>(),
            _services.GetRequiredService<IApplicationRestart>());

        await model.OpenAsync(model.First());

        Assert.Equal(string.Empty, model.Problem);

        var window = new SettingsWindow { DataContext = model, Width = 900, Height = 600 };

        window.Show();

        return (window, model);
    }

    private sealed class FakeEditor : ISettingsEditor
    {
        public bool IsDirty => false;

        public bool IsValid => true;

        public bool IsPageWritable { get; set; } = true;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public Task LoadAsync(CancellationToken token = default) => Task.CompletedTask;

        public Task SaveAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Discard()
        {
        }
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
