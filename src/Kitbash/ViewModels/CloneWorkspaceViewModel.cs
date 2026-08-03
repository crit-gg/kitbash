using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Git;

namespace Kitbash.ViewModels;

/// <summary>
/// One clone being set up. Holds the two answers a person gives and runs git once they
/// are both good.
/// </summary>
public partial class CloneWorkspaceViewModel : ObservableObject
{
    private readonly IGitCloner _cloner;

    private CancellationTokenSource? _running;

    [ObservableProperty]
    private string _address = string.Empty;

    /// <summary>The folder the clone goes under, not the folder it makes.</summary>
    [ObservableProperty]
    private string _folder;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isCloning;

    public CloneWorkspaceViewModel(IGitCloner cloner, string folder)
    {
        ArgumentNullException.ThrowIfNull(cloner);
        ArgumentNullException.ThrowIfNull(folder);

        _cloner = cloner;
        _folder = folder;
    }

    /// <summary>Where the clone landed. Null until one has finished.</summary>
    public string? Root { get; private set; }

    public bool HasMessage => Message.Length > 0;

    /// <summary>Nothing is edited while git runs, since both answers are already in use.</summary>
    public bool CanEdit => !IsCloning;

    public bool CanClone => !IsCloning && Remote is not null && HasFolder;

    /// <summary>
    /// The folder that would be made, so the name git picks is on screen before it picks
    /// it. A line about where the name comes from until there is enough to say.
    /// </summary>
    public string Target => Remote is { } remote && HasFolder
        ? Path.Combine(Folder.Trim(), remote.Name)
        : "The folder is named after the repository";

    private GitRemote? Remote => GitRemote.TryParse(Address, out var remote) ? remote : null;

    private bool HasFolder => Path.IsPathRooted(Folder.Trim());

    /// <summary>
    /// Runs the clone. True means <see cref="Root"/> holds a folder. False leaves the
    /// reason in <see cref="Message"/>, or leaves it empty when the person stopped it.
    /// </summary>
    public async Task<bool> CloneAsync()
    {
        if (!CanClone || Remote is not { } remote)
        {
            return false;
        }

        Message = string.Empty;
        IsCloning = true;

        using var running = new CancellationTokenSource();
        _running = running;

        try
        {
            var result = await _cloner
                .CloneAsync(remote, Folder.Trim(), running.Token)
                .ConfigureAwait(true);

            if (result.Succeeded)
            {
                Root = result.Destination;

                return true;
            }

            Message = Explain(result);

            return false;
        }
        finally
        {
            _running = null;
            IsCloning = false;
        }
    }

    /// <summary>Stops a clone that is running. Does nothing when none is.</summary>
    public void Cancel() => _running?.Cancel();

    partial void OnAddressChanged(string value) => Restate();

    partial void OnFolderChanged(string value) => Restate();

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsCloningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanClone));
    }

    private void Restate()
    {
        OnPropertyChanged(nameof(CanClone));
        OnPropertyChanged(nameof(Target));
    }

    /// <summary>
    /// What went wrong, in a sentence. Git's own last line is kept for the cases nothing
    /// here recognises, since it is usually the only thing that says why.
    /// </summary>
    private static string Explain(GitCloneResult result) => result.Outcome switch
    {
        GitCloneOutcome.GitMissing => "Kitbash could not find git on this machine.",
        GitCloneOutcome.DestinationExists => "That folder already exists. Pick another place for it.",
        GitCloneOutcome.NeedsCredentials =>
            "Git could not sign in. Set up a credential helper or an SSH key, then try again.",
        GitCloneOutcome.NotFound => "There is no repository at that address.",
        GitCloneOutcome.Cancelled => string.Empty,
        GitCloneOutcome.TimedOut => "The clone took too long, so it was stopped.",
        _ => LastLine(result.Message) ?? "The clone did not finish.",
    };

    private static string? LastLine(string message) =>
        message
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
}
