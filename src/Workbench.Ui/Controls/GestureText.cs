using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace Workbench.Ui.Controls;

/// <summary>
/// Writes a <see cref="KeyGesture"/> as the hint beside a menu item.
/// </summary>
/// <remarks>
/// Avalonia's own rendering uses a plus between the parts. This uses a space, because
/// the hint is set in monospace beside other hints and a run of them lines up better
/// without punctuation between every word. It also keeps the copy free of symbols, the
/// way the rest of the app's text is.
/// </remarks>
public class GestureText : IValueConverter
{
    public static readonly GestureText Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is KeyGesture gesture ? Spell(gesture) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A gesture hint is read only.");

    private static string Spell(KeyGesture gesture)
    {
        var parts = new List<string>();

        // Named in the order a person says them, which is also the order every desktop
        // prints them, rather than the order the enum happens to declare.
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control)) { parts.Add("Ctrl"); }
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift)) { parts.Add("Shift"); }
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt)) { parts.Add("Alt"); }
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta)) { parts.Add("Super"); }

        parts.Add(gesture.Key.ToString());

        return string.Join(" ", parts);
    }
}
