using Kitbash.Core.Workspaces;

namespace Kitbash.Tools;

/// <summary>
/// Which installed tools a workspace provides. A workspace's own repository list offers a
/// tool in that workspace alone, and installing it does not change that.
/// </summary>
public interface IProvidedTools
{
    /// <summary>
    /// The tools of <paramref name="installed"/> that belong on the page here, in the order
    /// they came in. Touches a disk, so call it off the UI thread.
    /// </summary>
    IReadOnlyList<ProvidedTool> Here(IReadOnlyList<InstalledTool> installed);

    /// <summary>
    /// The same rule for one named workspace, open or not, which is what a page listing
    /// every workspace asks. Touches a disk, so call it off the UI thread.
    /// </summary>
    IReadOnlyList<ProvidedTool> For(IReadOnlyList<InstalledTool> installed, Workspace workspace);
}
