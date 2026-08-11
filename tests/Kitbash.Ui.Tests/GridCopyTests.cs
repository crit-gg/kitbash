using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// Copying out of a grid. Tab separated and Excel compatible, which is the settled
/// convention across every grid read for this.
/// </summary>
public sealed class GridCopyTests
{
    /// <summary>Control and C writes the picked rows, one line each.</summary>
    [AvaloniaFact]
    public async Task CopyWritesThePickedRowsAsTabSeparatedText()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectionMode = SelectionMode.Multiple;
            grid.SelectedItems!.Add(grid.Rows![0]);
            grid.SelectedItems!.Add(grid.Rows![2]);

            Dispatcher.UIThread.RunJobs();
            Press(grid, Key.C, KeyModifiers.Control);

            Assert.Equal("one\tfirst\r\nthree\tthird", await Text(window));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>With nothing picked it writes the row the keyboard is on.</summary>
    [AvaloniaFact]
    public async Task CopyFallsBackToTheCurrentRow()
    {
        var grid = Grid(out var window);

        try
        {
            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            grid.SelectedItems!.Clear();
            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.C, KeyModifiers.Control);

            Assert.Equal("two\tsecond", await Text(window));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A value carrying a tab or a quote is quoted the way a spreadsheet reads it.</summary>
    [AvaloniaFact]
    public async Task AwkwardValuesAreQuoted()
    {
        var grid = Grid(out var window, new Entry("has\ttab", "says \"go\""));

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.C, KeyModifiers.Control);

            Assert.Equal("\"has\ttab\"\t\"says \"\"go\"\"\"", await Text(window));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A grid told to allow nothing copies nothing.</summary>
    [AvaloniaFact]
    public async Task NoActionsCopiesNothing()
    {
        var grid = Grid(out var window);

        try
        {
            grid.CellActions = CellActions.None;
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.C, KeyModifiers.Control);

            Assert.True(string.IsNullOrEmpty(await Text(window)));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A hidden column is not copied, since copy writes what is on the page.</summary>
    [AvaloniaFact]
    public async Task AHiddenColumnIsLeftOut()
    {
        var grid = Grid(out var window);

        try
        {
            grid.Columns[1].IsVisible = false;
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.C, KeyModifiers.Control);

            Assert.Equal("one", await Text(window));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A column that says nothing about an item is left out of the text.</summary>
    [AvaloniaFact]
    public async Task AColumnWithNoValueIsLeftOut()
    {
        var grid = Grid(out var window);

        try
        {
            grid.Columns.Insert(0, new GridColumn { Header = "PICK", Width = new GridLength(34) });
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.C, KeyModifiers.Control);

            Assert.Equal("one\tfirst", await Text(window));
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task<string?> Text(Window window)
    {
        Dispatcher.UIThread.RunJobs();

        return window.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;
    }

    private static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            Source = target,
        });

        Dispatcher.UIThread.RunJobs();
    }

    private static DataGrid Grid(out Window window, params Entry[] items)
    {
        var grid = new DataGrid();

        grid.Columns.Add(Column("ID", entry => entry.Id));
        grid.Columns.Add(Column("NAME", entry => entry.Name));

        grid.ItemsSource = new GridRows(items.Length > 0
            ? items
            : [new Entry("one", "first"), new Entry("two", "second"), new Entry("three", "third")]);

        window = new Window { Content = grid, Width = 480, Height = 300 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private static GridColumn Column(string header, Func<Entry, object?> value) => new()
    {
        Header = header,
        Width = new GridLength(1, GridUnitType.Star),
        CellTemplate = new FuncDataTemplate<Entry>((entry, _) =>
            new TextBlock { Text = entry is null ? null : value(entry)?.ToString() }),
        Value = item => value((Entry)item),
    };

    private sealed record Entry(string Id, string Name);
}
