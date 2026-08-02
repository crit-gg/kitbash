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

    /// <summary>
    /// Files under a path, ordered by name. A folder that cannot be read counts as empty,
    /// the way <see cref="EnumerateDirectories"/> treats one.
    /// </summary>
    IReadOnlyList<string> EnumerateFiles(string path, bool recursive);

    /// <summary>How large a file is, or zero when it is not there or cannot be read.</summary>
    long GetFileLength(string path);

    /// <summary>
    /// Removes a directory and everything under it. Does nothing when it is already gone,
    /// since the caller wanted it absent rather than wanted to be the one to remove it.
    /// </summary>
    void DeleteDirectory(string path);
}
