using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The column chooser. A list rather than a grid of boxes, since order matters as much as
/// visibility, and pinned columns are a group at the top rather than a flag on every row.
/// </summary>
public sealed class GridColumnChooserTests
{
    /// <summary>Every column is in the list, in the order the grid has them.</summary>
    [AvaloniaFact]
    public void TheListIsEveryColumnInOrder()
    {
        var chooser = Chooser(out var window, out _);

        try
        {
            Assert.Equal(["ID", "NAME", "KIND"], Labels(chooser));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A change lands as it is made, with no apply button to press.</summary>
    [AvaloniaFact]
    public void TurningARowOffTakesTheColumnOff()
    {
        var chooser = Chooser(out var window, out var columns);

        try
        {
            Row(chooser, "KIND").IsShown = false;
            Dispatcher.UIThread.RunJobs();

            Assert.False(columns[2].IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A column that cannot be turned off sits flat and stays in the list, so the list is
    /// always the whole set of columns.
    /// </summary>
    [AvaloniaFact]
    public void AColumnThatCannotBeHiddenStaysAndSitsFlat()
    {
        var chooser = Chooser(out var window, out var columns);

        try
        {
            columns[0].CanHide = false;
            columns.Gestures = ColumnGestures.Hide;
            chooser.Columns = null;
            chooser.Columns = columns;

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["ID", "NAME", "KIND"], Labels(chooser));
            Assert.False(Row(chooser, "ID").CanHide);
            Assert.True(Row(chooser, "NAME").CanHide);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A pinned column moves into a group of its own at the top.</summary>
    [AvaloniaFact]
    public void PinnedColumnsAreAGroupAtTheTop()
    {
        var chooser = Chooser(out var window, out var columns);

        try
        {
            columns[2].IsPinned = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["KIND", "ID", "NAME"], Labels(chooser));
            Assert.Contains("PINNED", Headings(chooser));
            Assert.Contains("COLUMNS", Headings(chooser));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Reset puts every column back the way the tool declared it.</summary>
    [AvaloniaFact]
    public void ResetPutsTheDeclarationBack()
    {
        var chooser = Chooser(out var window, out var columns);

        try
        {
            Row(chooser, "KIND").IsShown = false;
            columns[1].IsPinned = true;
            Dispatcher.UIThread.RunJobs();

            Assert.NotEmpty(columns.Capture());

            columns.Reset();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(columns.Capture());
            Assert.Equal(["ID", "NAME", "KIND"], Labels(chooser));
        }
        finally
        {
            window.Close();
        }
    }

    private static List<string> Labels(GridColumnChooser chooser) =>
    [
        .. chooser.GetVisualDescendants().OfType<GridColumnChooserRow>().Select(row => row.Label),
    ];

    private static List<string> Headings(GridColumnChooser chooser) =>
    [
        .. chooser.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.Classes.Contains("chooserGroup"))
            .Select(text => text.Text ?? string.Empty),
    ];

    private static GridColumnChooserRow Row(GridColumnChooser chooser, string label) =>
        chooser.GetVisualDescendants().OfType<GridColumnChooserRow>().First(row => row.Label == label);

    private static GridColumnChooser Chooser(out Window window, out GridColumns columns)
    {
        columns = new GridColumns { Gestures = ColumnGestures.Hide | ColumnGestures.Pin };

        foreach (var name in (string[])["ID", "NAME", "KIND"])
        {
            columns.Add(new GridColumn { Key = name.ToLowerInvariant(), Header = name });
        }

        var chooser = new GridColumnChooser { Columns = columns };

        window = new Window { Content = chooser, Width = 320, Height = 320 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return chooser;
    }
}
