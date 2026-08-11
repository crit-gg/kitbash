using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// What a grid does with a value too wide for its column. Trimming is one thing and saying
/// what was trimmed is another, and the second one has to lose to an error on the same cell.
/// </summary>
public sealed class GridTrimTests
{
    private const string Long = "a name far too long for the column it is drawn in";
    private const string Title = "A COLUMN TITLE NOBODY SHORTENED";

    /// <summary>Text in a cell trims rather than clipping mid glyph, whatever a view wrote.</summary>
    [AvaloniaFact]
    public void ACellTrimsWithoutBeingAsked()
    {
        var grid = Grid(out var window, out _);

        try
        {
            Assert.All(Labels(grid), label =>
                Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>And what was trimmed is in the tooltip, so nothing is only half readable.</summary>
    [AvaloniaFact]
    public void WhatWasTrimmedIsInTheTooltip()
    {
        var grid = Grid(out var window, out _);

        try
        {
            var wide = Labels(grid).First(label => label.Text == Long);

            Assert.Equal(Long, ToolTip.GetTip(wide));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A value that fits carries no tooltip, since it would repeat what is on screen.</summary>
    [AvaloniaFact]
    public void AValueThatFitsCarriesNothing()
    {
        var grid = Grid(out var window, out _);

        try
        {
            var narrow = Labels(grid).First(label => label.Text == "a0");

            Assert.Null(ToolTip.GetTip(narrow));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Error beats trimmed. The cell carries the error's tooltip, so the text inside it
    /// gives its own up rather than winning by being nearer the pointer.
    /// </summary>
    [AvaloniaFact]
    public void ErrorBeatsTrimmed()
    {
        var grid = Grid(out var window, out var items);

        try
        {
            var cell = Cells(grid).First(cell => cell.Column?.Header?.ToString() == "NAME");
            var wide = cell.GetVisualDescendants().OfType<TextBlock>().First();

            Assert.Equal(Long, ToolTip.GetTip(wide));

            items[0].Break("Name", "A name cannot be that long.");
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("A name cannot be that long.", ToolTip.GetTip(cell));
            Assert.Null(ToolTip.GetTip(wide));

            // And the trimmed one comes back once there is nothing wrong to say.
            items[0].Mend("Name");
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Null(ToolTip.GetTip(cell));
            Assert.Equal(Long, ToolTip.GetTip(wide));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A title wider than its column is handed infinite width by the row of furniture it
    /// sits in, so it never collapses and the cell clips it instead. Pinned here because the
    /// trimming is set and does nothing, which is worth finding on purpose rather than by
    /// wondering why a title has no ellipsis.
    /// </summary>
    [AvaloniaFact]
    public void ATitleIsNotMeasuredAgainstItsColumn()
    {
        var grid = Grid(out var window, out _);

        try
        {
            var title = grid.GetVisualDescendants()
                .OfType<DataGridHeaderCell>()
                .SelectMany(cell => cell.GetVisualDescendants().OfType<TextBlock>())
                .First(label => label.Text is Title);

            Assert.Equal(TextTrimming.CharacterEllipsis, title.TextTrimming);
            Assert.True(
                title.Bounds.Width > title.FindAncestorOfType<DataGridHeaderCell>()!.Bounds.Width,
                "the title is arranged wider than the cell holding it");
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<TextBlock> Labels(DataGrid grid) =>
        Cells(grid).SelectMany(cell => cell.GetVisualDescendants().OfType<TextBlock>());

    private static IEnumerable<DataGridCell> Cells(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridCell>();

    private static DataGrid Grid(out Window window, out Entry[] items)
    {
        items = [.. Enumerable.Range(0, 3).Select(number => new Entry($"a{number}", Long))];

        var grid = new DataGrid();

        grid.Columns.Add(Column("ID", "Id", entry => entry.Id));
        grid.Columns.Add(Column("NAME", "Name", entry => entry.Name));
        grid.Columns.Add(Column(Title, "Id", entry => entry.Id));

        grid.ItemsSource = new GridRows(items);

        window = new Window { Content = grid, Width = 260, Height = 220 };
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
    private sealed class Entry(string id, string name) : System.ComponentModel.INotifyDataErrorInfo
    {
        private readonly Dictionary<string, string> wrong = [];

        public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs>? ErrorsChanged;

        public string Id => id;

        public string Name => name;

        public bool HasErrors => wrong.Count > 0;

        public void Break(string field, string why)
        {
            wrong[field] = why;
            ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(field));
        }

        public void Mend(string field)
        {
            wrong.Remove(field);
            ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(field));
        }

        public System.Collections.IEnumerable GetErrors(string? propertyName) =>
            propertyName is not null && wrong.TryGetValue(propertyName, out var why) ? new[] { why } : [];
    }
}
