using System.Text;
using Kitbash.Core.IO;
using Kitbash.Core.Settings;
using Kitbash.Core.Settings.Schema;

namespace Kitbash.Core.Workspaces;

/// <summary>
/// Writes the workspace's team config file the first time it is missing.
/// </summary>
internal sealed class WorkspaceScaffold : IWorkspaceScaffold
{
    /// <summary>Where a comment line is wrapped, chosen to sit inside 80 with its hash.</summary>
    private const int Width = 74;

    /// <summary>The table the links block follows.</summary>
    private const string WorkspaceTable = "workspace";

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

    public void EnsureUserLayerIgnored(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var paths = new WorkspacePaths(root);
        var file = Path.Combine(paths.KitbashDirectory, ".gitignore");

        if (_fileSystem.FileExists(file))
        {
            return;
        }

        try
        {
            _fileSystem.CreateDirectory(paths.KitbashDirectory);
            _fileSystem.WriteAllText(
                file,
                $"# Personal Kitbash settings. Not shared.{Environment.NewLine}user/{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The settings still work. A checkout that cannot be written is not a reason
            // to stop, and the same rule is in the repository's own ignore file for most.
        }
    }

    private string Template()
    {
        var text = new StringBuilder();

        Comment(text, "Kitbash settings for this workspace. This file is shared, so it "
            + "belongs in version control and applies to everybody who opens the workspace.");
        text.AppendLine("#");
        Comment(text, "Personal overrides go in .kitbash/user/kitbash.toml, which is "
            + "ignored by git and beats this file key by key.");
        text.AppendLine("#");
        Comment(text, "Every setting below is commented out and showing what it would be "
            + "if nothing said otherwise. Uncomment a line to set it.");
        text.AppendLine();

        var table = string.Empty;
        var links = false;

        foreach (var row in Rows())
        {
            if (!string.Equals(row.Table, table, StringComparison.Ordinal))
            {
                // The links belong with the rest of the workspace, so they go in as that
                // table is left rather than at the end under whatever table came last.
                links = links || Leaving(text, table);
                table = row.Table;

                if (table.Length > 0)
                {
                    text.AppendLine($"[{table}]");
                    text.AppendLine();
                }
            }

            Section(text, row);
        }

        if (!links)
        {
            Leaving(text, table);
        }

        // One trailing newline rather than the blank line each section leaves behind.
        return text.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <summary>
    /// Every setting the file shows, in the order it is written, each split into the table
    /// it belongs to and the key inside it. A key of one segment has no table and is
    /// written above the first header, where a root key has to go.
    /// </summary>
    private IEnumerable<Row> Rows()
    {
        // Written by hand, since the name has no descriptor to read this from.
        yield return Row.For(
            "Name",
            "What this workspace is called in Kitbash. Blank reads the Godot project's "
            + "own name, and then the folder name.",
            WorkspaceNameResolver.NameKey,
            string.Empty);

        foreach (var section in _godot.Page.Sections)
        {
            foreach (var descriptor in section.Rows.OfType<ISettingDescriptor>())
            {
                yield return Row.For(
                    descriptor.Name,
                    descriptor.Description,
                    descriptor.Key,
                    descriptor.Default as string ?? string.Empty);
            }
        }
    }

    /// <summary>Writes the links block as the workspace table is left.</summary>
    /// <returns>Whether it was written, so it is never written twice.</returns>
    private static bool Leaving(StringBuilder text, string table)
    {
        if (!string.Equals(table, WorkspaceTable, StringComparison.Ordinal))
        {
            return false;
        }

        Links(text);

        return true;
    }

    /// <summary>
    /// The links the launcher draws above the tools. The header is commented too, unlike
    /// every setting above, because a live [[workspace.links]] with no keys under it is a
    /// link with no label and no address rather than a table that sets nothing.
    /// </summary>
    private static void Links(StringBuilder text)
    {
        text.AppendLine("# Workspace links");
        Comment(text, "Links for this workspace, shown above the tools. Repeat the four "
            + "lines for each one. The icon is optional.");
        text.AppendLine("#");
        text.AppendLine("# [[workspace.links]]");
        text.AppendLine("# label = \"Design docs\"");
        text.AppendLine("# url = \"https://example.com/design\"");
        text.AppendLine("# icon = \"file\"");
        text.AppendLine();
    }

    private static void Section(StringBuilder text, Row row)
    {
        text.AppendLine($"# {row.Name}");
        Comment(text, row.Description);
        text.AppendLine($"# {row.Key} = \"{row.Fallback}\"");
        text.AppendLine();
    }

    private readonly record struct Row(string Name, string Description, string Table, string Key, string Fallback)
    {
        public static Row For(string name, string description, string key, string fallback)
        {
            var at = key.LastIndexOf('.');

            return at < 0
                ? new Row(name, description, string.Empty, key, fallback)
                : new Row(name, description, key[..at], key[(at + 1)..], fallback);
        }
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
