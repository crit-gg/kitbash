using System.Text.RegularExpressions;
using Workbench.Core.IO;

namespace Workbench.Core.Godot;

/// <summary>
/// Reads a <c>project.godot</c> by line.
/// </summary>
internal sealed partial class GodotProjectReader : IGodotProjectReader
{
    public const string ProjectFileName = "project.godot";

    /// <summary>How deep to look for a project before giving up.</summary>
    private const int MaxDepth = 4;

    private static readonly string[] SkippedDirectories =
        [".git", ".godot", ".import", ".workbench", "node_modules", "bin", "obj"];

    private readonly IFileSystem _fileSystem;

    public GodotProjectReader(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public GodotProject? Find(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        return Search(root, depth: 0) is { } file ? Read(file) : null;
    }

    public GodotProject Read(string projectFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFile);

        var project = new GodotProject
        {
            File = projectFile,
            Directory = Path.GetDirectoryName(projectFile) ?? projectFile,
        };

        string text;

        try
        {
            text = _fileSystem.ReadAllText(projectFile);
        }
        catch (IOException)
        {
            return project;
        }
        catch (UnauthorizedAccessException)
        {
            return project;
        }

        var section = string.Empty;
        var name = string.Empty;
        var configVersion = 0;
        var usesDotnet = false;
        EngineVersionPattern? version = null;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            // Godot writes its header as semicolon comments.
            if (trimmed.Length == 0 || trimmed[0] is ';' or '#')
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1].Trim();

                // Godot 4 writes [dotnet] and Godot 3 wrote [mono]. Either says C#.
                usesDotnet |= section is "dotnet" or "mono";

                continue;
            }

            var split = trimmed.IndexOf('=');

            if (split <= 0)
            {
                continue;
            }

            var key = trimmed[..split].Trim();
            var value = trimmed[(split + 1)..].Trim();

            if (section.Length == 0 && key == "config_version")
            {
                _ = int.TryParse(value, out configVersion);

                continue;
            }

            if (section != "application")
            {
                continue;
            }

            switch (key)
            {
                case "config/name":
                    name = Unquote(value);

                    break;

                case "config/features":
                    foreach (var feature in Strings(value))
                    {
                        // Godot writes the version first and the flags after it, but
                        // nothing promises that, so this takes whichever entry is a
                        // version and reads the rest as flags.
                        if (version is null && EngineVersionPattern.TryParse(feature, out var parsed))
                        {
                            version = parsed;
                        }
                        else if (feature.Equals("C#", StringComparison.OrdinalIgnoreCase))
                        {
                            usesDotnet = true;
                        }
                    }

                    break;
            }
        }

        return project with
        {
            Name = name,
            ConfigVersion = configVersion,
            Version = version,
            UsesDotnet = usesDotnet,
        };
    }

    private string? Search(string directory, int depth)
    {
        var candidate = Path.Combine(directory, ProjectFileName);

        if (_fileSystem.FileExists(candidate))
        {
            return candidate;
        }

        if (depth >= MaxDepth)
        {
            return null;
        }

        foreach (var child in _fileSystem.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(child);

            if (name.StartsWith('.') || SkippedDirectories.Contains(name))
            {
                continue;
            }

            if (Search(child, depth + 1) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    /// <summary>The quoted strings in a value such as <c>PackedStringArray("4.7", "C#")</c>.</summary>
    private static IEnumerable<string> Strings(string value)
    {
        foreach (Match match in Quoted().Matches(value))
        {
            yield return match.Groups[1].Value;
        }
    }

    [GeneratedRegex("\"([^\"]*)\"")]
    private static partial Regex Quoted();
}
