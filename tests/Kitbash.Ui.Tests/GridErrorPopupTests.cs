using System.Collections;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The message under the cell the keyboard is in. A tooltip needs a pointer, so without this
/// a person arrowing through a grid meets a red cell that says nothing at all.
/// </summary>
public sealed class GridErrorPopupTests
{
    /// <summary>The current cell being wrong opens the message under it.</summary>
    [AvaloniaFact]
    public void TheCurrentCellSaysWhatIsWrong()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Assert.False(Tip(grid).IsOpen);

            items[0].Break("Id", "An id cannot be empty.");
            Dispatcher.UIThread.RunJobs();

            Assert.True(Tip(grid).IsOpen);
            Assert.Equal("An id cannot be empty.", Message(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every other wrong cell stays quiet until it is reached.</summary>
    [AvaloniaFact]
    public void AWrongCellNobodyIsOnStaysQuiet()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            items[2].Break("Id", "An id cannot be empty.");
            Dispatcher.UIThread.RunJobs();

            Assert.False(Tip(grid).IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Moving onto a wrong cell opens it, and moving off closes it again.</summary>
    [AvaloniaFact]
    public void ItFollowsTheKeyboard()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            grid.Focus();
            items[0].Break("Name", "A name cannot be empty.");
            Dispatcher.UIThread.RunJobs();

            // The keyboard starts in the first column, which is not the wrong one.
            Assert.False(Tip(grid).IsOpen);

            Press(grid, Key.Right);

            Assert.True(Tip(grid).IsOpen);
            Assert.Equal("A name cannot be empty.", Message(grid));

            Press(grid, Key.Left);

            Assert.False(Tip(grid).IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Fixing the value closes it without anything having to move.</summary>
    [AvaloniaFact]
    public void FixingTheValueClosesIt()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            items[0].Break("Id", "An id cannot be empty.");
            Dispatcher.UIThread.RunJobs();

            Assert.True(Tip(grid).IsOpen);

            items[0].Mend("Id");
            Dispatcher.UIThread.RunJobs();

            Assert.False(Tip(grid).IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Dropping the selection takes the keyboard off the cell and closes it.</summary>
    [AvaloniaFact]
    public void DroppingTheSelectionClosesIt()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            items[0].Break("Id", "An id cannot be empty.");
            Dispatcher.UIThread.RunJobs();

            Assert.True(Tip(grid).IsOpen);

            grid.UnselectAll();
            Dispatcher.UIThread.RunJobs();

            Assert.False(Tip(grid).IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The cell keeps its own tooltip, which is what a pointer reaches it by.</summary>
    [AvaloniaFact]
    public void TheCellStillCarriesTheTooltip()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            items[2].Break("Id", "An id cannot be empty.");
            Dispatcher.UIThread.RunJobs();

            var cell = grid.GetVisualDescendants()
                .OfType<DataGridCell>()
                .First(cell => cell.Error is { Length: > 0 });

            Assert.Equal("An id cannot be empty.", ToolTip.GetTip(cell));
        }
        finally
        {
            window.Close();
        }
    }

    private static void Press(DataGrid grid, Key key)
    {
        grid.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
        });

        Dispatcher.UIThread.RunJobs();
    }

    private static Popup Tip(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<Popup>().Single(popup => popup.Name == "PART_ErrorTip");

    private static string? Message(DataGrid grid) =>
        (Tip(grid).Child as Border)?.Child is TextBlock text ? text.Text : null;

    private static DataGrid Grid(out Window window, out Entry[] items)
    {
        items = [.. Enumerable.Range(0, 3).Select(number => new Entry($"a{number}", $"b{number}"))];

        var grid = new DataGrid();

        grid.Columns.Add(Column("ID", "Id", entry => entry.Id));
        grid.Columns.Add(Column("NAME", "Name", entry => entry.Name));

        grid.ItemsSource = new GridRows(items);

        window = new Window { Content = grid, Width = 480, Height = 240 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private static GridColumn Column(string header, string field, Func<Entry, string> read) => new()
    {
        Header = header,
        Field = field,
        Width = new GridLength(1, GridUnitType.Star),
        CellTemplate = new FuncDataTemplate<Entry>((entry, _) =>
            new TextBlock { Text = entry is null ? null : read(entry) }),
    };

    /// <summary>An item that reports its own errors, the way a tool's row model would.</summary>
    private sealed class Entry(string id, string name) : INotifyDataErrorInfo
    {
        private readonly Dictionary<string, string> wrong = [];

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public string Id => id;

        public string Name => name;

        public bool HasErrors => wrong.Count > 0;

        public void Break(string field, string why)
        {
            wrong[field] = why;
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(field));
        }

        public void Mend(string field)
        {
            wrong.Remove(field);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(field));
        }

        public IEnumerable GetErrors(string? propertyName) =>
            propertyName is not null && wrong.TryGetValue(propertyName, out var why) ? new[] { why } : [];
    }
}
