namespace Kitbash.Tools;

/// <summary>
/// Putting a version on this machine and taking one off. Nothing here runs on its own: a
/// person clicks Install and a person clicks Update.
/// </summary>
public interface IToolInstaller
{
    /// <summary>
    /// Downloads, checks, extracts and makes the version the one that opens. A version
    /// directory exists whole or does not exist, so a failure leaves what was there alone.
    /// </summary>
    /// <exception cref="ToolInstallException">It did not land, and why.</exception>
    Task<InstalledTool> InstallAsync(
        OfferedTool tool,
        IProgress<ToolInstallProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the versions and keeps everything a person owns: the tool's state file, its
    /// settings, and anything it wrote inside a workspace.
    /// </summary>
    void Uninstall(InstalledTool tool);

    /// <summary>
    /// Removes version directories that are not the one that opens. Called at launch,
    /// since a version is never removed while anything might be running from it.
    /// </summary>
    void SweepOldVersions(IReadOnlyList<InstalledTool> installed);
}
