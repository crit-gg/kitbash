using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The three cell contents. A keyed theme cannot supply content, and these three are content
/// rather than chrome: a mark beside a name, a bar behind a value, chips with a count after
/// them.
/// </summary>
public sealed class GridCellContentTests
{
    /// <summary>What does not fit is counted rather than wrapped, since a row is one line.</summary>
    [AvaloniaFact]
    public void TagsCountWhatTheyCannotFit()
    {
        var cell = new GridTagsCell { Tags = ["smelting", "tier 2", "review", "bulk"] };

        Assert.Equal(["smelting", "tier 2"], cell.Drawn.Select(tag => tag.Text));
        Assert.Equal("+2", cell.Overflow);
    }

    /// <summary>Everything fitting says nothing at all rather than plus zero.</summary>
    [AvaloniaFact]
    public void NothingLeftOverSaysNothing()
    {
        var cell = new GridTagsCell { Tags = ["smelting"] };

        Assert.Equal(string.Empty, cell.Overflow);
    }

    /// <summary>The first chip leads in the accent, and only the first.</summary>
    [AvaloniaFact]
    public void OnlyTheFirstChipLeads()
    {
        var cell = new GridTagsCell { Tags = ["smelting", "tier 2", "review"] };

        Assert.True(cell.Drawn[0].IsLead);
        Assert.False(cell.Drawn[1].IsLead);
    }

    /// <summary>How many fit is the caller's, and moving it re counts the rest.</summary>
    [AvaloniaFact]
    public void HowManyFitIsTheCallers()
    {
        var cell = new GridTagsCell { Tags = ["a", "b", "c", "d"], Shown = 3 };

        Assert.Equal(3, cell.Drawn.Count);
        Assert.Equal("+1", cell.Overflow);
    }

    /// <summary>A tag that is nothing but space is not a tag.</summary>
    [AvaloniaFact]
    public void BlankTagsAreNotCounted()
    {
        var cell = new GridTagsCell { Tags = ["smelting", "  ", "tier 2"] };

        Assert.Equal(["smelting", "tier 2"], cell.Drawn.Select(tag => tag.Text));
        Assert.Equal(string.Empty, cell.Overflow);
    }

    /// <summary>A ratio outside its range is clamped rather than drawn past the cell.</summary>
    [AvaloniaTheory]
    [InlineData(0.64, 0.64)]
    [InlineData(-1, 0)]
    [InlineData(2.5, 1)]
    [InlineData(0, 0)]
    public void ARatioIsClamped(double value, double reach)
    {
        var cell = new GridRatioCell { Value = value };

        Assert.Equal(reach, cell.Reach);
    }

    /// <summary>
    /// A ratio nobody has worked out is not a ratio of zero, so it draws no bar rather than
    /// an empty one.
    /// </summary>
    [AvaloniaFact]
    public void ARatioThatIsNotANumberDrawsNothing()
    {
        var cell = new GridRatioCell { Value = double.NaN };

        Assert.Equal(0, cell.Reach);
    }

    /// <summary>The words on a ratio are the caller's, since a fraction has many readings.</summary>
    [AvaloniaFact]
    public void TheWordsOnARatioAreTheCallers()
    {
        var cell = Draw(new GridRatioCell { Value = 0.64, Text = "64%" }, out var window);

        try
        {
            Assert.Contains(
                cell.GetVisualDescendants().OfType<TextBlock>(),
                label => label.Text == "64%");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A reference with no kind draws no mark, only the name.</summary>
    [AvaloniaFact]
    public void AReferenceWithNoKindDrawsNoMark()
    {
        var cell = Draw(new GridRefCell { Text = "MCH_ArcFurnace" }, out var window);

        try
        {
            var mark = cell.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "PART_Mark");

            Assert.False(mark.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>And one with a kind draws it in that kind's colour.</summary>
    [AvaloniaFact]
    public void AReferenceWithAKindDrawsIt()
    {
        var window = new Window();
        var data = window.FindResource("Data");
        var cell = Draw(new GridRefCell { Text = "MCH_ArcFurnace", Mark = data as Avalonia.Media.IBrush }, out var host);

        window.Close();

        try
        {
            var mark = cell.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "PART_Mark");

            Assert.True(mark.IsVisible);
            Assert.Same(data, mark.Background);
        }
        finally
        {
            host.Close();
        }
    }

    private static T Draw<T>(T control, out Window window)
        where T : Control
    {
        window = new Window { Content = control, Width = 300, Height = 120 };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return control;
    }
}
