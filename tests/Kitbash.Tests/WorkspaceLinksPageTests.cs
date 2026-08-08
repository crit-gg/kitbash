using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Kitbash.Core;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;
using Kitbash.Core.Workspaces;
using Kitbash.Settings;
using Kitbash.Ui.Controls;
using Kitbash.Ui.Settings;
using Kitbash.ViewModels;
using Kitbash.Views;
using Kitbash.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Tests;

/// <summary>
/// The workspace links page, drawn by the real settings window with no display behind it,
/// over real workspace config files in a temporary folder.
/// </summary>
public sealed class WorkspaceLinksPageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kitbash-links-page-{Guid.NewGuid():N}");

    private readonly ServiceProvider _services;
    private readonly WorkspaceLinksSettingsSchema _schema;

    public WorkspaceLinksPageTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IUserDirectories>(new FakeUserDirectories(_root))
            .AddKitbashSettingsSchema()
            .AddKitbashKnownWorkspaceSettings()
            .AddSingleton<WorkspaceLog>()
            .AddSingleton<WorkspaceLinkIcons>()
            .AddSingleton<IWorkspaceLinks, WorkspaceLinks>()
            .AddSingleton<WorkspaceLinksSettingsSchema>()
            .AddSingleton<IApplicationRestart>(new FakeRestart())
            .BuildServiceProvider();

        _schema = _services.GetRequiredService<WorkspaceLinksSettingsSchema>();
    }

    public void Dispose()
    {
        _services.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Every workspace has the page, since the store keeps a place for each.</summary>
    [AvaloniaFact]
    public async Task EveryWorkspaceHasItsOwnPage()
    {
        Workspace("Art", Link("Design docs", "https://example.com/design"));
        Workspace("Docs");

        var (window, model) = await Shown("Art");

        try
        {
            Assert.Equal(["Art", "Docs"], Places(model));

            // The window opens a layered page on the personal layer, which this workspace
            // has no file for at all.
            Assert.Equal(SettingsLayer.User, model.Page!.Layer);
            Assert.Empty(Editor(model).Rows);

            model.Page.Layer = SettingsLayer.TeamShared;
            Assert.Equal(["Design docs"], Editor(model).Rows.Select(row => row.Label));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The row is shared by every place, so an editor of its own per page is what keeps
    /// one workspace's list off another's.
    /// </summary>
    [AvaloniaFact]
    public async Task TwoWorkspacesEditTheirOwnLists()
    {
        Workspace("Art", Link("Design docs", "https://example.com/design"));
        Workspace("Docs", Link("Style guide", "https://example.com/style"));

        var (window, model) = await Shown("Art");

        try
        {
            model.Page!.Layer = SettingsLayer.TeamShared;

            var art = Editor(model);

            Assert.Equal(["Design docs"], art.Rows.Select(row => row.Label));

            await Open(model, "Docs");
            model.Page!.Layer = SettingsLayer.TeamShared;

            var docs = Editor(model);

            Assert.NotSame(art, docs);
            Assert.Equal(["Style guide"], docs.Rows.Select(row => row.Label));

            // Staged on one and untouched on the other, which is what a page keeping its
            // own changes means.
            docs.Rows[0].Label = "The style guide";

            await Open(model, "Art");
            model.Page!.Layer = SettingsLayer.TeamShared;
            Assert.Equal(["Design docs"], Editor(model).Rows.Select(row => row.Label));

            Assert.Equal(1, model.DirtyCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The picker swaps which file is drawn and nothing else, so work staged on the layer
    /// being left is still there on the way back.
    /// </summary>
    [AvaloniaFact]
    public async Task MovingThePickerKeepsWhatIsStagedOnBothLayers()
    {
        var root = Workspace("Art", Link("Design docs", "https://example.com/design"));

        Personal(root, Link("My board", "https://example.com/board"));

        var (window, model) = await Shown("Art");

        try
        {
            var editor = Editor(model);

            Assert.Equal(SettingsLayer.User, model.Page!.Layer);
            Assert.Equal(["My board"], editor.Rows.Select(row => row.Label));

            model.Page.Layer = SettingsLayer.TeamShared;
            Assert.Equal(["Design docs"], editor.Rows.Select(row => row.Label));

            model.Page.Layer = SettingsLayer.User;

            editor.Rows[0].Label = "My own board";

            model.Page.Layer = SettingsLayer.TeamShared;
            Assert.Equal(["Design docs"], editor.Rows.Select(row => row.Label));

            model.Page.Layer = SettingsLayer.User;
            Assert.Equal(["My own board"], editor.Rows.Select(row => row.Label));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A save writes the layer that changed and leaves the other file alone.</summary>
    [AvaloniaFact]
    public async Task SavingWritesOnlyTheLayerThatChanged()
    {
        var root = Workspace("Art", Link("Design docs", "https://example.com/design"));

        Personal(root, Link("My board", "https://example.com/board"));

        var (window, model) = await Shown("Art");

        try
        {
            var editor = Editor(model);

            model.Page!.Layer = SettingsLayer.User;
            editor.Rows[0].Address = "https://example.com/my-board";

            Assert.Equal(1, model.DirtyCount);

            await model.Page.SaveAsync(TestContext.Current.CancellationToken);

            var links = _services.GetRequiredService<IWorkspaceLinks>();

            Assert.Equal(
                "https://example.com/my-board",
                Assert.Single(links.ReadEntries(root, SettingsLayer.User)).Url);

            Assert.Equal(
                "https://example.com/design",
                Assert.Single(links.ReadEntries(root, SettingsLayer.TeamShared)).Url);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Both layers at once, which one Save writes because the editor owns both files.</summary>
    [AvaloniaFact]
    public async Task ASaveCanWriteBothLayers()
    {
        var root = Workspace("Art", Link("Design docs", "https://example.com/design"));

        Personal(root, Link("My board", "https://example.com/board"));

        var (window, model) = await Shown("Art");

        try
        {
            var editor = Editor(model);

            model.Page!.Layer = SettingsLayer.TeamShared;
            editor.Rows[0].Label = "The design docs";

            model.Page.Layer = SettingsLayer.User;
            editor.Rows[0].Label = "My own board";

            await model.Page.SaveAsync(TestContext.Current.CancellationToken);

            var links = _services.GetRequiredService<IWorkspaceLinks>();

            Assert.Equal(
                "The design docs",
                Assert.Single(links.ReadEntries(root, SettingsLayer.TeamShared)).Label);

            Assert.Equal(
                "My own board",
                Assert.Single(links.ReadEntries(root, SettingsLayer.User)).Label);

            Assert.Equal(0, model.DirtyCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Add puts a row on the grid and picks it, and the row says why it cannot be written
    /// until it is filled in.
    /// </summary>
    [AvaloniaFact]
    public async Task AddingARowPutsItOnTheGridAndPicksIt()
    {
        var root = Workspace("Art", Link("Design docs", "https://example.com/design"));

        var (window, model) = await Shown("Art");

        try
        {
            model.Page!.Layer = SettingsLayer.TeamShared;

            var editor = Editor(model);

            editor.AddCommand.Execute(null);

            var added = editor.Rows[1];

            Assert.Same(added, editor.Selected);
            Assert.Equal(editor.Rows.Count, editor.Grid.Count);

            // A row with nothing in it stops the save rather than being dropped.
            Assert.False(added.IsValid);
            Assert.False(model.CanSave);

            added.Label = "Issue tracker";
            added.Address = "example.com/issues";
            Assert.False(model.CanSave);
            Assert.Contains("http", added.Problem, StringComparison.Ordinal);

            added.Address = "https://example.com/issues";
            Assert.True(model.CanSave);

            await model.Page.SaveAsync(TestContext.Current.CancellationToken);

            Assert.Equal(
                ["Design docs", "Issue tracker"],
                _services.GetRequiredService<IWorkspaceLinks>()
                    .ReadEntries(root, SettingsLayer.TeamShared).Select(entry => entry.Label));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Escape in the grid cancels the edit, which is the row's own IEditableObject putting
    /// every field back.
    /// </summary>
    [AvaloniaFact]
    public async Task CancellingAnEditPutsTheRowBack()
    {
        Workspace("Art", Link("Design docs", "https://example.com/design", "file"));

        var (window, model) = await Shown("Art");

        try
        {
            model.Page!.Layer = SettingsLayer.TeamShared;

            var row = Assert.Single(Editor(model).Rows);

            row.BeginEdit();

            row.Label = "Something else";
            row.Address = "https://example.com/elsewhere";
            row.Icon = row.Icons.Single(icon => icon.Glyph == IconGlyph.GitBranch);

            row.CancelEdit();

            Assert.Equal("Design docs", row.Label);
            Assert.Equal("https://example.com/design", row.Address);
            Assert.Equal(IconGlyph.File, row.Icon.Glyph);
            Assert.Equal(0, model.DirtyCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Remove takes the picked row and nothing else.</summary>
    [AvaloniaFact]
    public async Task RemoveTakesThePickedRow()
    {
        Workspace(
            "Art",
            Link("Design docs", "https://example.com/design"),
            Link("Issue tracker", "https://example.com/issues"));

        var (window, model) = await Shown("Art");

        try
        {
            model.Page!.Layer = SettingsLayer.TeamShared;

            var editor = Editor(model);

            // Nothing picked, so there is nothing to take off.
            Assert.False(editor.CanRemove);

            editor.Selected = editor.Rows[0];
            Assert.True(editor.CanRemove);

            editor.RemoveCommand.Execute(null);

            Assert.Equal(["Issue tracker"], editor.Rows.Select(row => row.Label));
            Assert.Single(editor.Grid);
            Assert.Equal(1, model.DirtyCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The window reads every open page again when it comes back to the front, and an
    /// unsaved change has to survive that.
    /// </summary>
    [AvaloniaFact]
    public async Task ComingBackToTheWindowKeepsAnUnsavedChange()
    {
        Workspace("Art", Link("Design docs", "https://example.com/design"));

        var (window, model) = await Shown("Art");

        try
        {
            model.Page!.Layer = SettingsLayer.TeamShared;

            var editor = Editor(model);

            editor.AddCommand.Execute(null);

            editor.Rows[1].Label = "Issue tracker";
            editor.Rows[1].Address = "https://example.com/issues";

            Assert.Equal(1, model.DirtyCount);

            await model.ReloadAsync();

            Assert.Equal(["Design docs", "Issue tracker"], editor.Rows.Select(row => row.Label));
            Assert.Equal(1, model.DirtyCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A field changing has to reach the page, or the unsaved count never moves once a
    /// cell is left and Save stays grey over a real change.
    /// </summary>
    [AvaloniaFact]
    public async Task ChangingAFieldReachesThePage()
    {
        Workspace("Art", Link("Design docs", "https://example.com/design"));

        var (window, model) = await Shown("Art");

        try
        {
            model.Page!.Layer = SettingsLayer.TeamShared;

            var row = Assert.Single(Editor(model).Rows);

            Assert.Equal(0, model.DirtyCount);

            row.Label = "The design docs";

            Assert.Equal(1, model.DirtyCount);
            Assert.True(model.CanSave);

            row.Label = "Design docs";

            Assert.Equal(0, model.DirtyCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A row that is no longer on the list must not still be marking the page dirty, which
    /// is what a listener left on it would do.
    /// </summary>
    [AvaloniaFact]
    public async Task ARowThatWasDiscardedIsNoLongerHeard()
    {
        Workspace("Art", Link("Design docs", "https://example.com/design"));

        var (window, model) = await Shown("Art");

        try
        {
            model.Page!.Layer = SettingsLayer.TeamShared;

            var editor = Editor(model);
            var before = Assert.Single(editor.Rows);

            before.Label = "Changed";
            Assert.Equal(1, model.DirtyCount);

            model.Page.Discard();
            Assert.Equal(0, model.DirtyCount);

            // The row the discard threw away, still being typed into by nothing.
            before.Label = "Changed again";

            Assert.Equal(0, model.DirtyCount);
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<string> Places(SettingsWindowViewModel model) =>
        model.Rows.Select(row => row.Item).OfType<SettingsTreeNode>()
            .Where(node => node.IsGroup && node.Place is { IsNamed: true })
            .Select(node => node.Title);

    private static WorkspaceLinksEditor Editor(SettingsWindowViewModel model) =>
        model.Page!.Sections
            .SelectMany(section => section.Rows)
            .OfType<SettingsEditorRowViewModel>()
            .Select(row => row.Editor)
            .OfType<WorkspaceLinksEditor>()
            .Single();

    private static string Link(string label, string url, string? icon = null)
    {
        var text = $"[[workspace.links]]{Environment.NewLine}"
            + $"label = \"{label}\"{Environment.NewLine}"
            + $"url = \"{url}\"{Environment.NewLine}";

        return icon is null ? text : text + $"icon = \"{icon}\"{Environment.NewLine}";
    }

    private static void Personal(string root, string links)
    {
        var file = new WorkspacePaths(root).FileFor(SettingsScope.Global, SettingsLayer.User);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, links);
    }

    private string Workspace(string name, params string[] links)
    {
        var root = Path.Combine(_root, "workspaces", name);
        var file = new WorkspacePaths(root).FileFor(SettingsScope.Global, SettingsLayer.TeamShared);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        // Written before the folder is registered, since registering scaffolds a config
        // into a workspace that has none and this one is meant to have its own.
        File.WriteAllText(file, string.Join(Environment.NewLine, links));

        _services.GetRequiredService<IWorkspaceRegistry>().Add(root);

        return root;
    }

    private static Task Open(SettingsWindowViewModel model, string workspace) =>
        model.OpenAsync(
            model.Rows.Select(row => row.Item).OfType<SettingsTreeNode>()
                .Single(node => !node.IsGroup && node.Place is { Name: var name } && name == workspace));

    private async Task<(SettingsWindow Window, SettingsWindowViewModel Model)> Shown(string workspace)
    {
        var model = new SettingsWindowViewModel(
            new SettingsSchema(SettingsScope.Global, "Kitbash Settings", [_schema.Page]),
            _services.GetRequiredService<ISettingsInspector>(),
            _services.GetRequiredService<ISettingsWriter>(),
            _services.GetRequiredService<ISettingsValueConverter>(),
            _services.GetRequiredService<IPathShortener>(),
            _services.GetRequiredService<IApplicationRestart>());

        await Open(model, workspace);

        Assert.Equal(string.Empty, model.Problem);

        var window = new SettingsWindow { DataContext = model };

        window.Show();

        return (window, model);
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
