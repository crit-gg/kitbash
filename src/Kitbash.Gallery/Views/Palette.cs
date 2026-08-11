using System.Collections.ObjectModel;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views;

/// <summary>
/// The saved colours the field on the inputs page and the pickers on the colour page share.
/// Both start empty, since the palette is not the picker's to invent, and an application
/// keeps its own through SwatchAdded and SwatchRemoved.
/// </summary>
public sealed class Palette
{
    public ObservableCollection<ColorValue> Swatches { get; } = [];

    public ObservableCollection<ColorValue> Recent { get; } = [];
}
