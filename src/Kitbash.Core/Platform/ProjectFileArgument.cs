using System.Diagnostics.CodeAnalysis;

namespace Kitbash.Core.Platform;

/// <summary>
/// A <c>project.godot</c> a desktop asked this app to open, as a full path. Whether the
/// file is still there is checked when it opens.
/// </summary>
public sealed record ProjectFileArgument
{
    /// <summary>The one file name Godot gives a project. Godot writes it in lower case.</summary>
    public const string FileName = "project.godot";

    private const string FileScheme = "file:";

    private ProjectFileArgument(string path) => Path = path;

    public string Path { get; }

    /// <summary>
    /// Takes a full path or a <c>file:</c> url, since the desktop entry specification lets
    /// a file manager pass either for <c>%u</c>. A relative path is refused.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out ProjectFileArgument? argument)
    {
        argument = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var path = text.Trim();

        if (path.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(path, UriKind.Absolute, out var url) || !url.IsFile)
            {
                return false;
            }

            path = url.LocalPath;
        }

        if (!System.IO.Path.IsPathFullyQualified(path)
            || !string.Equals(System.IO.Path.GetFileName(path), FileName, StringComparison.Ordinal))
        {
            return false;
        }

        argument = new ProjectFileArgument(path);

        return true;
    }

    public override string ToString() => Path;
}
