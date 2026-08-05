using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// A diff read as lines. Built on the list that already virtualises, so a file with tens of
/// thousands of changed lines costs the rows on screen and nothing more.
/// </summary>
public class TextDiff : ListBox
{
    /// <summary>
    /// What a changed word sits on. Inherited, so the theme sets one per line kind and a row
    /// reads whichever it landed under.
    /// </summary>
    public static readonly AttachedProperty<IBrush?> HighlightProperty =
        AvaloniaProperty.RegisterAttached<TextDiff, Control, IBrush?>("Highlight", inherits: true);

    /// <inheritdoc cref="ITextDiffColouring"/>
    public static readonly StyledProperty<ITextDiffColouring?> ColouringProperty =
        AvaloniaProperty.Register<TextDiff, ITextDiffColouring?>(nameof(Colouring));

    /// <summary>The file the lines came from, which is what a colouriser picks a grammar by.</summary>
    public static readonly StyledProperty<string> PathProperty =
        AvaloniaProperty.Register<TextDiff, string>(nameof(Path), "");

    /// <inheritdoc cref="ColouringProperty"/>
    public ITextDiffColouring? Colouring
    {
        get => GetValue(ColouringProperty);
        set => SetValue(ColouringProperty, value);
    }

    /// <inheritdoc cref="PathProperty"/>
    public string Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public static IBrush? GetHighlight(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(HighlightProperty);
    }

    public static void SetHighlight(Control control, IBrush? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(HighlightProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextDiff);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<TextDiffRow>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new TextDiffRow();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);

        if (container is TextDiffRow row)
        {
            row.Follow(item as TextDiffLine, Colouring, Path);
        }
    }

    /// <summary>
    /// A container kept but moved. Everything it was told is read again from whatever line
    /// now sits at its index, since this is the one place a container is reused without
    /// being cleared first.
    /// </summary>
    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);

        if (container is TextDiffRow row && ItemsView.Count > newIndex)
        {
            row.Follow(ItemsView[newIndex] as TextDiffLine, Colouring, Path);
        }
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);

        if (container is TextDiffRow row)
        {
            row.Follow(null, null, "");
        }
    }
}
