using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Wires the resize grips a drawn window frame supplies. The frame in
/// Themes/Controls/WindowChrome.axaml names eight borders and tags each with the edge it
/// pulls, and this turns a press on one into a resize.
/// </summary>
public class WindowResize
{
    /// <summary>Turn on for a window whose template carries the PART_Resize borders.</summary>
    public static readonly AttachedProperty<bool> GripsProperty =
        AvaloniaProperty.RegisterAttached<WindowResize, Window, bool>("Grips");

    private static readonly string[] GripNames =
    [
        "PART_ResizeTopLeft",
        "PART_ResizeTop",
        "PART_ResizeTopRight",
        "PART_ResizeLeft",
        "PART_ResizeRight",
        "PART_ResizeBottomLeft",
        "PART_ResizeBottom",
        "PART_ResizeBottomRight",
    ];

    static WindowResize() => GripsProperty.Changed.AddClassHandler<Window>(OnGripsChanged);

    public static void SetGrips(Window window, bool value) => window.SetValue(GripsProperty, value);

    public static bool GetGrips(Window window) => window.GetValue(GripsProperty);

    private static void OnGripsChanged(Window window, AvaloniaPropertyChangedEventArgs change)
    {
        window.TemplateApplied -= OnTemplateApplied;

        if (change.GetNewValue<bool>())
        {
            window.TemplateApplied += OnTemplateApplied;
        }
    }

    // A frame is replaced whenever the window swaps template, so every application has to
    // reach the grips again.
    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        foreach (var name in GripNames)
        {
            if (e.NameScope.Find<Border>(name) is not { } grip)
            {
                continue;
            }

            grip.PointerPressed -= OnGripPressed;
            grip.PointerPressed += OnGripPressed;
        }
    }

    private static void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: WindowEdge edge } grip)
        {
            return;
        }

        if (grip.FindAncestorOfType<Window>() is not { CanResize: true } window)
        {
            return;
        }

        window.BeginResizeDrag(edge, e);
        e.Handled = true;
    }
}
