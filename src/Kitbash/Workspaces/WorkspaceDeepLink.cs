using System.Diagnostics.CodeAnalysis;
using Kitbash.Core.Godot;
using Kitbash.Core.Platform;

namespace Kitbash.Workspaces;

/// <summary>
/// A <c>kitbash://workspace/name/action</c> link, taken apart. The name is what
/// <c>IWorkspaceLinkNames</c> matches, and no action means open the workspace.
/// </summary>
/// <example>
/// <c>kitbash://workspace/Slopworks/edit?build=false</c> opens the editor without building.
/// </example>
public sealed class WorkspaceDeepLink
{
    public const string Verb = "workspace";

    private const string BuildKey = "build";

    private static readonly (string Word, GodotLaunchMode Mode)[] Actions =
    [
        ("edit", GodotLaunchMode.Editor),
        ("play", GodotLaunchMode.Play),
        ("rebuild", GodotLaunchMode.Rebuild),
    ];

    private WorkspaceDeepLink(string name, GodotLaunchMode? mode, bool builds)
    {
        Name = name;
        Mode = mode;
        Builds = builds;
    }

    public string Name { get; }

    /// <summary>What to start, or null to open the workspace and start nothing.</summary>
    public GodotLaunchMode? Mode { get; }

    /// <summary>False only for an edit link carrying <c>build=false</c>.</summary>
    public bool Builds { get; }

    /// <summary>True when the link is a workspace link with a name and a known action.</summary>
    public static bool TryParse(DeepLink link, [NotNullWhen(true)] out WorkspaceDeepLink? parsed)
    {
        ArgumentNullException.ThrowIfNull(link);

        parsed = null;

        if (link.Verb != Verb || link.Segments.Count is 0 or > 2 || string.IsNullOrWhiteSpace(link.First))
        {
            return false;
        }

        GodotLaunchMode? mode = null;

        if (link.Segments.Count == 2)
        {
            var word = link.Segments[1];
            var match = Actions.Where(action => string.Equals(action.Word, word, StringComparison.OrdinalIgnoreCase));

            if (!match.Any())
            {
                return false;
            }

            mode = match.First().Mode;
        }

        var builds = mode != GodotLaunchMode.Editor
            || !string.Equals(link.Value(BuildKey), "false", StringComparison.OrdinalIgnoreCase);

        parsed = new WorkspaceDeepLink(link.First, mode, builds);

        return true;
    }

    /// <summary>The text of a link. <paramref name="builds"/> is written only for an edit.</summary>
    public static string Write(string name, GodotLaunchMode? mode, bool builds = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (mode is not { } wanted)
        {
            return DeepLink.Write(Verb, [name], []);
        }

        var word = Actions.First(action => action.Mode == wanted).Word;

        return DeepLink.Write(
            Verb,
            [name, word],
            wanted == GodotLaunchMode.Editor && !builds ? [new(BuildKey, "false")] : []);
    }
}
