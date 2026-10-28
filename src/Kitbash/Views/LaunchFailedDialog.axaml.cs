using Avalonia.Controls;
using Avalonia.Input.Platform;
using Kitbash.Core.Godot;
using Kitbash.Ui.Controls;

namespace Kitbash.Views;

/// <summary>
/// Says a build or an import failed, and shows what the program wrote.
/// </summary>
public partial class LaunchFailedDialog : DialogWindow
{
    /// <summary>How long the button says Copied before it goes back to its own word.</summary>
    private static readonly TimeSpan Confirmation = TimeSpan.FromMilliseconds(1400);

    /// <summary>How long Yes stays off when the question follows a failed build.</summary>
    private static readonly TimeSpan SkipBuildWait = TimeSpan.FromSeconds(3);

    private bool _copying;

    public LaunchFailedDialog()
    {
        InitializeComponent();
    }

    /// <returns>True when the person chose to open without building and confirmed it.</returns>
    public static Task<bool> Show(Window owner, GodotLaunchException failure, GodotLaunchMode mode)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return For(failure, mode).ShowFor<bool>(owner);
    }

    /// <summary>
    /// Asks before a project opens without its C# being built.
    /// </summary>
    /// <param name="waits">True holds Yes off for a few seconds, which follows a failed build.</param>
    public static ConfirmDialog ConfirmSkippingBuild(GodotLaunchMode mode, bool waits)
    {
        var verb = mode == GodotLaunchMode.Play ? "run" : "open";
        var title = mode == GodotLaunchMode.Play ? "Run" : "Open";

        return ConfirmDialog.For(
            $"{title} without building",
            $"{title} the project without building?",
            $"Only {verb} the project without first building if you know what you are doing. "
            + "Godot will load the assemblies from the last good build, which may be missing "
            + "or out of date.",
            "Yes",
            ConfirmWeight.Grave,
            wait: waits ? SkipBuildWait : TimeSpan.Zero);
    }

    /// <summary>
    /// The dialog, built and filled in but not shown. Separate from <see cref="Show"/> so
    /// it can be rendered without a window to own it.
    /// </summary>
    public static LaunchFailedDialog For(
        GodotLaunchException failure, GodotLaunchMode mode = GodotLaunchMode.Editor)
    {
        ArgumentNullException.ThrowIfNull(failure);

        // What did not start, so the sentence says the thing the person asked for rather
        // than always naming the editor. A rebuild starts nothing, so it says so.
        var target = mode switch
        {
            GodotLaunchMode.Editor => "The editor",
            GodotLaunchMode.Rebuild => "The rebuild",
            _ => "The project",
        };

        var dialog = new LaunchFailedDialog();

        var what = failure.Stage switch
        {
            GodotLaunchStage.Cleaning => "The import cache could not be deleted",
            GodotLaunchStage.Building => "The C# build failed",
            GodotLaunchStage.Importing => "Importing assets failed",
            _ => mode switch
            {
                GodotLaunchMode.Editor => "The editor did not start",
                GodotLaunchMode.Rebuild => "The rebuild did not finish",
                _ => "The project did not start",
            },
        };

        // The title names the task and the heading names what went wrong, which is the
        // design's rule that a heading never repeats the title.
        dialog.Title = mode == GodotLaunchMode.Rebuild
            ? "Could not rebuild the project"
            : "Could not open the project";

        dialog.Heading.Text = $"{what}.";

        dialog.Body.Text = failure.Stage switch
        {
            GodotLaunchStage.Cleaning =>
                "Nothing was deleted and nothing was rebuilt. Something is most likely "
                + "holding a file in it, such as an editor that is already open.",
            GodotLaunchStage.Building => mode == GodotLaunchMode.Rebuild
                ? "The import cache was deleted and the build that follows it failed, so "
                  + "the assets were not imported. Fixing the build and rebuilding again "
                  + "is what puts the project back."
                : $"{target} was not started, since it would have run without the "
                  + "assemblies this project needs.",
            GodotLaunchStage.Importing => mode == GodotLaunchMode.Rebuild
                ? "The import cache was deleted and could not be made again. Opening the "
                  + "project in Godot will import again and show the same problem."
                : $"{target} was not started. Opening the project in Godot will import "
                  + "again and show the same problem.",
            _ => failure.Message,
        };

        var output = failure.Output.Length > 0 ? failure.Output : failure.Message;
        var shown = Problems(output);

        dialog.Output.Text = shown;
        dialog.OutputWell.IsVisible = shown.Length > 0;

        // The button says what it copies, since the well is showing something shorter and
        // a person has to know the whole thing is what lands on the clipboard.
        var label = failure.Stage switch
        {
            GodotLaunchStage.Building => "Copy build log",
            GodotLaunchStage.Importing => "Copy import log",
            GodotLaunchStage.Cleaning => "Copy details",
            _ => "Copy details",
        };

        dialog.Copy.Content = label;
        dialog.Copy.Click += async (_, _) => await dialog.CopyAsync(output, label);

        // A rebuild has no project to open, since the import that follows the build never ran.
        if (failure.Stage == GodotLaunchStage.Building && mode != GodotLaunchMode.Rebuild)
        {
            dialog.OpenAnyway.Content = mode == GodotLaunchMode.Play ? "Run anyway" : "Open anyway";
            dialog.OpenAnyway.IsVisible = true;
            dialog.OpenAnyway.Click += async (_, _) => await dialog.ConfirmAsync(mode);
        }

        return dialog;
    }

    /// <summary>
    /// Asks over this dialog, so a person who refuses is back at the failure.
    /// </summary>
    private async Task ConfirmAsync(GodotLaunchMode mode)
    {
        if (await ConfirmSkippingBuild(mode, waits: true).ShowFor<bool>(this))
        {
            Close(true);
        }
    }

    /// <summary>
    /// Puts the whole log on the clipboard and says so on the button for a moment.
    /// </summary>
    private async Task CopyAsync(string log, string label)
    {
        if (_copying || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        _copying = true;

        try
        {
            await clipboard.SetTextAsync(log);

            Copy.Content = "Copied";

            await Task.Delay(Confirmation);

            Copy.Content = label;
        }
        finally
        {
            _copying = false;
        }
    }

    /// <summary>
    /// The errors in a program's output, once each. The compiler's own outrank Godot's line
    /// saying a build failed. With neither, everything but the warnings.
    /// </summary>
    private static string Problems(string output)
    {
        var lines = output.Split('\n').Select(raw => raw.Trim()).Where(line => line.Length > 0).ToList();

        var compiler = lines.Where(IsCompilerError).Distinct(StringComparer.Ordinal).ToList();

        if (compiler.Count > 0)
        {
            return string.Join(Environment.NewLine, compiler);
        }

        var engine = lines.Where(IsEngineError).Distinct(StringComparer.Ordinal).ToList();

        return string.Join(Environment.NewLine, engine.Count > 0 ? engine : lines.Where(line => !IsWarning(line)));
    }

    // MSBuild writes every diagnostic as "origin: error CODE: text", the summary included.
    private static bool IsCompilerError(string line) =>
        line.Contains(": error ", StringComparison.OrdinalIgnoreCase);

    private static bool IsEngineError(string line) =>
        line.StartsWith("ERROR:", StringComparison.Ordinal)
        || line.StartsWith("SCRIPT ERROR:", StringComparison.Ordinal);

    private static bool IsWarning(string line) =>
        line.Contains(": warning ", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("WARNING:", StringComparison.Ordinal)
        || line.EndsWith(" Warning(s)", StringComparison.Ordinal);
}
