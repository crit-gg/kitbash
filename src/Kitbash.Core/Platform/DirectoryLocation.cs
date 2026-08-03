namespace Kitbash.Core.Platform;

/// <summary>
/// A well formed absolute directory path. Whether it exists is checked when it is
/// opened, because the filesystem can change after this value is made.
/// </summary>
public readonly record struct DirectoryLocation
{
    private DirectoryLocation(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>Requires a rooted path, so the result never depends on the current directory.</summary>
    public static DirectoryLocation Parse(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!Path.IsPathRooted(path))
        {
            throw new ArgumentException($"'{path}' must be an absolute path.", nameof(path));
        }

        return new DirectoryLocation(Path.GetFullPath(path));
    }

    public override string ToString() => Value;
}
