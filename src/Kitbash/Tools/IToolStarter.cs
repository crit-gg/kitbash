using Kitbash.Core.Platform;

namespace Kitbash.Tools;

/// <summary>Opening a tool, which is starting a program.</summary>
public interface IToolStarter
{
    /// <summary>
    /// Runs the tool in its own version folder, told which workspace is open. A tool
    /// outlives the launcher, so closing the launcher leaves it running.
    /// </summary>
    /// <param name="workspaceRoot">The open workspace, or null when there is none.</param>
    /// <exception cref="ProcessStartException">The tool could not be started.</exception>
    void Start(InstalledTool tool, string? workspaceRoot);
}
