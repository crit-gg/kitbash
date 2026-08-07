using Kitbash.Core.Platform;

namespace Kitbash.Tools;

/// <summary>
/// The repository an installed version came from. Which workspaces provide a tool is still
/// worked out from the lists as they stand, so this is only what the install recorded.
/// </summary>
/// <param name="Global">
/// The global list is what offered the version. Nothing can derive this later, and it is
/// what decides whether a tool survives its repository leaving every list.
/// </param>
public sealed record ToolOrigin(WebAddress Url, bool Global)
{
    /// <summary>What an install records for the source it took the version from.</summary>
    public static ToolOrigin For(ToolRepositorySource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ToolOrigin(source.Url, source.Workspace is null);
    }

    /// <summary>Whether a repository list entry names the same repository as this one.</summary>
    public bool Names(ToolRepositorySource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return string.Equals(source.Url.ToString(), Url.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
