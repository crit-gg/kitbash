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
/// The three that write. All of them are off unless a tool asks, since each writes to more
/// than one cell at a time and each arrives through a key a person hits meaning something
/// else.
/// </summary>
public sealed class GridWriteTests
{
    /// <summary>Nothing writes unless the grid was told it may.</summary>
    [AvaloniaFact]
    public void NothingWritesByDefault()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Delete);

            Assert.Equal("a0", items[0].Id);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Delete empties the block.</summary>
    [AvaloniaFact]
    public void ClearEmptiesTheBlock()
    {
        var grid = Grid(out var window, out var items, CellActions.Clear, GridSelectionUnit.Cell);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);
            Press(grid, Key.Down, KeyModifiers.Shift);
            Press(grid, Key.Delete);

            Assert.Equal(string.Empty, items[0].Id);
            Assert.Equal(string.Empty, items[0].Name);
            Assert.Equal(string.Empty, items[1].Id);
            Assert.Equal(string.Empty, items[1].Name);

            // Outside the block nothing moved.
            Assert.Equal("c0", items[0].Kind);
            Assert.Equal("a2", items[2].Id);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A read only column refuses the whole write rather than taking part of it.</summary>
    [AvaloniaFact]
    public void AColumnThatTakesNothingRefusesTheWholeWrite()
    {
        var grid = Grid(out var window, out var items, CellActions.Clear, GridSelectionUnit.Cell, locked: "NAME");

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);
            Press(grid, Key.Delete);

            Assert.Equal("a0", items[0].Id);
            Assert.Equal("b0", items[0].Name);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Cut writes the block out and empties the same cells.</summary>
    [AvaloniaFact]
    public async Task CutTakesTheBlockAndEmptiesIt()
    {
        var grid = Grid(out var window, out var items, CellActions.Cut, GridSelectionUnit.Cell);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);
            Press(grid, Key.X, KeyModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("a0\tb0", await Text(window));
            Assert.Equal(string.Empty, items[0].Id);
            Assert.Equal(string.Empty, items[0].Name);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Paste lands at the anchor in the shape it arrived in.</summary>
    [AvaloniaFact]
    public async Task PasteLandsAtTheAnchor()
    {
        var grid = Grid(out var window, out var items, CellActions.Paste, GridSelectionUnit.Cell);

        try
        {
            await Put(window, "one\ttwo\r\nthree\tfour");

            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.V, KeyModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("one", items[1].Id);
            Assert.Equal("two", items[1].Name);
            Assert.Equal("three", items[2].Id);
            Assert.Equal("four", items[2].Name);

            // Above the anchor nothing moved.
            Assert.Equal("a0", items[0].Id);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A block that divides evenly takes the paste repeated to fill it.</summary>
    [AvaloniaFact]
    public async Task PasteRepeatsToFillABlock()
    {
        var grid = Grid(out var window, out var items, CellActions.Paste, GridSelectionUnit.Cell);

        try
        {
            await Put(window, "x");

            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.Right, KeyModifiers.Shift);
            Press(grid, Key.Down, KeyModifiers.Shift);
            Press(grid, Key.V, KeyModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("x", items[0].Id);
            Assert.Equal("x", items[0].Name);
            Assert.Equal("x", items[1].Id);
            Assert.Equal("x", items[1].Name);
            Assert.Equal("c0", items[0].Kind);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A paste that runs off the end of the rows writes nothing at all.</summary>
    [AvaloniaFact]
    public async Task APasteThatRunsOffTheEndIsRefused()
    {
        var grid = Grid(out var window, out var items, CellActions.Paste, GridSelectionUnit.Cell);

        try
        {
            await Put(window, "one\r\ntwo\r\nthree");

            grid.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.V, KeyModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("a2", items[2].Id);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A quoted field keeps the tab inside it rather than splitting on it.</summary>
    [AvaloniaFact]
    public async Task AQuotedFieldSurvivesTheRoundTrip()
    {
        var grid = Grid(out var window, out var items, CellActions.Paste, GridSelectionUnit.Cell);

        try
        {
            await Put(window, "\"has\ttab\"");

            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.V, KeyModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("has\ttab", items[0].Id);
            Assert.Equal("b0", items[0].Name);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task Put(Window window, string text)
    {
        if (window.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<string?> Text(Window window) =>
        window.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;

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

    private static DataGrid Grid(
        out Window window,
        out Entry[] items,
        CellActions actions = CellActions.Copy,
        GridSelectionUnit unit = GridSelectionUnit.Row,
        string? locked = null)
    {
        items = [.. Enumerable.Range(0, 3).Select(number => new Entry($"a{number}", $"b{number}", $"c{number}"))];

        var grid = new DataGrid { CellActions = actions, SelectionUnit = unit };

        grid.Columns.Add(Column("ID", entry => entry.Id, (entry, value) => entry.Id = value ?? string.Empty, locked));
        grid.Columns.Add(Column("NAME", entry => entry.Name, (entry, value) => entry.Name = value ?? string.Empty, locked));
        grid.Columns.Add(Column("KIND", entry => entry.Kind, (entry, value) => entry.Kind = value ?? string.Empty, locked));

        grid.ItemsSource = new GridRows(items);

        window = new Window { Content = grid, Width = 520, Height = 320 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private static GridColumn Column(
        string header,
        Func<Entry, string> read,
        Action<Entry, string?> write,
        string? locked)
    {
        var column = new GridColumn
        {
            Header = header,
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry is null ? null : read(entry) }),
            Value = item => read((Entry)item),
        };

        if (header != locked)
        {
            column.Write = (item, value) => write((Entry)item, value);
        }

        return column;
    }

    private sealed class Entry(string id, string name, string kind)
    {
        public string Id { get; set; } = id;

        public string Name { get; set; } = name;

        public string Kind { get; set; } = kind;
    }
}
