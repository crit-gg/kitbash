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

    /// <summary>
    /// Makes a file runnable, where that means anything.
    /// </summary>
    /// <remarks>
    /// Nothing on Windows decides whether a file runs, so this does nothing there. On Unix
    /// it is not a rare guard. <c>ZipFile.ExtractToDirectory</c> carries an archive's mode
    /// across, but unpacking entry by entry does not, and the prefix strip and the two
    /// extraction guards mean this app unpacks by hand. So the installer calls this for
    /// every entry the archive recorded as executable. Measured: without it, an install
    /// completes and holds an editor at 0644 that nothing can find or run.
    /// </remarks>
    void MakeExecutableFile(string path);

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

    /// <summary>Removes a file, doing nothing when it is already gone.</summary>
    void DeleteFile(string path);

    /// <summary>
    /// Opens a file to read. For content too large to hold as text, such as hashing an
    /// archive or reading one entry at a time out of it.
    /// </summary>
    Stream OpenRead(string path);

    /// <summary>Creates or replaces a file and opens it to write.</summary>
    Stream Create(string path);
}
