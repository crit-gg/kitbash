using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The tooltip a trimmed label carries. Drawn for real, since whether text was trimmed is
/// an answer only a layout has.
/// </summary>
public class TextTipTests
{
    private static TextBlock Draw(TextBlock label, double width)
    {
        var window = new Window { Width = width, Height = 200, Content = label };

        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        return label;
    }

    [AvaloniaFact]
    public void ATrimmedLabelCarriesItsOwnText()
    {
        const string Words = "A name far longer than the column it was given to sit in";

        var label = Draw(
            new TextBlock
            {
                Text = Words,
                TextTrimming = TextTrimming.CharacterEllipsis,
                [TextTip.ShowsProperty] = true,
            },
            width: 120);

        Assert.Equal(Words, ToolTip.GetTip(label));
    }

    [AvaloniaFact]
    public void ALabelThatFitsCarriesNothing()
    {
        var label = Draw(
            new TextBlock
            {
                Text = "Splice",
                TextTrimming = TextTrimming.CharacterEllipsis,
                [TextTip.ShowsProperty] = true,
            },
            width: 400);

        Assert.Null(ToolTip.GetTip(label));
    }

    /// <summary>
    /// The same wrapped label in the family the app actually draws in. Trimming collapses a
    /// line that runs past its own width and a wrapped line ends at a break under it, so
    /// nothing collapses and the text simply stops. Counting the lines against the text is
    /// what catches that, and the font is what decides which way it goes.
    /// </summary>
    [AvaloniaFact]
    public void WrappedTextIsFoundWhenNoLineCollapsed()
    {
        const string Words =
            "Slices sprite sheets, writes the atlas Godot imports, and reports every frame " +
            "it could not place so a person can fix the source art before the next run.";

        var label = Draw(
            new TextBlock
            {
                Text = Words,
                FontFamily = new FontFamily("avares://Kitbash.Ui/Assets/Fonts#Archivo"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
                [TextTip.ShowsProperty] = true,
            },
            width: 277);

        Assert.DoesNotContain(label.TextLayout.TextLines, line => line.HasCollapsed);
        Assert.Equal(Words, ToolTip.GetTip(label));
    }

    // The compact card's description, wrapped to two lines with the rest dropped.
    [AvaloniaFact]
    public void WrappedTextIsTrimmedAtItsLastLine()
    {
        const string Words =
            "Reads every scene in the workspace, works out what each one depends on, and " +
            "writes the report out beside the project file for the importer to read later";

        var label = Draw(
            new TextBlock
            {
                Text = Words,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
                [TextTip.ShowsProperty] = true,
            },
            width: 200);

        Assert.Equal(Words, ToolTip.GetTip(label));
    }

    [AvaloniaFact]
    public void TextThatGrowsShorterGivesTheTooltipBack()
    {
        var label = Draw(
            new TextBlock
            {
                Text = "A name far longer than the column it was given to sit in",
                TextTrimming = TextTrimming.CharacterEllipsis,
                [TextTip.ShowsProperty] = true,
            },
            width: 120);

        Assert.NotNull(ToolTip.GetTip(label));

        label.Text = "Splice";

        Dispatcher.UIThread.RunJobs();
        label.UpdateLayout();

        Assert.Null(ToolTip.GetTip(label));
    }
}
