using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Dims the window behind a flyout while it is open. Attach it to the flyout:
/// <c>&lt;Flyout ui:Dimmer.Dims="True"&gt;</c>.
/// </summary>
public class Dimmer
{
    public static readonly AttachedProperty<bool> DimsProperty =
        AvaloniaProperty.RegisterAttached<Dimmer, FlyoutBase, bool>("Dims");

    // One scrim per flyout, so two open at once cannot leave one behind.
    private static readonly Dictionary<FlyoutBase, Border> Showing = [];

    static Dimmer()
    {
        DimsProperty.Changed.AddClassHandler<FlyoutBase, bool>(OnDimsChanged);
    }

    public static bool GetDims(FlyoutBase flyout) => flyout.GetValue(DimsProperty);

    public static void SetDims(FlyoutBase flyout, bool value) => flyout.SetValue(DimsProperty, value);

    private static void OnDimsChanged(FlyoutBase flyout, AvaloniaPropertyChangedEventArgs<bool> change)
    {
        flyout.Opened -= OnOpened;
        flyout.Closed -= OnClosed;

        if (!change.GetNewValue<bool>())
        {
            Remove(flyout);
            return;
        }

        flyout.Opened += OnOpened;
        flyout.Closed += OnClosed;
    }

    private static void OnOpened(object? sender, EventArgs e)
    {
        if (sender is not FlyoutBase flyout || flyout.Target is not { } target)
        {
            return;
        }

        // The layer is created on demand, so a window that has never floated anything
        // has none until now. A window whose template has no named layer manager has
        // none at all, and the scrim is skipped rather than throwing.
        if (OverlayLayer.GetOverlayLayer(target) is not { } layer)
        {
            return;
        }

        Remove(flyout);

        var scrim = new Border
        {
            Background = target.FindResource("Scrim") as IBrush,
            IsHitTestVisible = false,
            Width = layer.Bounds.Width,
            Height = layer.Bounds.Height,
        };

        // The layer is a Canvas, so a child has to be told its size, and told again when
        // the window is resized under an open flyout.
        layer.PropertyChanged += Resize;
        scrim.Tag = layer;

        layer.Children.Add(scrim);
        Showing[flyout] = scrim;

        void Resize(object? _, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == Visual.BoundsProperty)
            {
                scrim.Width = layer.Bounds.Width;
                scrim.Height = layer.Bounds.Height;
            }
        }
    }

    private static void OnClosed(object? sender, EventArgs e)
    {
        if (sender is FlyoutBase flyout)
        {
            Remove(flyout);
        }
    }

    private static void Remove(FlyoutBase flyout)
    {
        if (!Showing.Remove(flyout, out var scrim))
        {
            return;
        }

        if (scrim.Tag is OverlayLayer layer)
        {
            layer.Children.Remove(scrim);
        }
    }
}
