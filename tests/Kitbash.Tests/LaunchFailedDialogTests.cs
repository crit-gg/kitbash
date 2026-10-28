using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Kitbash.Core.Godot;
using Kitbash.Ui.Controls;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// The dialog a failed open ends in. The well shows errors and nothing else, and only a
/// failed build offers to go on without one.
/// </summary>
public sealed class LaunchFailedDialogTests
{
    private const string Error =
        "/game/A.cs(4,89): error CS0103: The name 'Missing' does not exist in the current context [/game/Game.csproj]";

    private const string Warning =
        "/game/A.cs(4,79): warning CS8602: Dereference of a possibly null reference. [/game/Game.csproj]";

    private static LaunchFailedDialog Open(string output, GodotLaunchMode mode = GodotLaunchMode.Editor,
        GodotLaunchStage stage = GodotLaunchStage.Building)
    {
        var dialog = LaunchFailedDialog.For(new GodotLaunchException(stage, "The C# build failed.", output), mode);

        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        dialog.UpdateLayout();

        return dialog;
    }

    private static string Shown(LaunchFailedDialog dialog) =>
        dialog.GetControl<SelectableTextBlock>("Output").Text ?? string.Empty;

    private static Button OpenAnyway(LaunchFailedDialog dialog) => dialog.GetControl<Button>("OpenAnyway");

    /// <summary>dotnet prints each error twice, once inline and once in its summary.</summary>
    [AvaloniaFact]
    public void TheWellShowsEachErrorOnceAndNoWarnings()
    {
        var dialog = Open(string.Join('\n',
            "  Determining projects to restore...",
            Warning,
            Error,
            "",
            "Build FAILED.",
            "",
            Warning,
            Error,
            "    1 Warning(s)",
            "    1 Error(s)"));

        Assert.Equal(Error, Shown(dialog));
    }

    /// <summary>The editor's own line says only that a build failed, so the compiler's outrank it.</summary>
    [AvaloniaFact]
    public void TheCompilerErrorsOutrankTheEnginesLine()
    {
        var dialog = Open(string.Join('\n',
            "ERROR: An EditorPlugin build callback failed.",
            "   at: call_build (editor/editor_node.cpp:7678)",
            "\t" + Error,
            "\t" + Warning));

        Assert.Equal(Error, Shown(dialog));
    }

    [AvaloniaFact]
    public void WithNoErrorLineTheWarningsStillGo()
    {
        var dialog = Open(string.Join('\n', "Something went wrong.", Warning, "WARNING: a thing", "    1 Warning(s)"));

        Assert.Equal("Something went wrong.", Shown(dialog));
    }

    [AvaloniaFact]
    public void AFailedBuildOffersToOpenAnyway()
    {
        var button = OpenAnyway(Open(Error));

        Assert.True(button.IsVisible);
        Assert.Equal("Open anyway", button.Content);
    }

    [AvaloniaFact]
    public void AFailedBuildBeforeARunOffersToRunAnyway()
    {
        var button = OpenAnyway(Open(Error, GodotLaunchMode.Play));

        Assert.True(button.IsVisible);
        Assert.Equal("Run anyway", button.Content);
    }

    [AvaloniaFact]
    public void ARebuildHasNothingToOpenAnyway()
    {
        Assert.False(OpenAnyway(Open(Error, GodotLaunchMode.Rebuild)).IsVisible);
    }

    [AvaloniaFact]
    public void AFailedImportOffersNothingMore()
    {
        Assert.False(OpenAnyway(Open("ERROR: no", stage: GodotLaunchStage.Importing)).IsVisible);
    }

    /// <summary>The question opens over the failure, so refusing it leaves the failure up.</summary>
    [AvaloniaFact]
    public void OpenAnywayAsksAndRefusingLeavesTheFailureUp()
    {
        var dialog = Open(Error);
        var closed = false;
        dialog.Closed += (_, _) => closed = true;

        OpenAnyway(dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var question = Assert.IsType<ConfirmDialog>(Assert.Single(dialog.OwnedWindows));
        var yes = question.GetControl<Button>("Accept");

        Assert.Equal("Open the project without building?", question.GetControl<TextBlock>("Heading").Text);
        Assert.False(yes.IsEnabled);
        Assert.Equal("Yes (3)", yes.Content);

        question.GetControl<Button>("Refuse").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(dialog.OwnedWindows);
        Assert.False(closed);
    }

    /// <summary>Asked before anything has failed, so there is no wait on Yes.</summary>
    [AvaloniaFact]
    public void TheQuestionWithoutAFailureIsReadyAtOnce()
    {
        var question = LaunchFailedDialog.ConfirmSkippingBuild(GodotLaunchMode.Editor, waits: false);

        question.Show();
        Dispatcher.UIThread.RunJobs();

        var yes = question.GetControl<Button>("Accept");

        Assert.True(yes.IsEnabled);
        Assert.Equal("Yes", yes.Content);
        Assert.Contains("danger", yes.Classes);
        Assert.True(question.GetControl<Button>("Refuse").IsFocused);
    }
}
