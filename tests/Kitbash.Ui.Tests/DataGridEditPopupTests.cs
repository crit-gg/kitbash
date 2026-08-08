using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// Editing a cell that opens a popup. A dropdown takes the focus into a window of its own,
/// and without a guard that ends the edit under the open list.
/// </summary>
public sealed class DataGridEditPopupTests
{
    [AvaloniaFact]
    public void ADropdownInAnEditTemplateKeepsTheCellEditing()
    {
        var (window, grid, elsewhere) = Shown();

        try
        {
            var cell = Assert.Single(grid.GetVisualDescendants().OfType<DataGridCell>());

            grid.BeginEdit(cell);
            Dispatcher.UIThread.RunJobs();

            Assert.True(cell.IsEditing);

            var box = Assert.Single(cell.GetVisualDescendants().OfType<ComboBox>());

            // What a click on the chevron does. The list is its own window, so the cell is
            // no longer holding the keyboard focus.
            box.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();

            // Real focus, moved off the cell the way opening the list does.
            elsewhere.Focus();
            Dispatcher.UIThread.RunJobs();

            Assert.True(cell.IsEditing);

            // Picking closes the list, and only then does leaving the cell end the edit.
            box.SelectedIndex = 1;
            box.IsDropDownOpen = false;
            Dispatcher.UIThread.RunJobs();

            box.Focus();
            Dispatcher.UIThread.RunJobs();

            elsewhere.Focus();
            Dispatcher.UIThread.RunJobs();

            Assert.False(cell.IsEditing);
            Assert.Equal("second", ((Row)grid.Rows![0].Item!).Choice);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, DataGrid Grid, Button Elsewhere) Shown()
    {
        var grid = new DataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "Choice",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Row>((row, _) => new TextBlock { Text = row?.Choice }),
            EditTemplate = new FuncDataTemplate<Row>((row, _) =>
            {
                var box = new ComboBox { ItemsSource = new[] { "first", "second" } };

                box.SelectionChanged += (_, _) =>
                {
                    if (row is not null && box.SelectedItem is string picked)
                    {
                        row.Choice = picked;
                    }
                };

                return box;
            }),
        });

        grid.ItemsSource = new GridRows(new[] { new Row { Choice = "first" } });

        var elsewhere = new Button { Content = "Away" };
        var window = new Window
        {
            Content = new StackPanel { Children = { grid, elsewhere } },
            Width = 420,
            Height = 180,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        return (window, grid, elsewhere);
    }

    private sealed class Row : IEditableObject
    {
        public string Choice { get; set; } = string.Empty;

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
