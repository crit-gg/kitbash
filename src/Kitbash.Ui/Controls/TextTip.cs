using Avalonia;
using Avalonia.Controls;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Puts a label's own text in its tooltip while the text is trimmed, and takes the tooltip
/// away again while it fits, so a pointer resting on a label never repeats what is on screen.
/// </summary>
public class TextTip : AvaloniaObject
{
    /// <summary>
    /// Whether the label carries the tooltip. Only a label that can trim needs it, since
    /// text that wraps and grows is never hidden.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowsProperty =
        AvaloniaProperty.RegisterAttached<TextTip, TextBlock, bool>("Shows");

    static TextTip()
    {
        ShowsProperty.Changed.AddClassHandler<TextBlock, bool>(OnShowsChanged);
    }

    public static void SetShows(TextBlock label, bool value) => label.SetValue(ShowsProperty, value);

    public static bool GetShows(TextBlock label) => label.GetValue(ShowsProperty);

    private static void OnShowsChanged(TextBlock label, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        label.LayoutUpdated -= OnLayoutUpdated;

        if (change.GetNewValue<bool>())
        {
            label.LayoutUpdated += OnLayoutUpdated;
            return;
        }

        ToolTip.SetTip(label, null);
    }

    /// <summary>
    /// Read after the pass rather than on a text or size change, since either one is
    /// answered by a layout that has not run yet.
    /// </summary>
    private static void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not TextBlock label)
        {
            return;
        }

        var tip = IsTrimmed(label) ? label.Text : null;

        // Written only on a change, because a tooltip that is replaced while it is open
        // closes itself.
        if (!Equals(ToolTip.GetTip(label), tip))
        {
            ToolTip.SetTip(label, tip);
        }
    }

    /// <summary>
    /// Asks the lines the label drew. HasCollapsed is what puts the ellipsis there, so this
    /// is the same answer rather than a width worked out again here.
    /// </summary>
    private static bool IsTrimmed(TextBlock label)
    {
        foreach (var line in label.TextLayout.TextLines)
        {
            if (line.HasCollapsed)
            {
                return true;
            }
        }

        return false;
    }
}
