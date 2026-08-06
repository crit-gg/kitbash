using System.Text;
using Kitbash.Core.Platform;

namespace Kitbash.Tools;

/// <summary>
/// Runs a script tool and turns what it writes into progress. Output is read a line at a
/// time while it runs, and error arrives at the end, since only one of the two is a report.
/// </summary>
public sealed class ToolScriptRunner : IToolScriptRunner
{
    private readonly IProcessRunner _processes;
    private readonly ToolProgressReader _reader;

    public ToolScriptRunner(IProcessRunner processes, ToolProgressReader reader)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(reader);

        _processes = processes;
        _reader = reader;
    }

    public async Task<ToolRunOutcome> RunAsync(
        InstalledTool tool,
        string? workspaceRoot,
        IReadOnlyList<string> arguments,
        IProgress<ToolProgressStep> progress,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(progress);

        string[] workspace = string.IsNullOrWhiteSpace(workspaceRoot)
            ? []
            : [ToolStarter.WorkspaceArgument, workspaceRoot];

        var request = new ProcessRequest(
            tool.Executable,
            [.. workspace, .. arguments],
            UseShellExecute: false,
            tool.Directory)
        {
            // A script writes whatever its own language writes, and that is UTF 8 on both
            // platforms. Without this Windows would decode it as the OEM code page.
            TextEncoding = Encoding.UTF8,
        };

        var output = await _processes
            .ReadLinesAsync(request, line => Report(progress, _reader.Read(line)), cancellation)
            .ConfigureAwait(false);

        foreach (var line in output.StandardError.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Report(progress, _reader.ReadError(line));
        }

        return new ToolRunOutcome(output.ExitCode);
    }

    private static void Report(IProgress<ToolProgressStep> progress, ToolProgressStep step)
    {
        if (!step.IsEmpty)
        {
            progress.Report(step);
        }
    }
}
