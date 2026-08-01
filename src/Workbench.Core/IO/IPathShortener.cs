namespace Workbench.Core.IO;

/// <summary>Shortens a path so it fits a fixed width without the control growing.</summary>
public interface IPathShortener
{
    /// <summary>
    /// Keeps the end of the path, which is the part that identifies it, and the root,
    /// which says where it lives. Everything between collapses. The result is written
    /// the way the running platform writes a path, so it is for display only.
    /// </summary>
    string Shorten(string path, int maxLength);
}
