using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The two level edit. A cell was the only unit before this, so a rule that only holds once
/// three fields agree could not be written and a whole row could not be put back.
/// </summary>
public sealed class GridEditUnitTests
{
    /// <summary>A cell unit tells the item once per cell, which is what it always did.</summary>
    [AvaloniaFact]
    public void ACellUnitOpensAndClosesPerCell()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.F2);

            Assert.Equal(1, items[0].Begun);
            Assert.Equal(0, items[0].Ended);

            grid.CommitEdit();

            Assert.Equal(1, items[0].Ended);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A row unit tells the item once for the row, whatever the cells do.</summary>
    [AvaloniaFact]
    public void ARowUnitOpensOnceForTheWholeRow()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.EditUnit = GridEditUnit.Row;
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.F2);
            grid.CommitEdit();

            Assert.Equal(1, items[0].Begun);
            Assert.Equal(0, items[0].Ended);

            // A second cell in the same row joins the transaction already open.
            Press(grid, Key.Right);
            Press(grid, Key.F2);
            grid.CommitEdit();

            Assert.Equal(1, items[0].Begun);
            Assert.Equal(0, items[0].Ended);
            Assert.True(grid.EditingCell is null);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The transaction closes when the edit leaves the row.</summary>
    [AvaloniaFact]
    public void LeavingTheRowClosesTheTransaction()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.EditUnit = GridEditUnit.Row;
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.F2);
            grid.CommitEdit();

            Assert.Equal(0, items[0].Ended);

            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, items[0].Ended);
            Assert.Equal(0, items[0].Cancelled);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Escape under a row unit puts every field back rather than one.</summary>
    [AvaloniaFact]
    public void EscapeCancelsTheWholeRow()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            grid.EditUnit = GridEditUnit.Row;
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.F2);

            var editor = Editor(grid);

            editor.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
                Source = editor,
            });

            Assert.Equal(1, items[0].Cancelled);
            Assert.Equal(0, items[0].Ended);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Enter keeps what was typed and steps down a row in the same column.</summary>
    [AvaloniaFact]
    public void EnterCommitsAndStepsDown()
    {
        var grid = Grid(out var window, out _);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.F2);

            var editor = Editor(grid);

            editor.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Enter,
                Source = editor,
            });

            Dispatcher.UIThread.RunJobs();

            Assert.Null(grid.EditingCell);
            Assert.Equal(1, grid.SelectedIndex);
            Assert.Equal("NAME", Current(grid)?.Column?.Header);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>An editor that takes a return keeps the key.</summary>
    [AvaloniaFact]
    public void AMultiLineEditorKeepsEnter()
    {
        var grid = Grid(out var window, out _, multiline: true);

        try
        {
            grid.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Press(grid, Key.F2);

            var cell = grid.EditingCell!;
            var editor = Editor(grid);

            editor.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Enter,
                Source = editor,
            });

            Dispatcher.UIThread.RunJobs();

            Assert.Same(cell, grid.EditingCell);
            Assert.Equal(0, grid.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The field inside the cell being edited, which is what a person types into.</summary>
    private static TextBox Editor(DataGrid grid) =>
        grid.EditingCell!.GetVisualDescendants().OfType<TextBox>().First();

    private static DataGridCell? Current(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>().FirstOrDefault(cell => cell.IsCurrent);

    private static void Press(Control target, Key key)
    {
        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            Source = target,
        });

        Dispatcher.UIThread.RunJobs();
    }

    private static DataGrid Grid(out Window window, out Entry[] items, bool multiline = false)
    {
        items = [new Entry("first"), new Entry("second"), new Entry("third")];

        var grid = new DataGrid();

        var name = new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
            EditTemplate = new FuncDataTemplate<Entry>((entry, _) =>
                new TextBox { Text = entry?.Name, AcceptsReturn = multiline }),
        };

        var kind = new GridColumn
        {
            Header = "KIND",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
            EditTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBox { Text = entry?.Name }),
        };

        grid.Columns.Add(name);
        grid.Columns.Add(kind);
        grid.ItemsSource = new GridRows(items);

        window = new Window { Content = grid, Width = 480, Height = 300 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return grid;
    }

    /// <summary>An item that counts what the grid tells it.</summary>
    private sealed class Entry(string name) : IEditableObject
    {
        public string Name => name;

        public int Begun { get; private set; }

        public int Ended { get; private set; }

        public int Cancelled { get; private set; }

        public void BeginEdit() => Begun++;

        public void EndEdit() => Ended++;

        public void CancelEdit() => Cancelled++;
    }
}
