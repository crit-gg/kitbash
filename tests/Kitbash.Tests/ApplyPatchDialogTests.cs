using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Kitbash.Core.Git;
using Kitbash.Core.IO;
using Kitbash.Ui.Controls;
using Kitbash.ViewModels;
using Kitbash.Views;

namespace Kitbash.Tests;

/// <summary>
/// The dialog a person applies a patch through, over an applier that answers from a table.
/// The real applier is tested against real git in the core tests.
/// </summary>
public sealed class ApplyPatchDialogTests : IDisposable
{
    private const string Mail =
        "From 0000000000000000000000000000000000000000 Mon Sep 17 00:00:00 2001\n"
        + "From: Dana <d@example.invalid>\n"
        + "Subject: [PATCH] Fix the door\n"
        + "\n"
        + "---\n"
        + "diff --git a/scenes/door.tscn b/scenes/door.tscn\n"
        + "index 1111111..2222222 100644\n"
        + "--- a/scenes/door.tscn\n"
        + "+++ b/scenes/door.tscn\n"
        + "@@ -1 +1 @@\n"
        + "-a\n"
        + "+b\n"
        + "diff --git a/scripts/Door.cs b/scripts/Door.cs\n"
        + "index 1111111..2222222 100644\n"
        + "--- a/scripts/Door.cs\n"
        + "+++ b/scripts/Door.cs\n"
        + "@@ -1 +1 @@\n"
        + "-a\n"
        + "+b\n";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kitbash-apply-" + Path.GetRandomFileName());
    private readonly FakeApplier _applier = new();

    public ApplyPatchDialogTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Patch(string text = Mail)
    {
        var file = Path.Combine(_folder, "fix.patch");

        File.WriteAllText(file, text);

        return file;
    }

    private (ApplyPatchDialog Dialog, ApplyPatchViewModel Model) Open()
    {
        var model = new ApplyPatchViewModel(_applier, new FileSystem(), _folder);
        var dialog = new ApplyPatchDialog { DataContext = model };

        dialog.Show();
        Settle();

        return (dialog, model);
    }

    /// <summary>The reads run on the thread pool and come back through the dispatcher.</summary>
    private static void Settle()
    {
        for (var i = 0; i < 50; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    private static T Named<T>(ApplyPatchDialog dialog, string name) where T : Control =>
        dialog.GetControl<T>(name);

    private static Alert Notice(ApplyPatchDialog dialog) =>
        dialog.GetLogicalDescendants().OfType<Alert>().Last();

    [AvaloniaFact]
    public void NothingCanBeAppliedUntilAPatchIsPicked()
    {
        var (dialog, _) = Open();

        Assert.False(Named<Button>(dialog, "ApplyButton").IsEnabled);
    }

    [AvaloniaFact]
    public void APickedPatchSaysWhatItChangesAndTheCommitItMakes()
    {
        var (dialog, model) = Open();

        model.PatchFile = Patch();
        Settle();

        Assert.True(Named<Button>(dialog, "ApplyButton").IsEnabled);
        Assert.Equal("This patch changes 2 files", model.FilesHeading);
        Assert.Equal(["scenes/door.tscn", "scripts/Door.cs"], Named<ItemsControl>(dialog, "PathList").Items.Cast<string>());
        Assert.Equal("Fix the door", Named<TextBlock>(dialog, "SubjectText").Text);
    }

    [AvaloniaFact]
    public void APlainDiffIsRefusedOnTheField()
    {
        var (dialog, model) = Open();

        model.PatchFile = Patch(Mail[Mail.IndexOf("diff --git", StringComparison.Ordinal)..]);
        Settle();

        Assert.Equal("Kitbash can only apply a patch made with git format-patch.", Named<PathField>(dialog, "PatchField").Problem);
        Assert.False(Named<Button>(dialog, "ApplyButton").IsEnabled);
    }

    [AvaloniaFact]
    public async Task ALandedPatchClosesTheDialogAndSaysWhere()
    {
        var (dialog, model) = Open();
        _applier.Answer = new GitPatchResult(GitPatchOutcome.Applied, Files: ["a"], Branch: "main");
        var closed = false;
        dialog.Closed += (_, _) => closed = true;

        model.PatchFile = Patch();
        Settle();

        Assert.True(await model.ApplyAsync());
        Assert.Equal("Fix the door is now a commit on main.", model.Landed);
        Assert.False(closed);
    }

    [AvaloniaFact]
    public async Task AChangeInTheWayIsNamedAndSaysNothingChanged()
    {
        var (dialog, model) = Open();
        _applier.Answer = new GitPatchResult(GitPatchOutcome.InTheWay, "conflict", ["scenes/door.tscn"]);

        model.PatchFile = Patch();
        Settle();

        Assert.False(await model.ApplyAsync());
        Settle();

        var notice = Notice(dialog);

        Assert.True(notice.IsOpen);
        Assert.Equal(AlertTier.Error, notice.Tier);
        Assert.Equal("Your own changes to door.tscn are in the way of this patch.", notice.Title);
        Assert.StartsWith("Nothing was changed.", notice.Description);
        Assert.True(Named<Button>(dialog, "CopyDetails").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task WorkLeftSetAsideIsOfferedBackAndBlocksApplying()
    {
        _applier.SetAside = "Kitbash: before applying fix.patch";
        var (dialog, model) = Open();

        model.PatchFile = Patch();
        Settle();

        var alert = dialog.GetLogicalDescendants().OfType<Alert>().First();

        Assert.True(alert.IsOpen);
        Assert.False(Named<Button>(dialog, "ApplyButton").IsEnabled);

        Named<Button>(dialog, "RestoreButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle();
        await Task.Yield();

        Assert.False(alert.IsOpen);
        Assert.Equal("Your changes are back.", Notice(dialog).Title);
        Assert.True(Named<Button>(dialog, "ApplyButton").IsEnabled);
    }

    /// <summary>Stopping git part way is the one thing that could leave work set aside.</summary>
    [AvaloniaFact]
    public void TheDialogCannotCloseWhileGitRuns()
    {
        var (dialog, model) = Open();
        _applier.Hold = new TaskCompletionSource();
        var closed = false;
        dialog.Closed += (_, _) => closed = true;

        model.PatchFile = Patch();
        Settle();

        var applying = model.ApplyAsync();
        Settle();

        dialog.Close();
        Settle();

        Assert.False(closed);
        Assert.False(model.CanEdit);

        _applier.Hold.SetResult();
        Settle();

        Assert.True(applying.IsCompleted);
    }

    [AvaloniaFact]
    public void TheLaunchersUpdateButtonCarriesTheGitMenu()
    {
        var window = new LauncherWindow();
        var button = window.GetLogicalDescendants().OfType<SplitButton>().Single(split => split.Name == "GitButton");

        var menu = Assert.IsType<MenuFlyout>(button.Flyout);

        Assert.Equal(["Apply patch"], menu.Items.Cast<MenuItem>().Select(item => item.Header));
    }

    private sealed class FakeApplier : IGitPatchApplier
    {
        private readonly GitPatchReader _patches = new();

        public GitPatchResult Answer { get; set; } = new(GitPatchOutcome.Applied, Branch: "main");

        public string? SetAside { get; set; }

        public TaskCompletionSource? Hold { get; set; }

        public GitPatchPreview Read(string text) =>
            new GitPatchApplier(new NoGit(), new NoCommits(), new NoConflicts(), _patches, new FileSystem()).Read(text);

        public async Task<GitPatchResult> ApplyAsync(string root, string patchFile)
        {
            if (Hold is { } hold)
            {
                await hold.Task;
            }

            return Answer;
        }

        public Task<string?> ReadSetAsideAsync(string root, CancellationToken cancellation = default) =>
            Task.FromResult(SetAside);

        public Task<GitRestoreOutcome> RestoreAsync(string root)
        {
            SetAside = null;

            return Task.FromResult(GitRestoreOutcome.Restored);
        }
    }

    private sealed class NoGit : IGitRunner
    {
        public bool IsAvailable => false;

        public Task<GitResult> RunAsync(string root, GitCommand command, CancellationToken cancellation = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoCommits : IGitCommitter
    {
        public Task<GitResult> CommitAsync(string root, string message, GitCommitOptions? options = null, CancellationToken cancellation = default) =>
            throw new NotSupportedException();

        public Task<GitResult> RevertAsync(string root, string revision, bool commit = true, CancellationToken cancellation = default) =>
            throw new NotSupportedException();

        public Task<GitIdentity?> ReadIdentityAsync(string root, CancellationToken cancellation = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoConflicts : IGitConflictReader
    {
        public Task<IReadOnlyList<GitConflict>> ReadAsync(string root, CancellationToken cancellation = default) =>
            throw new NotSupportedException();

        public Task<GitMergeState> ReadStateAsync(string root, CancellationToken cancellation = default) =>
            throw new NotSupportedException();

        public Task<string?> ReadVersionAsync(string root, GitConflict conflict, GitConflictSide side, CancellationToken cancellation = default) =>
            throw new NotSupportedException();

        public Task<GitResult> ResolveAsync(string root, IReadOnlyList<string> paths, CancellationToken cancellation = default) =>
            throw new NotSupportedException();

        public Task<GitResult> TakeAsync(string root, IReadOnlyList<string> paths, GitConflictSide side, CancellationToken cancellation = default) =>
            throw new NotSupportedException();

        public Task<GitResult> AbortAsync(string root, CancellationToken cancellation = default) =>
            throw new NotSupportedException();
    }
}
