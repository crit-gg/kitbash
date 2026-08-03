using System.Text;
using Workbench.Core.IO;
using Workbench.Core.Settings;
using Workbench.Core.Settings.Schema;

namespace Workbench.Core.Workspaces;

/// <summary>
/// Writes the workspace's team config file the first time it is missing.
/// </summary>
/// <remarks>
/// <para>
/// **The file is entirely comments, so it parses as an empty document.** Nothing is set
/// and nothing is overridden by it existing. It is there to be read and edited, since
/// until a settings window lands hand editing is the only way to set any of this, and an
/// empty file answers no questions.
/// </para>
/// <para>
/// **Every setting in it is written from its descriptor**, so a key added to a workspace
/// schema appears here without anybody remembering to add it. Only <c>workspace.name</c>
/// is written by hand, because it has no descriptor and lives as a constant on
/// <see cref="WorkspaceNameResolver"/>.
/// </para>
/// <para>
/// **A write does not keep these comments.** Settings are written back from the model, so
/// the first time anything saves a value here the commentary goes. That is the existing
/// rule rather than something this adds, and the trade is worth it: the file is useful
/// now, and by the time something is writing it a person has a window to do it in.
/// </para>
/// </remarks>
internal sealed class WorkspaceScaffold : IWorkspaceScaffold
{
    /// <summary>Where a comment line is wrapped, chosen to sit inside 80 with its hash.</summary>
    private const int Width = 74;

    private readonly IFileSystem _fileSystem;
    private readonly WorkspaceGodotSettingsSchema _godot;

    public WorkspaceScaffold(IFileSystem fileSystem, WorkspaceGodotSettingsSchema godot)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(godot);

        _fileSystem = fileSystem;
        _godot = godot;
    }

    public void Ensure(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var paths = new WorkspacePaths(root);
        var file = paths.FileFor(SettingsScope.Global, SettingsLayer.TeamShared);

        if (_fileSystem.FileExists(file))
        {
            return;
        }

        try
        {
            _fileSystem.CreateDirectory(paths.TeamSharedDirectory);
            _fileSystem.WriteAllText(file, Template());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A workspace works without this file. Somebody else's checkout, or a read
            // only mount, is not a reason to stop.
        }
    }

    private string Template()
    {
        var text = new StringBuilder();

        Comment(text, "Workbench settings for this workspace. This file is shared, so it "
            + "belongs in version control and applies to everybody who opens the workspace.");
        text.AppendLine("#");
        Comment(text, "Personal overrides go in .workbench/user/workbench.toml, which is "
            + "ignored by git and beats this file key by key.");
        text.AppendLine("#");
        Comment(text, "Everything below is commented out and showing what it would be if "
            + "nothing said otherwise. Uncomment a line to set it.");
        text.AppendLine();

        // Written by hand, since the name has no descriptor to read this from.
        Section(
            text,
            "Name",
            "What this workspace is called in Workbench. Blank reads the Godot project's "
            + "own name, and then the folder name.",
            WorkspaceNameResolver.NameKey,
            string.Empty);

        foreach (var section in _godot.Page.Sections)
        {
            foreach (var row in section.Rows.OfType<ISettingDescriptor>())
            {
                Section(text, row.Name, row.Description, row.Key, row.Default as string ?? string.Empty);
            }
        }

        // One trailing newline rather than the blank line each section leaves behind.
        return text.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void Section(
        StringBuilder text, string name, string description, string key, string fallback)
    {
        text.AppendLine($"# {name}");
        Comment(text, description);
        text.AppendLine($"# {key} = \"{fallback}\"");
        text.AppendLine();
    }

    /// <summary>One paragraph as hash prefixed lines, wrapped on words.</summary>
    private static void Comment(StringBuilder text, string paragraph)
    {
        var line = new StringBuilder();

        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > Width)
            {
                text.AppendLine($"# {line}");
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            text.AppendLine($"# {line}");
        }
    }
}
