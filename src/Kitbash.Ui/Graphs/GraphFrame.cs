using Avalonia;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A labelled box behind a run of nodes. It groups by sitting under them and nothing more,
/// so a node is never owned by one.
/// </summary>
public sealed class GraphFrame : GraphItem
{
    private string _label;
    private Color _colour;

    public GraphFrame(string id, string label, Color colour)
        : base(id)
    {
        _label = label;
        _colour = colour;
    }

    /// <summary>The name on the tab above the box.</summary>
    public override string? Label
    {
        get => _label;
        set => SetLook(ref _label, value ?? string.Empty);
    }

    /// <summary>The tint its edge, its label tab and its wash are all taken from.</summary>
    public Color Colour
    {
        get => _colour;
        set => SetLook(ref _colour, value);
    }

    public override int Layer => 0;

    /// <summary>
    /// The tab above the top left corner. It is given a floor so a frame named with two
    /// letters is still something a person can aim the rename box at.
    /// </summary>
    public override Rect LabelBox(GraphMetrics metrics) =>
        new(
            X,
            Y - metrics.FrameLabelHeight - metrics.FrameLabelGap,
            Math.Max(metrics.FrameLabelWidest, Math.Min(Width, 220)),
            metrics.FrameLabelHeight);
}
