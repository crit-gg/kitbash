using System.Text.RegularExpressions;
using Workbench.Core.IO;

namespace Workbench.Core.Godot;

/// <summary>
/// Reads whether a project has been imported, from the sidecars rather than the cache.
/// </summary>
internal sealed partial class GodotImports : IGodotImports
{
    /// <summary>
    /// How deep the walk goes. Assets sit well below the top, under <c>addons</c> for
    /// one, so this is generous. It only guards an unusually deep tree or a link loop.
    /// </summary>
    private const int MaxDepth = 24;

    private readonly IFileSystem _fileSystem;

    public GodotImports(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public bool NeedsImport(GodotProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return Walk(project.Directory, project.Directory, depth: 0);
    }

    private bool Walk(string root, string directory, int depth)
    {
        IEnumerable<string> files;

        try
        {
            files = _fileSystem.EnumerateFiles(directory, recursive: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A folder that will not open cannot be proven imported either way. Saying no
            // costs an import that was not needed, saying yes risks running a project
            // with no resources, so this takes the cheaper mistake.
            return true;
        }

        foreach (var file in files)
        {
            if (!file.EndsWith(".import", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Missing(root, file))
            {
                return true;
            }
        }

        if (depth >= MaxDepth)
        {
            return false;
        }

        IEnumerable<string> children;

        try
        {
            children = _fileSystem.EnumerateDirectories(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        foreach (var child in children)
        {
            // The cache and the version control folder hold no sidecars, and the cache is
            // where the outputs land, so walking it would be walking the answer.
            if (Path.GetFileName(child).StartsWith('.'))
            {
                continue;
            }

            if (Walk(root, child, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private bool Missing(string root, string sidecar)
    {
        string text;

        try
        {
            text = _fileSystem.ReadAllText(sidecar);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        foreach (Match match in Generated().Matches(text))
        {
            var relative = match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);

            if (!_fileSystem.FileExists(Path.Combine(root, relative)))
            {
                return true;
            }
        }

        return false;
    }

    // Only the generated outputs count. A sidecar also names its source file and its uid,
    // and neither says whether an import has run. Godot 4 writes them under
    // .godot/imported and Godot 3 wrote them under .import.
    [GeneratedRegex(@"""res://(\.godot/imported/[^""]+|\.import/[^""]+)""")]
    private static partial Regex Generated();
}
