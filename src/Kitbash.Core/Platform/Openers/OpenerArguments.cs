using System.Text;

namespace Kitbash.Core.Platform.Openers;

/// <summary>
/// The template is split before the token is filled in, so a workspace path with spaces
/// in it stays one argument.
/// </summary>
internal sealed class OpenerArguments : IOpenerArguments
{
    public IReadOnlyList<string> Build(string? template, string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        if (string.IsNullOrWhiteSpace(template))
        {
            return [workspaceRoot];
        }

        var parts = Split(template);

        if (parts.Count == 0)
        {
            return [workspaceRoot];
        }

        var names = parts.Any(part => part.Contains(IOpenerArguments.WorkspaceToken, StringComparison.Ordinal));

        // A template with no token still means the workspace, since somebody writing
        // --new-window should not have to learn the token to get anywhere.
        if (!names)
        {
            return [.. parts, workspaceRoot];
        }

        return [.. parts.Select(part => part.Replace(IOpenerArguments.WorkspaceToken, workspaceRoot, StringComparison.Ordinal))];
    }

    /// <summary>
    /// Whitespace separates and double quotes group. There is no backslash escape, since
    /// Windows and Linux disagree about it and a Windows path is what gets pasted in.
    /// </summary>
    private static List<string> Split(string template)
    {
        List<string> parts = [];
        var part = new StringBuilder();
        var quoted = false;
        var started = false;

        foreach (var character in template)
        {
            if (character == '"')
            {
                quoted = !quoted;
                started = true;
                continue;
            }

            if (!quoted && char.IsWhiteSpace(character))
            {
                if (started)
                {
                    parts.Add(part.ToString());
                    part.Clear();
                    started = false;
                }

                continue;
            }

            part.Append(character);
            started = true;
        }

        // An unbalanced quote closes at the end rather than throwing away what was typed.
        if (started)
        {
            parts.Add(part.ToString());
        }

        return parts;
    }
}
