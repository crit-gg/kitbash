using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Kitbash.Gallery.Views;

/// <summary>
/// One page of the gallery, opened by the rail. Every page pins its held samples when it
/// loads, so no page has to say so.
/// </summary>
public class GalleryPage : UserControl
{
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        HoldStates();
    }

    /// <summary>
    /// Pins the sample controls to one state so the five can be read side by side. Runs
    /// again every time the page comes back, which sets the same classes to the same values.
    /// </summary>
    private void HoldStates()
    {
        foreach (var control in this.GetVisualDescendants().OfType<Control>())
        {
            var hover = control.Classes.Contains("forceHover");
            var pressed = control.Classes.Contains("forcePressed");
            var focus = control.Classes.Contains("forceFocus");

            if (!hover && !pressed && !focus)
            {
                continue;
            }

            var pseudo = (IPseudoClasses)control.Classes;

            // A press is always also a hover, so both are set and the theme must order
            // pressed after hover.
            pseudo.Set(":pointerover", hover || pressed);
            pseudo.Set(":pressed", pressed);
            pseudo.Set(":focus-visible", focus);

            control.IsHitTestVisible = false;
        }
    }
}
