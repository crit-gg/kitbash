namespace Kitbash.Core.Platform.Openers;

internal sealed class WorkspaceOpeners : IWorkspaceOpeners
{
    private readonly IWorkspaceOpenerFinder _finder;
    private readonly ICustomOpeners _custom;
    private readonly IWorkspaceFileFinder _files;
    private readonly IOpenerArguments _arguments;
    private readonly IPlatformServices _platform;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<WorkspaceOpener>? _detected;

    public WorkspaceOpeners(
        IWorkspaceOpenerFinder finder,
        ICustomOpeners custom,
        IWorkspaceFileFinder files,
        IOpenerArguments arguments,
        IPlatformServices platform)
    {
        ArgumentNullException.ThrowIfNull(finder);
        ArgumentNullException.ThrowIfNull(custom);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(platform);

        _finder = finder;
        _custom = custom;
        _files = files;
        _arguments = arguments;
        _platform = platform;
    }

    public async Task<IReadOnlyList<WorkspaceOpener>> ReadAsync(CancellationToken cancellation = default)
    {
        return [.. await DetectedAsync(cancellation).ConfigureAwait(false), .. Custom()];
    }

    public IReadOnlyList<OpenChoice> ChoicesFor(WorkspaceOpener opener, string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(opener);

        if (opener.OpensFiles.Count == 0 || string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return [];
        }

        return
        [
            .. _files.Find(workspaceRoot, opener.OpensFiles, opener.SearchDepth)
                .Select(file => new OpenChoice(
                    Path.GetRelativePath(workspaceRoot, file),
                    [.. opener.FixedArguments, file])),
        ];
    }

    public void Open(WorkspaceOpener opener, OpenChoice? choice, string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(opener);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var arguments = choice?.Arguments ?? Arguments(opener, workspaceRoot);

        // Running in the workspace is what makes a terminal's own -d . and --cwd . work,
        // and it is the one form every program agrees on.
        var request = new ProcessRequest(
            opener.Program,
            arguments,
            UseShellExecute: false,
            workspaceRoot);

        // An editor is a person's next few hours and the launcher is a window they may
        // close, so the two do not share a fate.
        _platform.StartDetached(request);
    }

    /// <summary>
    /// A person's own tool is whatever they typed. A detected one carries its own flags
    /// and then the workspace, unless it is told where it is by the working directory.
    /// </summary>
    private IReadOnlyList<string> Arguments(WorkspaceOpener opener, string workspaceRoot)
    {
        if (opener.Kind is WorkspaceOpenerKind.Custom)
        {
            return _arguments.Build(opener.ArgumentTemplate, workspaceRoot);
        }

        return opener.TakesPathArgument
            ? [.. opener.FixedArguments, workspaceRoot]
            : opener.FixedArguments;
    }

    private async Task<IReadOnlyList<WorkspaceOpener>> DetectedAsync(CancellationToken cancellation)
    {
        if (_detected is { } held)
        {
            return held;
        }

        await _gate.WaitAsync(cancellation).ConfigureAwait(false);

        try
        {
            return _detected ??= await _finder.FindAsync(cancellation).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Read every time, since the settings window can add one while the launcher is open.
    /// A row naming a program that is not there is still offered, so pressing it says so
    /// rather than the tool quietly vanishing from the menu.
    /// </summary>
    private IReadOnlyList<WorkspaceOpener> Custom() =>
    [
        .. _custom.Read().Select((opener, index) => new WorkspaceOpener(
            $"custom.{index}",
            opener.Name,
            "custom",
            opener.Path,
            WorkspaceOpenerKind.Custom)
        {
            ArgumentTemplate = opener.Arguments,
        }),
    ];
}
