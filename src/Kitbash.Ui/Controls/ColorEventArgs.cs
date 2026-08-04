using Avalonia.Interactivity;

namespace Kitbash.Ui.Controls;

/// <summary>A routed event carrying one colour.</summary>
public class ColorEventArgs : RoutedEventArgs
{
    public ColorEventArgs(RoutedEvent routedEvent, ColorValue color)
        : base(routedEvent) =>
        Color = color;

    public ColorValue Color { get; }
}
