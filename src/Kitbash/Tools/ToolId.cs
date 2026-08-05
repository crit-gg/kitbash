namespace Kitbash.Tools;

/// <summary>
/// What a tool is called on this machine. A manifest names the tool and the launcher
/// puts the source in front of it, so github.foundry and gitea.foundry are two tools.
/// </summary>
public readonly record struct ToolId
{
    /// <summary>Separates the source from the name.</summary>
    private const char Separator = '.';

    /// <summary>
    /// The source a tool takes when a person points at a folder on this machine. No
    /// repository type may be called this, since the two would then share a namespace.
    /// </summary>
    public const string LocalSource = "local";

    private ToolId(string? source, string name)
    {
        Source = source;
        Name = name;
    }

    /// <summary>Where the tool came from, or null for one that was placed by hand.</summary>
    public string? Source { get; }

    /// <summary>The name the manifest gives, which is not unique on its own.</summary>
    public string Name { get; }

    /// <summary>The whole id, which is the folder the tool is installed in.</summary>
    public string Value => Source is null ? Name : Source + Separator + Name;

    /// <summary>Builds the id the launcher stores for a tool one repository offers.</summary>
    /// <exception cref="ArgumentException">Either part breaks the rules.</exception>
    public static ToolId For(string source, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!IsLegalPart(source) || !IsLegalPart(name))
        {
            throw new ArgumentException($"'{source}{Separator}{name}' is not a tool id.");
        }

        return new ToolId(source, name);
    }

    /// <summary>Reads an id back, such as the name of a folder under the tools directory.</summary>
    public static bool TryParse(string? value, out ToolId id)
    {
        id = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(Separator);

        if (parts.Length > 2 || Array.Exists(parts, part => !IsLegalPart(part)))
        {
            return false;
        }

        id = parts.Length == 2 ? new ToolId(parts[0], parts[1]) : new ToolId(null, parts[0]);

        return true;
    }

    public override string ToString() => Value;

    /// <summary>
    /// Lower case letters, digits and dashes, not starting or ending with a dash. The id
    /// is a folder name on a case sensitive filesystem and on one that ignores case, so a
    /// capital would mean one tool on Linux and the same tool twice on Windows.
    /// </summary>
    private static bool IsLegalPart(string part)
    {
        if (part.Length == 0 || part[0] == '-' || part[^1] == '-')
        {
            return false;
        }

        foreach (var character in part)
        {
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
