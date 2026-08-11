using System.Collections;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a grid does with an item that reports its own errors. Nothing read
/// <see cref="INotifyDataErrorInfo"/> before this, so every tool hand rolled a cell error
/// state and got a different answer.
/// </summary>
public sealed class GridValidationTests
{
    /// <summary>A cell whose value is wrong says so, and the ones beside it do not.</summary>
    [AvaloniaFact]
    public void OnlyTheWrongCellIsMarked()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            Assert.DoesNotContain(Cells(grid), cell => cell.Error is not null);

            items[0].Break("Name", "A name cannot be empty.");
            Dispatcher.UIThread.RunJobs();

            var wrong = Assert.Single(Cells(grid), cell => cell.Error is not null);

            Assert.Equal("NAME", wrong.Column?.Header);
            Assert.Equal("A name cannot be empty.", wrong.Error);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A column that names no property of the item never shows an error.</summary>
    [AvaloniaFact]
    public void AColumnWithNoFieldNeverShowsOne()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.Columns[1].Field = null;

            items[0].Break("Name", "A name cannot be empty.");
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(Cells(grid), cell => cell.Error is not null);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The footer counts what is blocking a save, over rows nobody has scrolled to.</summary>
    [AvaloniaFact]
    public void TheFooterCountsEveryWrongCell()
    {
        var grid = Grid(out var window, out var items, rows: 40);

        try
        {
            Assert.Equal(string.Empty, grid.ErrorText);

            items[0].Break("Name", "no");

            Dispatcher.UIThread.RunJobs();

            Assert.Equal("1 cell needs fixing", grid.ErrorText);

            // A row far past the end of the viewport has no container to hear it, so a tool
            // that has checked its whole set says so. The count itself is over every row.
            items[39].Break("Id", "no");
            items[39].Break("Name", "no");

            grid.Revalidate();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("3 cells need fixing", grid.ErrorText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Fixing a value clears the mark and the count with it.</summary>
    [AvaloniaFact]
    public void FixingAValueClearsTheMark()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            items[0].Break("Name", "no");
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(Cells(grid), cell => cell.Error is not null);

            items[0].Mend("Name");
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(Cells(grid), cell => cell.Error is not null);
            Assert.Equal(string.Empty, grid.ErrorText);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>An item that reports nothing at all is left alone.</summary>
    [AvaloniaFact]
    public void APlainItemIsLeftAlone()
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Field = "Name",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<object>((_, _) => new TextBlock()),
        });

        grid.ItemsSource = new GridRows(new[] { new { Name = "one" } });

        var window = new Window { Content = grid, Width = 420, Height = 200 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        try
        {
            Assert.DoesNotContain(Cells(grid), cell => cell.Error is not null);
            Assert.Equal(string.Empty, grid.ErrorText);
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<DataGridCell> Cells(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>();

    private static DataGrid Grid(out Window window, out Entry[] items, int rows = 3)
    {
        items = [.. Enumerable.Range(0, rows).Select(number => new Entry($"a{number}", $"b{number}"))];

        var grid = new DataGrid();

        grid.Columns.Add(Column("ID", "Id", entry => entry.Id));
        grid.Columns.Add(Column("NAME", "Name", entry => entry.Name));

        grid.ItemsSource = new GridRows(items);

        window = new Window { Content = grid, Width = 480, Height = 220 };
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
