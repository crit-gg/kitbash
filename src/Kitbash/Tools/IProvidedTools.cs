namespace Kitbash.Tools;

/// <summary>
/// Which installed tools the open workspace provides. A workspace's own repository list
/// offers a tool in that workspace alone, and installing it does not change that.
/// </summary>
public interface IProvidedTools
{
    /// <summary>
    /// The tools of <paramref name="installed"/> that belong on the page here, in the order
    /// they came in. Touches a disk, so call it off the UI thread.
    /// </summary>
    IReadOnlyList<ProvidedTool> Here(IReadOnlyList<InstalledTool> installed);
}
