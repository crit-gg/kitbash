namespace Kitbash.Core.Platform.Openers;

/// <summary>Turns what a person typed into argv.</summary>
public interface IOpenerArguments
{
    /// <summary>The token a template puts the workspace path at.</summary>
    const string WorkspaceToken = "{workspace}";

    /// <summary>
    /// Splits the template and fills in the token. A blank template gives the workspace
    /// alone, and a template naming no token gets it appended.
    /// </summary>
    IReadOnlyList<string> Build(string? template, string workspaceRoot);
}
