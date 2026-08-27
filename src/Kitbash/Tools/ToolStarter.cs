using Kitbash.Core.Platform;

namespace Kitbash.Tools;

/// <summary>
/// The whole contract between the launcher and a tool: the program the manifest names,
/// run in its own folder, with the open workspace as an argument when it takes one.
/// </summary>
public sealed class ToolStarter : IToolStarter
{
    /// <summary>How a tool is told which workspace to open, script or window alike.</summary>
    public const string WorkspaceArgument = "--workspace";

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

        var workspace = Argument(tool, workspaceRoot);

        _platform.StartDetached(
            ProcessRequest.CommandIn(
                tool.Directory, tool.Executable, [.. tool.Arguments, .. workspace]));
    }

    /// <summary>
    /// The workspace argument, or nothing when there is no workspace open and when the
    /// manifest says the tool does not take one.
    /// </summary>
    internal static string[] Argument(InstalledTool tool, string? workspaceRoot) =>
        string.IsNullOrWhiteSpace(workspaceRoot) || !tool.Manifest.TakesWorkspace
            ? []
            : [WorkspaceArgument, workspaceRoot];
}
