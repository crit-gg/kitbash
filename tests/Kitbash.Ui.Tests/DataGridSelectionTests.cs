using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a grid hands back when a row is picked. The rows a grid holds are wrappers, so
/// the data has to be published separately or every caller unwraps by hand.
/// </summary>
public sealed class DataGridSelectionTests
{
    /// <summary>
    /// The trap this exists to stop somebody walking into again. A grid is a ListBox over
    /// GridRow, so the inherited property is the wrapper and binding it to a typed
    /// property is a conversion that never lands.
    /// </summary>
    [AvaloniaFact]
    public void TheInheritedSelectedItemIsTheWrapper()
    {
        var (grid, _) = Shown();

        grid.SelectedIndex = 1;

        Assert.IsType<GridRow>(grid.SelectedItem);
    }

    /// <summary>A picked row publishes the thing the caller put in, not the wrapper.</summary>
    [AvaloniaFact]
    public void ATypedSelectionBindingLands()
    {
        var (grid, items) = Shown();
        var model = new Picker();

        grid.Bind(
            DataGrid.SelectedValueProperty,
            new Binding(nameof(Picker.Chosen)) { Source = model, Mode = BindingMode.TwoWay });

        grid.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(items[1], model.Chosen);
    }

    /// <summary>
    /// And it carries the other way, so a view model that picks a row moves the grid. Add
    /// then select is what the settings list editors do, and it is the half that used to
    /// clear itself.
    /// </summary>
    [AvaloniaFact]
    public void PickingFromTheModelMovesTheGrid()
    {
        var (grid, items) = Shown();
        var model = new Picker();

        grid.Bind(
            DataGrid.SelectedValueProperty,
            new Binding(nameof(Picker.Chosen)) { Source = model, Mode = BindingMode.TwoWay });

        model.Chosen = items[2];
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, grid.SelectedIndex);
        Assert.Same(items[2], model.Chosen);
    }

    /// <summary>
    /// The tree grid says the same thing, which it gets from the tree it is built on. A
    /// default overridden on a base type has to reach the derived one or the two grids
    /// disagree about what a picked row is.
    /// </summary>
    [AvaloniaFact]
    public void TheTreeGridUnwrapsTheSameWay()
    {
        var roots = new[] { new Entry("root"), new Entry("second root") };
        var grid = new TreeDataGrid();

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        });

        grid.ItemsSource = new TreeRows(roots, _ => null);

        var window = new Window { Content = grid, Width = 420, Height = 220 };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        try
        {
            grid.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.IsType<TreeRow>(grid.SelectedItem);
            Assert.Same(roots[1], grid.SelectedValue);
        }
        finally
        {
            window.Close();
        }
    }

    private static (DataGrid Grid, IReadOnlyList<Entry> Items) Shown()
    {
        var items = new[] { new Entry("first"), new Entry("second"), new Entry("third") };
        var grid = new DataGrid { SelectionMode = SelectionMode.Multiple };

        grid.Columns.Add(new GridColumn
        {
            Header = "NAME",
            Width = new GridLength(1, GridUnitType.Star),
            CellTemplate = new FuncDataTemplate<Entry>((entry, _) => new TextBlock { Text = entry?.Name }),
        });

        grid.ItemsSource = new GridRows(items);

        var window = new Window { Content = grid, Width = 420, Height = 220 };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        return (grid, items);
    }

    private sealed record Entry(string Name);

    /// <summary>
    /// A view model holding a picked row the way the settings list editors do, typed to
    /// the thing the grid was given rather than to a wrapper.
    /// </summary>
    private sealed class Picker : INotifyPropertyChanged
    {
        private Entry? chosen;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Entry? Chosen
        {
            get => chosen;
            set
            {
                chosen = value;
                Raise();
            }
        }

        private void Raise([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
