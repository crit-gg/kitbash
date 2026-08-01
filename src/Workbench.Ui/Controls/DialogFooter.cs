using Avalonia.Controls;

namespace Workbench.Ui.Controls;

/// <summary>
/// The row a dialog's actions sit in. It sits on the root tone rather than the body
/// tone, which is what separates it from the content above without a heavy rule.
/// </summary>
/// <remarks>
/// It holds whatever the dialog puts in it and aligns that to the right, so a dialog
/// supplies its buttons and nothing else.
/// </remarks>
public class DialogFooter : ContentControl
{
}
