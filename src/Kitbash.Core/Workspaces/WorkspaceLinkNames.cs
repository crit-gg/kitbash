using Kitbash.Core.Git;

namespace Kitbash.Core.Workspaces;

internal sealed class WorkspaceLinkNames : IWorkspaceLinkNames
{
    private readonly IWorkspaceRegistry _registry;
    private readonly IWorkspaceNameResolver _names;
    private readonly IGitRunner _git;

    public WorkspaceLinkNames(IWorkspaceRegistry registry, IWorkspaceNameResolver names, IGitRunner git)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(git);

        _registry = registry;
        _names = names;
        _git = git;
    }

    public async Task<string?> ForAsync(string root, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (_names.TeamName(root) is { } name)
        {
            return name;
        }

        // The smallest, so a history with several roots always writes the same one.
        return (await RootsAsync(root, cancellation).ConfigureAwait(false)).Order(StringComparer.Ordinal).FirstOrDefault();
    }

    public async Task<IReadOnlyList<Workspace>> MatchAsync(string name, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var wanted = name.Trim();
        var isCommit = IsCommit(wanted);
        var found = new List<Workspace>();

        foreach (var workspace in _registry.All.Where(workspace => workspace.Exists))
        {
            if (string.Equals(_names.TeamName(workspace.Root), wanted, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(workspace);
            }
            else if (isCommit && (await RootsAsync(workspace.Root, cancellation).ConfigureAwait(false))
                .Contains(wanted, StringComparer.OrdinalIgnoreCase))
            {
                found.Add(workspace);
            }
        }

        return found;
    }

    /// <summary>
    /// Every commit with no parent that the head reaches. One in an ordinary repository,
    /// more after unrelated histories were merged, and none without a commit.
    /// </summary>
    private async Task<IReadOnlyList<string>> RootsAsync(string root, CancellationToken cancellation)
    {
        var roots = await _git
            .RunAsync(root, GitCommand.Of("rev-list", "--max-parents=0", "HEAD"), cancellation)
            .ConfigureAwait(false);

        return roots.Succeeded ? roots.Lines : [];
    }

    /// <summary>A full object name, 40 hex digits for SHA 1 or 64 for SHA 256.</summary>
    private static bool IsCommit(string value) =>
        value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit);
}
