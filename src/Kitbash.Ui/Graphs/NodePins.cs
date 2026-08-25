using Avalonia.Controls;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A node's pins, over whatever fills its body. A visual child draws after its parent's
/// <c>Render</c>, so a card holding a template would paint over the pins on the very edge
/// that template fills. A card with nothing in its body draws its own and never builds one
/// of these.
/// </summary>
public sealed class NodePins(NodeCard card) : Control
{
    public override void Render(DrawingContext context) => card.DrawPins(context);
}
