using CommunityToolkit.Mvvm.ComponentModel;
using Humanizer;
using Workbench.Core.Git;

namespace Workbench.ViewModels;

/// <summary>
/// The git strip along the foot of the launcher. Reads a <see cref="GitStatus"/> and says
/// nothing when there is not one, which is how the plain workspace footer gets its turn.
/// </summary>
public partial class GitViewModel : ViewModelBase
{
    [ObservableProperty]
    private GitStatus? _status;

    /// <summary>
    /// An update is running. Held here rather than beside the command, so the label below
    /// updates from one place whichever of the two changed.
    /// </summary>
    [ObservableProperty]
    private bool _isUpdating;

    /// <summary>True when the workspace is a repository and the strip should be shown.</summary>
    public bool IsRepository => Status is not null;

    public string Branch => Status?.Branch ?? "";

    public int Modified => Status?.Modified ?? 0;

    public int Staged => Status?.Staged ?? 0;

    public int Conflicted => Status?.Conflicted ?? 0;

    public int Ahead => Status?.Ahead ?? 0;

    public int Behind => Status?.Behind ?? 0;

    public bool HasModified => Modified > 0;

    public bool HasStaged => Staged > 0;

    public bool HasConflicted => Conflicted > 0;

    public bool HasAhead => Ahead > 0;

    public bool HasBehind => Behind > 0;

    /// <summary>
    /// The word beside the conflict count, agreeing with it. Modified and staged describe
    /// the files rather than name them, so they never inflect and are written in the view.
    /// </summary>
    public string ConflictWord => "conflict".ToQuantity(Conflicted, ShowQuantityAs.None);

    /// <summary>
    /// When this branch last looked at its remote, or that an update is happening now.
    /// Beside the button rather than on it, so the button keeps saying what it does.
    /// </summary>
    public string UpdateLabel => (IsUpdating, Status?.LastFetch) switch
    {
        (true, _) => "Updating",
        (_, null) => "Never updated",

        // No comparison date, so Humanizer takes UtcNow. The stamp carries its own offset,
        // so that is right wherever this machine sits.
        (_, { } updated) => $"Updated {updated.Humanize()}",
    };

    partial void OnStatusChanged(GitStatus? value) => OnPropertyChanged(string.Empty);

    partial void OnIsUpdatingChanged(bool value) => OnPropertyChanged(nameof(UpdateLabel));
}
