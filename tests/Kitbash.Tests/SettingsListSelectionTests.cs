using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Kitbash.Core.Platform.Openers;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// Picking a row in a settings list. The grid holds wrappers, so the binding that carries
/// a picked row back to the page is the one thing standing between Remove and doing
/// nothing at all.
/// </summary>
public sealed class SettingsListSelectionTests
{
    /// <summary>
    /// A row picked in the grid reaches the page, which is what Remove is enabled by.
    /// </summary>
    [AvaloniaFact]
    public async Task PickingARowEnablesRemove()
    {
        var (window, editor, grid) = await ShownAsync();

        try
        {
            Assert.False(editor.CanRemove);

            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.Same(editor.Rows[1], editor.Selected);
            Assert.True(editor.CanRemove);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Add picks the row it just made, and the grid has to follow. This is the direction
    /// that used to clear itself, since the grid could not find a row it had never been
    /// handed and reset the selection to nothing.
    /// </summary>
    [AvaloniaFact]
    public async Task AddingARowPicksItInTheGrid()
    {
        var (window, editor, grid) = await ShownAsync();

        try
        {
            editor.AddCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var added = editor.Rows[^1];

            Assert.Same(added, editor.Selected);
            Assert.True(editor.CanRemove);
            Assert.Same(added, grid.SelectedValue);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>And Remove then takes off the row that was picked, rather than any other.</summary>
    [AvaloniaFact]
    public async Task RemoveTakesOffThePickedRow()
    {
        var (window, editor, grid) = await ShownAsync();

        try
        {
            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            editor.RemoveCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["First", "Third"], editor.Rows.Select(row => row.Name));
            Assert.Null(editor.Selected);
            Assert.False(editor.CanRemove);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Awaited rather than blocked on. The editor's load continues on the UI thread, so
    /// waiting for it from that thread deadlocks the dispatcher.
    /// </summary>
    private static async Task<(Window Window, CustomToolsEditor Editor, DataGrid Grid)> ShownAsync()
    {
        var editor = new CustomToolsEditor(new StoredOpeners(
        [
            new CustomOpener("First", "/usr/bin/first", string.Empty),
            new CustomOpener("Second", "/usr/bin/second", string.Empty),
            new CustomOpener("Third", "/usr/bin/third", string.Empty),
        ]));

        await editor.LoadAsync();

        var view = new CustomToolsEditorView { DataContext = editor };
        var window = new Window { Content = view, Width = 640, Height = 320 };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var grid = Assert.Single(view.GetLogicalDescendants().OfType<DataGrid>());

        return (window, editor, grid);
    }

    /// <summary>The file, without one. Read hands back what it was built with.</summary>
    private sealed class StoredOpeners(IReadOnlyList<CustomOpener> stored) : ICustomOpeners
    {
        public IReadOnlyList<CustomOpener> Read() => stored;

        public void Write(IReadOnlyList<CustomOpener> openers) => stored = openers;
    }
}
