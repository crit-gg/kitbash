using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>A note on the canvas, drawn. Prose wraps to the note's own width.</summary>
public sealed class GraphNoteCard : Control
{
    private const double Pad = 10;
    private const double Gap = 6;

    internal NodeGraph? Graph { get; private set; }

    internal GraphNote? Note { get; private set; }

    internal void Follow(NodeGraph graph, GraphNote note)
    {
        Graph = graph;
        Note = note;
        ZIndex = note.Layer;
        InvalidateVisual();
    }

    internal void Release()
    {
        Graph = null;
        Note = null;
    }

    public override void Render(DrawingContext context)
    {
        if (Graph is not { } graph || Note is not { } note)
        {
            return;
        }

        var colours = graph.Colours;
        var box = new Rect(Bounds.Size);

        context.DrawRectangle(
            colours.Header,
            note.IsSelected ? colours.NodeEdgePicked : colours.NodeEdge,
            new RoundedRect(box, graph.Metrics.NodeRadius));

        if (graph.View.Lod != GraphLod.Full)
        {
            return;
        }

        var at = Pad;

        if (note.Author is { Length: > 0 } author)
        {
            var by = graph.Text.Get(author, GraphTextRole.Label, colours.Muted);

            context.DrawText(by, new Point(Pad, at));
            at += by.Height + Gap;
        }

        var text = graph.Text.Get(note.Text, GraphTextRole.Label, colours.Title, Math.Max(1, box.Width - Pad * 2), wrap: true);

        context.DrawText(text, new Point(Pad, at));
    }
}
