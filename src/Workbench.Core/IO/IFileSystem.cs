namespace Workbench.Core.IO;

/// <summary>
/// Filesystem access. Everything that touches disk goes through this so callers stay
/// substitutable.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>Whether the path is a file that can be run.</summary>
    bool IsExecutableFile(string path);

    void CreateDirectory(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    void MoveFile(string sourcePath, string destinationPath, bool overwrite);

    /// <summary>
    /// When the path was last written, or null when it is not there. Used to read a
    /// timestamp off a file whose contents do not matter, such as when a repository last
    /// fetched.
    /// </summary>
    DateTimeOffset? GetLastWriteTime(string path);

    /// <summary>Immediate subdirectories, ordered by name so a search is repeatable.</summary>
    IReadOnlyList<string> EnumerateDirectories(string path);
}
