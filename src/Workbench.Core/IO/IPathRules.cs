namespace Workbench.Core.IO;

/// <summary>
/// How this platform decides whether two paths mean the same place. Windows ignores
/// case and Linux does not, so a comparison that is right on one is wrong on the other.
/// </summary>
/// <remarks>
/// Paths are compared as written, after being made absolute. A symbolic link is not
/// followed, so two names for one directory compare as different places.
/// </remarks>
public interface IPathRules
{
    /// <summary>Whether both paths name the same directory.</summary>
    bool AreSame(string left, string right);

    /// <summary>
    /// Whether <paramref name="candidate"/> sits somewhere under
    /// <paramref name="container"/>. A directory does not contain itself.
    /// </summary>
    bool Contains(string container, string candidate);
}
