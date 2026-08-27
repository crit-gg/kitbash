using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The ramp well. It draws points as a line and stops as a gradient, and its editor opens
/// over the field rather than in a window.
/// </summary>
public class CurveFieldTests
{
    [AvaloniaFact]
    public void PointsDrawALineAcrossTheWell()
    {
        var (window, field) = Field(f => f.Points = [new Point(0, 0), new Point(0.5, 1), new Point(1, 0)]);

        try
        {
            var trace = Assert.IsType<StreamGeometry>(field.Trace);

            // The run spans the well, and the top of it is not on the edge.
            Assert.Equal(0, trace.Bounds.Left, 1);
            Assert.Equal(field.Bounds.Width, trace.Bounds.Right, 1);
            Assert.True(trace.Bounds.Top > 0, "the run touched the top of the well");
            Assert.Null(field.Wash);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A run of fewer than two points is not a run, so there is nothing to draw.</summary>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    public void ARunTooShortToBeALineDrawsNothing(int held)
    {
        var (window, field) = Field(f => f.Points = [.. Enumerable.Range(0, held).Select(at => new Point(at, at))]);

        try
        {
            Assert.Null(field.Trace);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Stops are a colour ramp, and they are what is drawn when there are any.</summary>
    [AvaloniaFact]
    public void StopsDrawAGradientAndTakeOverFromThePoints()
    {
        var (window, field) = Field(f =>
        {
            f.Points = [new Point(0, 0), new Point(1, 1)];
            f.Stops =
            [
                new GradientStop(Colors.Black, 0),
                new GradientStop(Colors.White, 1),
            ];
        });

        try
        {
            var wash = Assert.IsType<LinearGradientBrush>(field.Wash);

            Assert.Equal(2, wash.GradientStops.Count);
            Assert.Null(field.Trace);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The line reaches the template, since a geometry the theme never draws is a curve
    /// nobody sees.
    /// </summary>
    [AvaloniaFact]
    public void TheLineReachesTheWellItIsDrawnIn()
    {
        var (window, field) = Field(f => f.Points = [new Point(0, 0), new Point(1, 1)]);

        try
        {
            var path = field.GetVisualDescendants()
                .OfType<Avalonia.Controls.Shapes.Path>()
                .Single(part => part.Name == "PART_Trace");

            Assert.Same(field.Trace, path.Data);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The editor is a popover over the field, never a window of its own.</summary>
    [AvaloniaFact]
    public void TheEditorOpensOverTheField()
    {
        var (window, field) = Field(f => f.Editor = new TextBlock { Text = "rows" });

        try
        {
            var flyout = Assert.IsType<Flyout>(field.Flyout);

            Assert.Equal(PlacementMode.BottomEdgeAlignedLeft, flyout.Placement);
            Assert.Contains("popover", flyout.FlyoutPresenterClasses);

            var popover = Assert.IsType<Popover>(flyout.Content);

            Assert.Equal("rows", Assert.IsType<TextBlock>(popover.Content).Text);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, CurveField Field) Field(Action<CurveField> set)
    {
        var field = new CurveField { Width = 120 };

        set(field);

        var window = new Window { Width = 300, Height = 200, Content = field };

        window.Show();
        window.UpdateLayout();

        return (window, field);
    }
}
