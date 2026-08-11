using Avalonia.Controls;
using Kitbash.Core.Platform;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>The colour picker in both of its hosts.</summary>
public partial class ColourPage : GalleryPage
{
    public ColourPage(IScreenColour screen, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        InitializeComponent();

        foreach (var picker in new[] { Bench, Floating })
        {
            picker.ScreenColour = screen;
            picker.Swatches = palette.Swatches;
            picker.Recent = palette.Recent;
        }

        // A picker written into a flyout by hand closes its own host, which is the one thing
        // ui:ColorField does for you.
        Floating.Applied += (_, _) => (Floater.Flyout as Flyout)?.Hide();
        Floating.Cancelled += (_, _) => (Floater.Flyout as Flyout)?.Hide();
    }
}
