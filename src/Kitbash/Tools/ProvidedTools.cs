using Kitbash.Core.Workspaces;

namespace Kitbash.Tools;

/// <summary>
/// Matches what is installed against the lists in force. A tool the global list provides is
/// here in every workspace and one a workspace provides is here only where it is listed.
/// </summary>
public sealed class ProvidedTools : IProvidedTools
{
    private readonly IToolRepositoryList _list;

    public ProvidedTools(IToolRepositoryList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        _list = list;
    }

    public IReadOnlyList<ProvidedTool> Here(IReadOnlyList<InstalledTool> installed)
    {
        ArgumentNullException.ThrowIfNull(installed);

        return Matched(installed, _list.Read);
    }

    public IReadOnlyList<ProvidedTool> For(IReadOnlyList<InstalledTool> installed, Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(workspace);

        return Matched(installed, () => _list.ReadFor(workspace));
    }

    /// <summary>
    /// <paramref name="lists"/> is the lists in force where the answer is wanted, and it is
    /// a function so nothing installed reads no file at all.
    /// </summary>
    private IReadOnlyList<ProvidedTool> Matched(
        IReadOnlyList<InstalledTool> installed,
        Func<IReadOnlyList<ToolRepositorySource>> lists)
    {
        if (installed.Count == 0)
        {
            return [];
        }

        var inForce = lists();

        // Every registered workspace's list, read at most once and only when a tool needs
        // it, since it is a file read per workspace.
        IReadOnlyList<ToolRepositorySource>? workspaces = null;

        List<ProvidedTool> here = [];

        foreach (var tool in installed)
        {
            // A linked tool is a folder on this machine, and one with nothing recorded
            // cannot be placed at all, so no workspace owns either of them.
            if (IsLocal(tool) || tool.Origin is not { } origin)
            {
                here.Add(new ProvidedTool(tool, []));
                continue;
            }

            var holders = inForce.Where(origin.Names).ToArray();

            // The global list provides it, so it is here whatever any workspace lists.
            if (Array.Exists(holders, source => source.Workspace is null))
            {
                here.Add(new ProvidedTool(tool, []));
                continue;
            }

            var naming = Naming(workspaces ??= _list.ReadWorkspaces(), origin);

            if (holders.Length > 0)
            {
                // The open workspace provides it. Named from its own entry when the second
                // read missed it, which is a workspace config that stopped parsing.
                here.Add(new ProvidedTool(tool, naming.Count > 0 ? naming : Naming(holders, origin)));
                continue;
            }

            // Nothing in force names the repository. A workspace that is not open does, so
            // the tool belongs there, and no list at all leaves the install to decide.
            if (naming.Count == 0 && origin.Global)
            {
                here.Add(new ProvidedTool(tool, []));
            }
        }

        return here;
    }

    /// <summary>
    /// Every workspace listing the same repository, so a tool two workspaces share says
    /// both. Matched by address, the way the catalogue names the workspaces behind an offer.
    /// </summary>
    private static IReadOnlyList<string> Naming(
        IReadOnlyList<ToolRepositorySource> lists,
        ToolOrigin origin) =>
    [
        .. lists
            .Where(origin.Names)
            .Select(source => source.Workspace)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal),
    ];

    private static bool IsLocal(InstalledTool tool) =>
        string.Equals(tool.Id.Source, ToolId.LocalSource, StringComparison.Ordinal);
}
