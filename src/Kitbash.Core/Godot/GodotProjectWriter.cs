using System.Text;
using Kitbash.Core.IO;

namespace Kitbash.Core.Godot;

/// <summary>
/// Writes a new Godot project the way Godot's project dialog writes one.
/// </summary>
internal sealed class GodotProjectWriter : IGodotProjectWriter
{
    /// <summary>Godot's FileAccess writes LF on every platform, and these are its files.</summary>
    private const string Newline = "\n";

    /// <summary>ProjectSettings::CONFIG_VERSION in Godot 4. Godot 3 wrote 4.</summary>
    private const int ConfigVersion = 5;

    /// <summary>
    /// The DefaultProjectIcon from the editor icon set, which is what Godot stores as
    /// icon.svg. Copied byte for byte, since a project's icon is not ours to redraw.
    /// </summary>
    private const string Icon =
        """<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128"><rect width="124" height="124" x="2" y="2" fill="#363d52" stroke="#212532" stroke-width="4" rx="14"/><g fill="#fff" transform="translate(12.322 12.322)scale(.101)"><path d="M105 673v33q407 354 814 0v-33z"/><path fill="#478cbf" d="m105 673 152 14q12 1 15 14l4 67 132 10 8-61q2-11 15-15h162q13 4 15 15l8 61 132-10 4-67q3-13 15-14l152-14V427q30-39 56-81-35-59-83-108-43 20-82 47-40-37-88-64 7-51 8-102-59-28-123-42-26 43-46 89-49-7-98 0-20-46-46-89-64 14-123 42 1 51 8 102-48 27-88 64-39-27-82-47-48 49-83 108 26 42 56 81zm0 33v39c0 276 813 276 814 0v-39l-134 12-5 69q-2 10-14 13l-162 11q-12 0-16-11l-10-65H446l-10 65q-4 11-16 11l-162-11q-12-3-14-13l-5-69z"/><path d="M483 600c0 34 58 34 58 0v-86c0-34-58-34-58 0z"/><circle cx="725" cy="526" r="90"/><circle cx="299" cy="526" r="90"/></g><g fill="#414042" transform="translate(12.322 12.322)scale(.101)"><circle cx="307" cy="532" r="60"/><circle cx="717" cy="532" r="60"/></g></svg>""";

    private readonly IFileSystem _fileSystem;
    private readonly IEngineFiles _engineFiles;

    public GodotProjectWriter(IFileSystem fileSystem, IEngineFiles engineFiles)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(engineFiles);

        _fileSystem = fileSystem;
        _engineFiles = engineFiles;
    }

    public void Write(NewGodotProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        _fileSystem.WriteAllText(Path.Combine(project.Directory, "project.godot"), Settings(project));
        _fileSystem.WriteAllText(Path.Combine(project.Directory, "icon.svg"), Icon);

        WriteEditorConfig(project.Directory);
    }

    public void WriteGitFiles(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _fileSystem.WriteAllText(
            Path.Combine(directory, ".gitignore"),
            Lines("# Godot 4+ specific ignores", ".godot/", "/android/"));

        _fileSystem.WriteAllText(
            Path.Combine(directory, ".gitattributes"),
            Lines("# Normalize EOL for all files that Git considers text files.", "* text=auto eol=lf"));
    }

    /// <summary>
    /// An editor outside Godot reads this and writes UTF-8. Godot hides it, since it is
    /// not a file anyone opens.
    /// </summary>
    private void WriteEditorConfig(string directory)
    {
        var path = Path.Combine(directory, ".editorconfig");

        _fileSystem.WriteAllText(path, Lines("root = true", string.Empty, "[*]", "charset = utf-8"));
        _engineFiles.Hide(path);
    }

    /// <summary>
    /// The whole of <c>project.godot</c>. The header, the key order and the grouping are
    /// what ProjectSettings::_save_settings_text produces for a project made from the
    /// dialog, so a project made here reads the same as one made in the editor.
    /// </summary>
    private static string Settings(NewGodotProject project)
    {
        var text = new StringBuilder();

        text.Append(Lines(
            "; Engine configuration file.",
            "; It's best edited using the editor UI and not directly,",
            "; since the parameters that go here are not all obvious.",
            ";",
            "; Format:",
            ";   [section] ; section goes between []",
            ";   param=value ; assign values to parameters",
            string.Empty,
            $"config_version={ConfigVersion}",
            string.Empty));

        Section(text, "application", first: true);
        text.Append(Lines(
            $"config/name={Quote(project.Name.Trim())}",
            $"config/features={Features(project)}",
            "config/icon=\"res://icon.svg\""));

        // EditorNode::get_initial_settings, which the dialog merges over its own.
        Section(text, "display");
        text.Append(Lines(
            "window/stretch/mode=\"canvas_items\"",
            "window/stretch/aspect=\"expand\""));

        Section(text, "physics");
        text.Append(Lines("3d/physics_engine=\"Jolt Physics\""));

        Section(text, "rendering");
        text.Append(Lines($"renderer/rendering_method={Quote(MethodOf(project.Renderer))}"));

        // Compatibility is the one renderer that also overrides the mobile default, since
        // the mobile renderer would otherwise take over there.
        if (project.Renderer == GodotRenderer.Compatibility)
        {
            text.Append(Lines("renderer/rendering_method.mobile=\"gl_compatibility\""));
        }

        text.Append(Lines("rendering_device/driver.windows=\"d3d12\""));

        return text.ToString();
    }

    private static void Section(StringBuilder text, string name, bool first = false)
    {
        if (!first)
        {
            text.Append(Newline);
        }

        text.Append($"[{name}]{Newline}{Newline}");
    }

    /// <summary>
    /// <c>config/features</c>, which is the engine branch plus the renderer's own name,
    /// sorted. Godot sorts the array before it writes it.
    /// </summary>
    private static string Features(NewGodotProject project)
    {
        string[] features = [$"{project.Engine.Major}.{project.Engine.Minor}", NameOf(project.Renderer)];
        Array.Sort(features, StringComparer.Ordinal);

        return $"PackedStringArray({string.Join(", ", features.Select(Quote))})";
    }

    /// <summary>The value of <c>rendering/renderer/rendering_method</c>.</summary>
    private static string MethodOf(GodotRenderer renderer) => renderer switch
    {
        GodotRenderer.Mobile => "mobile",
        GodotRenderer.Compatibility => "gl_compatibility",
        _ => "forward_plus",
    };

    /// <summary>How a renderer is named inside <c>config/features</c>.</summary>
    private static string NameOf(GodotRenderer renderer) => renderer switch
    {
        GodotRenderer.Mobile => "Mobile",
        GodotRenderer.Compatibility => "GL Compatibility",
        _ => "Forward Plus",
    };

    // Godot writes a string through VariantWriter, which escapes the backslash and the
    // quote. A project name cannot hold anything else that needs escaping.
    private static string Quote(string value) =>
        $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string Lines(params string[] lines) =>
        string.Concat(lines.Select(line => line + Newline));
}
