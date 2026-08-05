using Kitbash.Core.IO;

namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// A bounded walk, written here rather than on IFileSystem because its recursive
/// enumeration has no depth limit and no skip list.
/// </summary>
internal sealed class WorkspaceFileFinder : IWorkspaceFileFinder
{
    /// <summary>Never worth walking, and large enough to be worth naming.</summary>
    private static readonly string[] Skipped = ["node_modules"];

    private readonly IFileSystem _fileSystem;

    public WorkspaceFileFinder(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public IReadOnlyList<string> Find(string root, IReadOnlyList<string> extensions, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        if (string.IsNullOrWhiteSpace(root) || extensions.Count == 0 || maxDepth < 1)
        {
            return [];
        }

        List<string> found = [];
        Walk(root, extensions, maxDepth, found);

        return found;
    }

    private void Walk(string directory, IReadOnlyList<string> extensions, int depthLeft, List<string> found)
    {
        foreach (var file in _fileSystem.EnumerateFiles(directory, recursive: false))
        {
            // An extension describes the shape of a name, so it matches ignoring case on
            // both platforms, the way a path field's filters do.
            if (extensions.Any(extension => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(file);
            }
        }

        if (depthLeft <= 1)
        {
            return;
        }

        foreach (var child in _fileSystem.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(child);

            if (name.StartsWith('.') || Skipped.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            Walk(child, extensions, depthLeft - 1, found);
        }
    }
}
