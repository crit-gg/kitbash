using Avalonia.Media;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views.Textures;

/// <summary>One parameter the panel draws a slider for.</summary>
public sealed record TextureKnob(string Label, string Key, double Least, double Most, bool Whole = false, string Tip = "");

/// <summary>
/// What the app knows about a node that the graph does not. It hangs off
/// <see cref="GraphItem.Tag"/>, which is the seam the library leaves for exactly this: the
/// canvas carries the box, the ports and the wires, and what a node means is the app's.
/// </summary>
public sealed class TextureNode(TextureOp op)
{
    public TextureOp Op { get; } = op;

    /// <summary>Every parameter by name, which is what the operators are handed.</summary>
    public Dictionary<string, double> Values { get; } = [];

    /// <summary>What the panel offers. A node with none is read only.</summary>
    public List<TextureKnob> Knobs { get; } = [];

    /// <summary>The one colour a gradient map takes.</summary>
    public Color Paint { get; set; } = Color.Parse("#3d5a80");

    /// <summary>The material slot an output writes, and the format it writes it in.</summary>
    public string Slot { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    /// <summary>Words for the strip under the body, beside the cook time.</summary>
    public string Note { get; set; } = string.Empty;

    public double Value(string key, double fallback = 0) => Values.GetValueOrDefault(key, fallback);

    public TextureNode With(string key, double value)
    {
        Values[key] = value;
        return this;
    }

    public TextureNode Knob(string label, string key, double least, double most, bool whole = false, string tip = "")
    {
        Knobs.Add(new TextureKnob(label, key, least, most, whole, tip));
        return this;
    }
}
