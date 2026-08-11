using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The modified mark, which stage 11 dropped and the data grid design puts back. The grid
/// cannot know what unsaved means, so a tool says, and the row reads it off the item.
/// </summary>
public sealed class GridModifiedMarkTests
{
    /// <summary>A row the tool calls modified wears the mark and the others do not.</summary>
    [AvaloniaFact]
    public void OnlyTheModifiedRowsAreMarked()
    {
        var items = Items();
        var grid = Grid(items, out var window);

        try
        {
            grid.RowModified = item => ((Entry)item).IsModified;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal([false, false, false], Marks(grid));

            items[1].IsModified = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal([false, true, false], Marks(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Saying nothing is modified marks nothing, which is the default.</summary>
    [AvaloniaFact]
    public void NoPredicateMarksNothing()
    {
        var items = Items();
        var grid = Grid(items, out var window);

        try
        {
            items[0].IsModified = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal([false, false, false], Marks(grid));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Saving clears the mark on the same pass that clears the flag.</summary>
    [AvaloniaFact]
    public void SavingClearsTheMark()
    {
        var items = Items();
        var grid = Grid(items, out var window);

        try
        {
            grid.RowModified = item => ((Entry)item).IsModified;
            items[0].IsModified = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal([true, false, false], Marks(grid));

            items[0].IsModified = false;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal([false, false, false], Marks(grid));
        }
        finally
        {
            window.Close();
        }
    }

    private static bool[] Marks(DataGrid grid) =>
    [
        .. grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .OrderBy(row => row.Row?.Item is Entry entry ? entry.Name : string.Empty)
            .Select(row => row.Classes.Contains(":modified")),
    ];

    private static Entry[] Items() => [new Entry("a"), new Entry("b"), new Entry("c")];

    private static DataGrid Grid(Entry[] items, out Window window)
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        });

        grid.ItemsSource = new GridRows(items);

        window = new Window { Content = grid, Width = 420, Height = 300 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    private sealed class Entry(string name) : INotifyPropertyChanged
    {
        private bool modified;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name => name;

        public bool IsModified
        {
            get => modified;
            set
            {
                if (modified == value)
                {
                    return;
                }

                modified = value;
                Raise();
            }
        }

        private void Raise([CallerMemberName] string? property = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
