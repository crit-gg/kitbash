using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Kitbash.Core.Git;

/// <summary>
/// A repository address git can clone, in either of the two forms a person copies out of
/// a hosting page. Parsing is the only way to make one, so an unchecked address cannot
/// reach a process.
/// </summary>
/// <example>
/// <c>GitRemote.TryParse("git@example.com:team/game.git", out var remote)</c>
/// </example>
public sealed partial class GitRemote
{
    private const string DotGit = ".git";

    private GitRemote(string address, string name)
    {
        Address = address;
        Name = name;
    }

    /// <summary>What is handed to git, exactly as it was written.</summary>
    public string Address { get; }

    /// <summary>The folder git would make, which is the last path segment without <c>.git</c>.</summary>
    public string Name { get; }

    /// <exception cref="FormatException">The address is not http, https or SSH.</exception>
    public static GitRemote Parse(string address) =>
        TryParse(address, out var remote)
            ? remote
            : throw new FormatException($"'{address}' is not a repository address.");

    /// <summary>
    /// True for http, https, ssh:// and the scp form such as <c>git@host:team/game.git</c>.
    /// Everything else, a local path included, is refused.
    /// </summary>
    public static bool TryParse(string? address, [NotNullWhen(true)] out GitRemote? remote)
    {
        remote = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var text = address.Trim();

        var path = UriPath(text) ?? ScpPath(text);

        if (path is null || FolderName(path) is not { } name)
        {
            return false;
        }

        remote = new GitRemote(text, name);

        return true;
    }

    /// <summary>The path part of a full address, or null when the scheme is not one git clones over here.</summary>
    private static string? UriPath(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
        {
            return null;
        }

        return uri.Scheme is "http" or "https" or "ssh" ? uri.AbsolutePath : null;
    }

    /// <summary>
    /// The path part of the scp form. A single character before the colon is a Windows
    /// drive letter, so a host is two characters or more.
    /// </summary>
    private static string? ScpPath(string text)
    {
        if (text.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        var match = Scp().Match(text);

        return match.Success ? match.Groups["path"].Value : null;
    }

    /// <summary>Null when nothing usable as a folder name is left.</summary>
    private static string? FolderName(string path)
    {
        var trimmed = path.TrimEnd('/');

        if (trimmed.EndsWith(DotGit, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^DotGit.Length].TrimEnd('/');
        }

        var last = trimmed.LastIndexOf('/');
        var name = last < 0 ? trimmed : trimmed[(last + 1)..];

        return name.Length > 0 && name.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && name is not ("." or "..")
            ? name
            : null;
    }

    [GeneratedRegex(@"^(?:[^@/\\:]+@)?[A-Za-z0-9._-]{2,}:(?<path>[^\\]+)$")]
    private static partial Regex Scp();
}
