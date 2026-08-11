using Kitbash.Core.Platform;

namespace Kitbash.Gallery.Views.Pages;

/// <summary>Everything that takes a value, from a text field to a date.</summary>
public partial class InputsPage : GalleryPage
{
    public InputsPage(IScreenColour screen, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        InitializeComponent();

        // The colour page holds the pickers this field opens, so both pages keep one list.
        Tint.ScreenColour = screen;
        Tint.Swatches = palette.Swatches;
        Tint.Recent = palette.Recent;
    }
}
