namespace Workbench.Core.Git;

/// <summary>
/// The variables that stop git waiting for a person. Nothing here has a terminal behind
/// it, so a prompt would wait until the app closed.
/// </summary>
public sealed class GitEnvironment
{
    /// <summary>An empty value unsets the variable, which is what stops a helper the
    /// environment already names.</summary>
    public IReadOnlyDictionary<string, string> Unattended { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GIT_ASKPASS"] = "",
            ["SSH_ASKPASS"] = "",
            ["SSH_ASKPASS_REQUIRE"] = "never",
            ["GIT_SSH_COMMAND"] = "ssh -oBatchMode=yes -oStrictHostKeyChecking=accept-new",
            ["GCM_INTERACTIVE"] = "never",
        };
}
