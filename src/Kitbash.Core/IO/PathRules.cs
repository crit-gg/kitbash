namespace Kitbash.Core.IO;

/// <summary>
/// The comparison every platform shares. A subclass supplies only whether case counts.
/// </summary>
internal abstract class PathRules : IPathRules
{
    protected abstract StringComparison Comparison { get; }

    public bool AreSame(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), Comparison);

    public bool Contains(string container, string candidate)
    {
        var root = Normalize(container);
        var inner = Normalize(candidate);

        if (string.Equals(root, inner, Comparison))
        {
            return false;
        }

        // The separator is part of the test, otherwise a sibling whose name merely
        // starts with the container's name would look like a child of it.
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return inner.StartsWith(prefix, Comparison);
    }

    private static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // GetFullPath resolves a relative path and settles the separators, including
        // the alternate one Windows accepts. It does not follow a symbolic link.
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
    }
}
