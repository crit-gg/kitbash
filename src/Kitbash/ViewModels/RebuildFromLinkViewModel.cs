using CommunityToolkit.Mvvm.ComponentModel;

namespace Kitbash.ViewModels;

/// <summary>A rebuild a link asked for, waiting on a person to say yes and which workspace.</summary>
public partial class RebuildFromLinkViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRebuild))]
    private WorkspaceViewModel? _target;

    /// <param name="targets">Every workspace the link's name matches. One is chosen already.</param>
    public RebuildFromLinkViewModel(IReadOnlyList<WorkspaceViewModel> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentOutOfRangeException.ThrowIfZero(targets.Count);

        Targets = targets;
        _target = targets.Count == 1 ? targets[0] : null;
    }

    public IReadOnlyList<WorkspaceViewModel> Targets { get; }

    /// <summary>True when the person has to say which workspace, since several carry the name.</summary>
    public bool HasChoice => Targets.Count > 1;

    public string Heading => HasChoice ? "Rebuild a project?" : $"Rebuild {Targets[0].Name}?";

    public bool CanRebuild => Target is not null;
}
