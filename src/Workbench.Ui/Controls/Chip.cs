using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Workbench.Ui.Controls;

/// <summary>
/// A small label carrying a mark and a value, such as a tag or a filter that is in
/// force. A chip is not a button. It says what is true, and removing it is the only
/// thing a person can do to it.
/// </summary>
public class Chip : ContentControl
{
    /// <summary>
    /// The square mark on the left. It is the one place a chip carries a colour of its
    /// own, so it is given rather than themed. A chip with no mark shows none.
    /// </summary>
    public static readonly StyledProperty<IBrush?> MarkProperty =
        AvaloniaProperty.Register<Chip, IBrush?>(nameof(Mark));

    /// <summary>Whether the chip offers a remove button.</summary>
    public static readonly StyledProperty<bool> IsRemovableProperty =
        AvaloniaProperty.Register<Chip, bool>(nameof(IsRemovable));

    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<Chip, ICommand?>(nameof(RemoveCommand));

    public static readonly StyledProperty<object?> RemoveCommandParameterProperty =
        AvaloniaProperty.Register<Chip, object?>(nameof(RemoveCommandParameter));

    /// <inheritdoc cref="MarkProperty"/>
    public IBrush? Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <inheritdoc cref="IsRemovableProperty"/>
    public bool IsRemovable
    {
        get => GetValue(IsRemovableProperty);
        set => SetValue(IsRemovableProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public object? RemoveCommandParameter
    {
        get => GetValue(RemoveCommandParameterProperty);
        set => SetValue(RemoveCommandParameterProperty, value);
    }
}
