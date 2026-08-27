using Avalonia;
using Avalonia.Controls;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Which axis of a vector a label names. A style tints it, so a view says which axis it is
/// drawing and never which colour that is.
/// </summary>
public class Axis : AvaloniaObject
{
    /// <summary>Nothing, which is what a label that names no axis carries.</summary>
    public const int None = -1;

    /// <summary>The axis, from zero. Anything past the fourth is left untinted.</summary>
    public static readonly AttachedProperty<int> IndexProperty =
        AvaloniaProperty.RegisterAttached<Axis, TextBlock, int>("Index", None);

    public static void SetIndex(TextBlock label, int value) => label.SetValue(IndexProperty, value);

    public static int GetIndex(TextBlock label) => label.GetValue(IndexProperty);
}
