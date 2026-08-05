namespace Kitbash.Tools;

/// <summary>
/// What the repositories are offering. A repository that cannot be reached contributes
/// nothing and says nothing, so an unreachable one and an empty one look identical.
/// </summary>
public interface IToolCatalogue
{
    /// <summary>
    /// One offer per id, whether or not that id is installed, so an installed tool can be
    /// compared against what its repository offers now. Touches a network, so it is slow
    /// and belongs off the UI thread.
    /// </summary>
    /// <param name="refresh">Asks every repository now rather than using what was cached.</param>
    Task<IReadOnlyList<OfferedTool>> ReadAsync(bool refresh, CancellationToken cancellationToken);
}
