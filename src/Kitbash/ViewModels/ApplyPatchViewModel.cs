using CommunityToolkit.Mvvm.ComponentModel;
using Humanizer;
using Kitbash.Core.Git;
using Kitbash.Core.IO;
using Kitbash.Ui.Controls;

namespace Kitbash.ViewModels;

/// <summary>
/// One patch being applied to one workspace. Reads the file once it is picked, says what it
/// will change, and applies it.
/// </summary>
public partial class ApplyPatchViewModel : ViewModelBase
{
    private const string NothingChanged = "Nothing was changed.";

    private readonly IGitPatchApplier _applier;
    private readonly IFileSystem _files;
    private readonly string _root;

    /// <summary>Counts reads, so one that finishes after a newer pick is dropped.</summary>
    private int _reads;

    [ObservableProperty]
    private string _patchFile = string.Empty;

    [ObservableProperty]
    private string _fileProblem = string.Empty;

    [ObservableProperty]
    private GitPatchPreview? _preview;

    [ObservableProperty]
    private string _notice = string.Empty;

    [ObservableProperty]
    private string _noticeDetail = string.Empty;

    [ObservableProperty]
    private AlertTier _noticeTier = AlertTier.Error;

    /// <summary>Git's own words for the last thing that failed, for a developer to read.</summary>
    [ObservableProperty]
    private string _details = string.Empty;

    [ObservableProperty]
    private bool _isWorking;

    /// <summary>The stash an interrupted run left behind. Empty when there is none.</summary>
    [ObservableProperty]
    private string _setAside = string.Empty;

    [ObservableProperty]
    private string _setAsideDetail = string.Empty;

    public ApplyPatchViewModel(IGitPatchApplier applier, IFileSystem files, string root)
    {
        ArgumentNullException.ThrowIfNull(applier);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _applier = applier;
        _files = files;
        _root = root;
    }

    /// <summary>The sentence the toast says once the patch has landed.</summary>
    public string Landed { get; private set; } = string.Empty;

    public bool HasPreview => Preview is { CanApply: true };

    public string FilesHeading => Preview is { } preview
        ? $"This patch changes {"file".ToQuantity(preview.Files.Count)}"
        : string.Empty;

    public IReadOnlyList<string> Paths => Preview?.Files.Select(file => file.Path).ToList() ?? [];

    public string Subject => Preview?.Subject ?? string.Empty;

    public bool CanEdit => !IsWorking;

    public bool CanApply => HasPreview && !IsWorking && !HasSetAside;

    public bool HasNotice => Notice.Length > 0;

    public bool HasDetails => Details.Length > 0;

    public bool HasSetAside => SetAside.Length > 0;

    /// <summary>Looks for work an interrupted run left set aside. Called as the dialog opens.</summary>
    public async Task CheckSetAsideAsync()
    {
        var label = await Task.Run(() => _applier.ReadSetAsideAsync(_root)).ConfigureAwait(true);

        SetAside = label ?? string.Empty;
        SetAsideDetail = "Put them back before applying another patch.";
    }

    /// <summary>Puts back what an interrupted run set aside, when that is safe.</summary>
    public async Task RestoreAsync()
    {
        if (IsWorking || !HasSetAside)
        {
            return;
        }

        IsWorking = true;

        try
        {
            var outcome = await Task.Run(() => _applier.RestoreAsync(_root)).ConfigureAwait(true);

            switch (outcome)
            {
                case GitRestoreOutcome.Restored or GitRestoreOutcome.Nothing:
                    SetAside = string.Empty;
                    Tell(AlertTier.Ok, "Your changes are back.", string.Empty);
                    break;

                case GitRestoreOutcome.NotClean:
                    SetAsideDetail =
                        "They can go back once you have no other changes. Ask your developer if you are not sure.";
                    break;

                default:
                    SetAsideDetail = $"They would not go back on their own. Ask your developer to restore {SetAside}.";
                    break;
            }
        }
        finally
        {
            IsWorking = false;
        }
    }

    /// <summary>Applies the patch. True when it landed, and the reason is on screen otherwise.</summary>
    public async Task<bool> ApplyAsync()
    {
        if (!CanApply)
        {
            return false;
        }

        IsWorking = true;
        Tell(AlertTier.Error, string.Empty, string.Empty);
        Details = string.Empty;

        try
        {
            var file = PatchFile.Trim();
            var result = await Task.Run(() => _applier.ApplyAsync(_root, file)).ConfigureAwait(true);

            if (result.Outcome == GitPatchOutcome.Applied)
            {
                Landed = Subject.Length > 0
                    ? $"{Subject} is now a commit on {result.Branch}."
                    : $"It is now a commit on {result.Branch}.";

                return true;
            }

            if (result.Outcome == GitPatchOutcome.SetAsideWaiting)
            {
                await CheckSetAsideAsync().ConfigureAwait(true);

                return false;
            }

            Details = result.Message;
            Explain(result);

            return false;
        }
        finally
        {
            IsWorking = false;
        }
    }

    partial void OnPatchFileChanged(string value) => _ = ReadAsync(value.Trim());

    partial void OnPreviewChanged(GitPatchPreview? value)
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(FilesHeading));
        OnPropertyChanged(nameof(Paths));
        OnPropertyChanged(nameof(Subject));
        OnPropertyChanged(nameof(CanApply));
    }

    partial void OnNoticeChanged(string value) => OnPropertyChanged(nameof(HasNotice));

    partial void OnDetailsChanged(string value) => OnPropertyChanged(nameof(HasDetails));

    partial void OnIsWorkingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanApply));
    }

    partial void OnSetAsideChanged(string value)
    {
        OnPropertyChanged(nameof(HasSetAside));
        OnPropertyChanged(nameof(CanApply));
    }

    /// <summary>Reads and parses the file off the UI thread, since a patch can be large.</summary>
    private async Task ReadAsync(string path)
    {
        var read = ++_reads;

        Preview = null;
        FileProblem = string.Empty;
        Tell(AlertTier.Error, string.Empty, string.Empty);

        if (path.Length == 0)
        {
            return;
        }

        var (preview, problem) = await Task.Run(() => Parse(path)).ConfigureAwait(true);

        if (read != _reads)
        {
            return;
        }

        Preview = preview;
        FileProblem = problem;
    }

    private (GitPatchPreview? Preview, string Problem) Parse(string path)
    {
        string text;

        try
        {
            text = _files.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, "This file could not be read.");
        }

        var preview = _applier.Read(text);

        if (!preview.IsMail || preview.Files.Count == 0)
        {
            return (null, "Kitbash can only apply a patch made with git format-patch.");
        }

        return preview.MissingBinary.Count > 0
            ? (null, $"{Missing(preview.MissingBinary)} Ask your developer to make it again with --binary.")
            : (preview, string.Empty);
    }

    private void Explain(GitPatchResult result)
    {
        var names = Names(result.Paths);

        switch (result.Outcome)
        {
            case GitPatchOutcome.AlreadyApplied:
                Tell(AlertTier.Info, "This patch is already in your copy of the project.", NothingChanged);
                break;
            case GitPatchOutcome.DoesNotFit:
                Tell(AlertTier.Error, "This patch does not fit your copy of the project.",
                    $"{NothingChanged} Send the details to your developer.");
                break;
            case GitPatchOutcome.InTheWay:
                Tell(AlertTier.Error, $"Your own changes to {names} are in the way of this patch.",
                    $"{NothingChanged} Ask your developer to help with those changes first.");
                break;
            case GitPatchOutcome.NotAPatch:
                Tell(AlertTier.Error, "Kitbash can only apply a patch made with git format-patch.", NothingChanged);
                break;
            case GitPatchOutcome.MissingBinary:
                Tell(AlertTier.Error, Missing(result.Paths), $"{NothingChanged} Ask your developer to make it again with --binary.");
                break;
            case GitPatchOutcome.GitMissing:
                Tell(AlertTier.Error, "Kitbash could not find git on this machine.", NothingChanged);
                break;
            case GitPatchOutcome.NotARepository:
                Tell(AlertTier.Error, "This workspace is not in a git repository.", NothingChanged);
                break;
            case GitPatchOutcome.NotOnBranch:
                Tell(AlertTier.Error, "This workspace is not on a branch.",
                    $"{NothingChanged} Ask your developer to switch it to one.");
                break;
            case GitPatchOutcome.NoCommits:
                Tell(AlertTier.Error, "This repository has no commits yet.", NothingChanged);
                break;
            case GitPatchOutcome.Busy:
                Tell(AlertTier.Error, "Git is in the middle of something else.",
                    $"{NothingChanged} Ask your developer to finish it first.");
                break;
            case GitPatchOutcome.NoIdentity:
                Tell(AlertTier.Error, "Git does not know your name yet.",
                    $"{NothingChanged} Ask your developer to set it up.");
                break;
            case GitPatchOutcome.Stranded:
                Tell(AlertTier.Warn, "Git could not put everything back.", result.SetAside.Length > 0
                    ? $"Your changes are safe in git as {result.SetAside}. Ask your developer to restore them."
                    : "Your own work is safe. Ask your developer to look at this workspace.");
                break;
            default:
                Tell(AlertTier.Error, "Git refused this patch.", $"{NothingChanged} Send the details to your developer.");
                break;
        }
    }

    private void Tell(AlertTier tier, string title, string detail)
    {
        NoticeTier = tier;
        Notice = title;
        NoticeDetail = detail;
    }

    private static string Missing(IReadOnlyList<string> paths) =>
        $"This patch is missing the contents of {Names(paths)}.";

    /// <summary>Up to two names, then how many more, so a long list stays one sentence.</summary>
    private static string Names(IReadOnlyList<string> paths)
    {
        var names = paths.Select(Path.GetFileName).ToList();

        return names.Count switch
        {
            0 => "these files",
            1 => names[0]!,
            2 => $"{names[0]} and {names[1]}",
            _ => $"{names[0]}, {names[1]} and {"other file".ToQuantity(names.Count - 2)}",
        };
    }
}
