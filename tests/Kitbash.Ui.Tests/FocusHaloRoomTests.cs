using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Tests;

/// <summary>
/// A halo is drawn 2px outside the control it rings, so anything above it that clips takes
/// a slice of it. Read off a real form: rows of fields inside an ItemsControl.
/// </summary>
public class FocusHaloRoomTests
{
    /// <summary>
    /// A field stretched across a row is the case, since its halo is the only part of it
    /// standing outside the row it fills.
    /// </summary>
    [AvaloniaFact]
    public void AStretchedFieldKeepsItsWholeHalo()
    {
        var (window, field) = Form();

        try
        {
            Assert.Equal(Halo(window, field), Showing(window, Halo(window, field), field));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A view that wants the clip back writes it, and a local value beats the style, so the
    /// rule can still be turned off where a form is not what is being drawn.
    /// </summary>
    [AvaloniaFact]
    public void AViewCanClipAnyway()
    {
        var (window, field) = Form();

        try
        {
            var rows = window.GetVisualDescendants().OfType<ItemsControl>().First();

            rows.ClipToBounds = true;
            window.UpdateLayout();

            Assert.NotEqual(Halo(window, field), Showing(window, Halo(window, field), field));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A form of three rows, each holding a field that fills the row.</summary>
    private static (Window Window, TextBox Field) Form()
    {
        var rows = new ItemsControl { ItemsSource = new[] { "one", "two", "three" } };

        rows.ItemTemplate = new FuncDataTemplate<string>(
            (_, _) => new TextBox(),
            supportsRecycling: true);

        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = new ScrollViewer { Content = new Border { Padding = new Thickness(16), Child = rows } },
        };

        window.Show();
        window.UpdateLayout();

        var field = window.GetVisualDescendants().OfType<TextBox>().First();

        field.Focus();
        window.UpdateLayout();

        return (window, field);
    }

    /// <summary>The halo's own rectangle in the window's own coordinates.</summary>
    private static Rect Halo(Window window, TextBox field)
    {
        var halo = field.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Name == "PART_Halo");

        return Placed(window, halo);
    }

    /// <summary>What is left of a rectangle once every clipping ancestor has had its say.</summary>
    private static Rect Showing(Window window, Rect rect, Visual visual)
    {
        foreach (var ancestor in visual.GetVisualAncestors())
        {
            if (ancestor.ClipToBounds)
            {
                rect = rect.Intersect(Placed(window, ancestor));
            }
        }

        return rect;
    }

    /// <summary>
    /// A visual's own box in the window's coordinates. Its Bounds are in its parent's space,
    /// so the size is what is transformed rather than the rectangle.
    /// </summary>
    private static Rect Placed(Window window, Visual visual) =>
        new Rect(visual.Bounds.Size).TransformToAABB(visual.TransformToVisual(window)!.Value);
}
