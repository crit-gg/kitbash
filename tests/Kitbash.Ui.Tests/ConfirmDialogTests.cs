using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Kitbash.Ui.Controls;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The question a gesture that cannot be undone asks first. What matters is that the grave
/// form cannot be answered yes by a keypress, since that is the whole reason it exists.
/// </summary>
public class ConfirmDialogTests
{
    private static Button Button(ConfirmDialog dialog, string name) =>
        dialog.GetControl<Button>(name);

    private static ConfirmDialog Open(ConfirmDialog dialog)
    {
        dialog.Show();

        Dispatcher.UIThread.RunJobs();
        dialog.UpdateLayout();

        return dialog;
    }

    [AvaloniaFact]
    public void AnOrdinaryQuestionIsAnsweredYesByEnter()
    {
        var dialog = Open(ConfirmDialog.For(
            "Rename branch", "Rename this branch?", "Nobody else sees the old name.", "Rename"));

        var accept = Button(dialog, "Accept");

        Assert.Equal("Rename", accept.Content);
        Assert.Contains("primary", accept.Classes);
        Assert.True(accept.IsDefault);
        Assert.True(accept.IsFocused);
    }

    // Accepting is the dangerous answer, so Enter must not reach it.
    [AvaloniaFact]
    public void AGraveQuestionLeavesTheKeyboardOnCancel()
    {
        var dialog = Open(ConfirmDialog.For(
            "Delete branch",
            "Delete this branch?",
            "The commits on it are nowhere else.",
            "Delete",
            ConfirmWeight.Grave));

        var accept = Button(dialog, "Accept");
        var refuse = Button(dialog, "Refuse");

        Assert.Contains("danger", accept.Classes);
        Assert.DoesNotContain("primary", accept.Classes);
        Assert.False(accept.IsDefault);

        Assert.True(refuse.IsCancel);
        Assert.True(refuse.IsFocused);
    }

    [AvaloniaFact]
    public void ADetailNobodyGaveTakesNoRoom()
    {
        var dialog = Open(ConfirmDialog.For("Discard", "Throw this away?"));

        Assert.False(dialog.GetControl<TextBlock>("Detail").IsVisible);
        Assert.Equal("Throw this away?", dialog.GetControl<TextBlock>("Heading").Text);
    }

    [AvaloniaFact]
    public void EitherAnswerClosesTheDialog()
    {
        var dialog = Open(ConfirmDialog.For("Delete branch", "Delete this branch?"));

        var closed = false;
        dialog.Closed += (_, _) => closed = true;

        Button(dialog, "Accept").RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    }

    /// <summary>
    /// A save prompt has three answers. The third closes the dialog like a cancel and is
    /// told apart by which button was pressed.
    /// </summary>
    [AvaloniaFact]
    public void AThirdAnswerIsToldApartFromACancel()
    {
        var dialog = Open(ConfirmDialog.For(
            "Unsaved changes",
            "Save before closing?",
            "The edits since the last save are otherwise lost.",
            "Save",
            otherwise: "Close without saving"));

        var otherwise = Button(dialog, "Otherwise");

        Assert.True(otherwise.IsVisible);
        Assert.Equal("Close without saving", otherwise.Content);

        otherwise.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

        Assert.Equal(DialogRole.Alternate, dialog.Chose);
    }

    /// <summary>A dialog given no third answer draws two, the way every other one does.</summary>
    [AvaloniaFact]
    public void WithoutAThirdAnswerThereAreTwoButtons()
    {
        var dialog = Open(ConfirmDialog.For("Rename branch", "Rename this branch?"));

        Assert.False(Button(dialog, "Otherwise").IsVisible);
        Assert.Equal(DialogRole.Cancel, dialog.Chose);
    }
}
