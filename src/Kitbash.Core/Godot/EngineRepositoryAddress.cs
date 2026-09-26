using System.Diagnostics.CodeAnalysis;
using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

/// <summary>
/// Where an engine repository is, independent of the name a config gives it. Installs are
/// keyed by this, so renaming an entry orphans nothing. Its text form is
/// <c>github/crit-gg/godot-slopworks</c>, always lower case.
/// </summary>
public sealed record EngineRepositoryAddress
{
    /// <summary>GitHub releases, the one kind a config can name.</summary>
    public const string GitHub = "github";

    private EngineRepositoryAddress(string kind, string owner, string name)
    {
        Kind = kind;
        Owner = owner;
        Name = name;
    }

    public string Kind { get; }

    public string Owner { get; }

    public string Name { get; }

    /// <summary>The same text as <see cref="ToString"/>, split into folders under the engine directory.</summary>
    public IReadOnlyList<string> Segments => [Kind, Owner, Name];

    /// <summary>The repository's own page, which is what a person follows to read about it.</summary>
    public WebAddress Page => WebAddress.Parse($"https://github.com/{Owner}/{Name}");

    /// <summary>
    /// Reads the text form back. False for anything else, including a name GitHub would
    /// refuse, since the parts become folder names.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out EngineRepositoryAddress? address)
    {
        address = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('/');

        return parts.Length == 3 && TryCreate(parts[0], parts[1], parts[2], out address);
    }

    /// <summary>
    /// A GitHub repository from its web address, such as one copied out of a browser. Null
    /// for anything that is not a repository on github.com.
    /// </summary>
    public static EngineRepositoryAddress? ForGitHub(WebAddress url)
    {
        if (url.Value.Host is not ("github.com" or "www.github.com"))
        {
            return null;
        }

        var segments = url.Value.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 2)
        {
            return null;
        }

        // A url copied out of the address bar often ends in .git.
        var name = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];

        return TryCreate(GitHub, segments[0], name, out var address) ? address : null;
    }

    public override string ToString() => $"{Kind}/{Owner}/{Name}";

    private static bool TryCreate(
        string kind,
        string owner,
        string name,
        [NotNullWhen(true)] out EngineRepositoryAddress? address)
    {
        address = null;

        if (!string.Equals(kind, GitHub, StringComparison.OrdinalIgnoreCase)
            || !IsName(owner)
            || !IsName(name))
        {
            return false;
        }

        // GitHub ignores case in both parts, so folding them keeps one repository from
        // becoming two folders.
        address = new EngineRepositoryAddress(
            GitHub,
            owner.ToLowerInvariant(),
            name.ToLowerInvariant());

        return true;
    }

    /// <summary>
    /// The characters GitHub allows in an owner or a repository name, all of which every
    /// platform takes in a folder name. A lone dot or two would name a folder above.
    /// </summary>
    private static bool IsName(string text) =>
        text.Length > 0
        && text is not ("." or "..")
        && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
