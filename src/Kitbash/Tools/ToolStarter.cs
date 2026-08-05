using Kitbash.Core.Platform;

namespace Kitbash.Tools;

/// <summary>
/// The whole contract between the launcher and a tool: the program the manifest names,
/// run in its own folder, with the open workspace as an argument.
/// </summary>
public sealed class ToolStarter : IToolStarter
{
    /// <summary>How a tool is told which workspace to open.</summary>
    private const string WorkspaceArgument = "--workspace";

    private readonly IPlatformServices _platform;

    public ToolStarter(IPlatformServices platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        _platform = platform;
    }

    /// <summary>
    /// A missing program and one that is not runnable both come back from the runner as
    /// <see cref="ProcessStartException"/>, so neither is checked for here first.
    /// </summary>
    public void Start(InstalledTool tool, string? workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(tool);

        string[] arguments = string.IsNullOrWhiteSpace(workspaceRoot)
            ? []
            : [WorkspaceArgument, workspaceRoot];

        _platform.StartDetached(
            ProcessRequest.CommandIn(tool.Directory, tool.Executable, arguments));
    }
}
