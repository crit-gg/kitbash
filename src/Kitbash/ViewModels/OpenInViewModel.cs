using CommunityToolkit.Mvvm.ComponentModel;
using Kitbash.Core.Platform;
using Kitbash.Core.Platform.Openers;
using Kitbash.Ui.Toasts;

namespace Kitbash.ViewModels;

/// <summary>
/// The Open in menu. It gathers off the UI thread and assigns on it, so opening the menu
/// never touches a disk.
/// </summary>
public sealed partial class OpenInViewModel : ObservableObject
{
    private readonly IWorkspaceOpeners _openers;
    private readonly IPlatformServices _platform;
    private readonly IToastService _toasts;

    [ObservableProperty]
    private IReadOnlyList<OpenInRow> _rows = [];

    public OpenInViewModel(IWorkspaceOpeners openers, IPlatformServices platform, IToastService toasts)
    {
        ArgumentNullException.ThrowIfNull(openers);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(toasts);

        _openers = openers;
        _platform = platform;
        _toasts = toasts;
    }

    /// <summary>
    /// False when there is no workspace open and when this machine has nothing to open one
    /// in. The button goes entirely rather than offering a menu with nothing worth picking.
    /// </summary>
    public bool HasItems => Rows.Count > 0;

    /// <summary>
    /// Gathers what this workspace can be opened in. Walks a disk, so everything before
    /// the assignment runs off the UI thread.
    /// </summary>
    public async Task RefreshAsync(string? workspaceRoot, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            Rows = [];

            return;
        }

        try
        {
            var openers = await _openers.ReadAsync(cancellation).ConfigureAwait(true);
            var choices = await Task.Run(
                () => openers.ToDictionary(
                    opener => opener.Id,
                    opener => _openers.ChoicesFor(opener, workspaceRoot),
                    StringComparer.Ordinal),
                cancellation).ConfigureAwait(true);

            Rows = Build(openers, choices, workspaceRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A disk that will not answer leaves the button offering the folder alone
            // rather than taking the window down.
            Rows = [Folder(workspaceRoot)];
        }
    }

    private IReadOnlyList<OpenInRow> Build(
        IReadOnlyList<WorkspaceOpener> openers,
        IReadOnlyDictionary<string, IReadOnlyList<OpenChoice>> choices,
        string workspaceRoot)
    {
        var terminals = Group(openers, choices, workspaceRoot, WorkspaceOpenerKind.Terminal);

        var programs = Group(openers, choices, workspaceRoot, WorkspaceOpenerKind.Editor)
            .Concat(Group(openers, choices, workspaceRoot, WorkspaceOpenerKind.Custom))
            .ToArray();

        // Nothing to open the workspace in means no button at all, since a menu offering
        // only the folder is not what the button says it does.
        if (programs.Length == 0 && terminals.Count == 0)
        {
            return [];
        }

        return Join([[Folder(workspaceRoot)], terminals, programs]);
    }

    /// <summary>
    /// Every tool of one kind, one flat row each. A tool opens the first thing it would
    /// rather have, such as a solution, and the workspace folder when it found none.
    /// </summary>
    private List<OpenInRow> Group(
        IReadOnlyList<WorkspaceOpener> openers,
        IReadOnlyDictionary<string, IReadOnlyList<OpenChoice>> choices,
        string workspaceRoot,
        WorkspaceOpenerKind kind)
    {
        List<OpenInRow> rows = [];

        foreach (var opener in openers.Where(opener => opener.Kind == kind))
        {
            // The walk reads a folder before it descends, so the first is the shallowest.
            var best = choices.TryGetValue(opener.Id, out var found) ? found.FirstOrDefault() : null;

            rows.Add(new OpenInRow
            {
                Header = opener.Name,
                IconKey = opener.IconKey,
                Invoke = () => Start(opener, best, workspaceRoot),
            });
        }

        return rows;
    }

    private OpenInRow Folder(string workspaceRoot) => new()
    {
        Header = "Open folder",
        Invoke = () => _ = Task.Run(() =>
        {
            try
            {
                _platform.OpenInFileBrowser(DirectoryLocation.Parse(workspaceRoot));
            }
            catch (Exception exception) when (exception is ProcessStartException
                or DirectoryNotFoundException or PlatformNotSupportedException or ArgumentException)
            {
                Failed("The folder could not be opened", exception.Message);
            }
        }),
    };

    private void Start(WorkspaceOpener opener, OpenChoice? choice, string workspaceRoot) =>
        _ = Task.Run(() =>
        {
            try
            {
                _openers.Open(opener, choice, workspaceRoot);
            }
            catch (ProcessStartException exception)
            {
                Failed($"{opener.Name} could not be started", exception.Message);
            }
        });

    private void Failed(string title, string body) => _toasts.Post(new ToastRequest
    {
        Tier = ToastTier.Error,
        Title = title,
        Body = body,
    });

    /// <summary>Puts a rule between groups, and never before, after or beside another.</summary>
    private static IReadOnlyList<OpenInRow> Join(IReadOnlyList<IReadOnlyList<OpenInRow>> groups)
    {
        List<OpenInRow> rows = [];

        foreach (var group in groups.Where(group => group.Count > 0))
        {
            if (rows.Count > 0)
            {
                rows.Add(OpenInRow.Separator);
            }

            rows.AddRange(group);
        }

        return rows;
    }

    partial void OnRowsChanged(IReadOnlyList<OpenInRow> value) => OnPropertyChanged(nameof(HasItems));
}
