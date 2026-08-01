using Avalonia.Controls;

namespace Workbench.Ui.Controls;

/// <summary>
/// A dialog is a real window rather than an overlay, with the same frame and the same
/// title bar as any other. There is no scrim behind it.
/// </summary>
/// <remarks>
/// It differs from a main window in three ways only. It cannot be resized or
/// minimised, so the title bar keeps its close button alone. It centres on the window
/// that opened it. It stays out of the task bar, because it belongs to that window
/// rather than standing on its own.
/// <para>
/// A dialog lays out its own content. <see cref="DialogFooter"/> is the row its
/// actions sit in. Size is left to the caller, so a dialog can be measured or fixed.
/// </para>
/// </remarks>
public class DialogWindow : ChromelessWindow
{
    public DialogWindow()
    {
        CanResize = false;
        CanMinimize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }
}
