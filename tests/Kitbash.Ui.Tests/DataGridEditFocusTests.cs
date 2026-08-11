using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What ends an edit. The rule is where the focus went, not what opened, because a list of
/// the things allowed to take the focus can never be complete: a picker, a dialog and a
/// dropdown all take it into a window of their own.
/// </summary>
public sealed class DataGridEditFocusTests
{
    /// <summary>Focus landing elsewhere in the same window is a person leaving the cell.</summary>
    [AvaloniaFact]
    public void FocusMovingInsideTheWindowEndsTheEdit()
    {
        var (window, grid, cell, elsewhere) = Editing();

        try
        {
            elsewhere.Focus();
            Dispatcher.UIThread.RunJobs();

            Assert.False(cell.IsEditing);
            Assert.Null(grid.EditingCell);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Focus leaving for another window is a dialog, and a person who opened one has not
    /// finished with the cell. This is the shape a file picker takes.
    /// </summary>
    [AvaloniaFact]
    public void FocusLeavingForAnotherWindowKeepsTheEdit()
    {
        var (window, _, cell, _) = Editing();
        var over = new Window { Width = 200, Height = 120 };
        var inside = new Button { Content = "In the dialog" };

        over.Content = inside;
        over.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            inside.Focus();
            Dispatcher.UIThread.RunJobs();

            Assert.True(cell.IsEditing);
        }
        finally
        {
            over.Close();
            window.Close();
        }
    }

    /// <summary>
    /// And focus going nowhere at all is the app losing it, which a native picker does
    /// without ever building a window Avalonia can see.
    /// </summary>
    [AvaloniaFact]
    public void FocusGoingNowhereKeepsTheEdit()
    {
        var (window, _, cell, _) = Editing();

        try
        {
            window.FocusManager?.Focus(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(cell.IsEditing);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, DataGrid Grid, DataGridCell Cell, Button Elsewhere) Editing()
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Row>((row, _) => new TextBlock { Text = row?.Name }),
            EditTemplate = new FuncDataTemplate<Row>((row, _) => new TextBox { Text = row?.Name }),
        });

        grid.ItemsSource = new GridRows(new[] { new Row { Name = "only" } });

        var elsewhere = new Button { Content = "Away" };
        var window = new Window
        {
            Content = new StackPanel { Children = { grid, elsewhere } },
            Width = 420,
            Height = 200,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var cell = Assert.Single(grid.GetVisualDescendants().OfType<DataGridCell>());

        grid.BeginEdit(cell);
        Dispatcher.UIThread.RunJobs();

        Assert.True(cell.IsEditing);

        return (window, grid, cell, elsewhere);
    }

    private sealed class Row : IEditableObject
    {
        public string Name { get; set; } = string.Empty;

        public void BeginEdit()
        {
        }

        public void CancelEdit()
        {
        }

        public void EndEdit()
        {
        }
    }
}
