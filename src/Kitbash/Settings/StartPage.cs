namespace Kitbash.Settings;

/// <summary>
/// The page the launcher opens on. Stored by name, so a hand edited file spells the value
/// rather than a number.
/// </summary>
public enum StartPage
{
    /// <summary>The list of every workspace.</summary>
    AllWorkspaces,

    /// <summary>The workspace that was open last, or the list when there is none.</summary>
    OpenWorkspace,
}
