namespace Kitbash.Ui.Controls;

/// <summary>
/// How much of a node is worth drawing at the zoom in force. Text is the expensive part of
/// any graph at scale, so it is the first thing dropped and the boxes are the last.
/// </summary>
public enum GraphLod
{
    /// <summary>Names, values and badges. The look both designs draw.</summary>
    Full,

    /// <summary>The header and the pins. No port names and no inline values.</summary>
    Reduced,

    /// <summary>A filled box per node and nothing else. No containers are realised at all.</summary>
    Block,
}
