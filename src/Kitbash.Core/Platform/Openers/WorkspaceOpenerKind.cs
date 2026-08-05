namespace Kitbash.Core.Platform.Openers;

/// <summary>What kind of program a workspace is being opened in.</summary>
public enum WorkspaceOpenerKind
{
    /// <summary>An editor or an IDE, found on this machine.</summary>
    Editor,

    /// <summary>A terminal, found on this machine.</summary>
    Terminal,

    /// <summary>One a person added themselves.</summary>
    Custom,
}
