namespace Kitbash.Core.Platform.MacOS;

/// <summary>
/// Writes the folder into /etc/paths.d, which path_helper adds to every login shell's PATH.
/// That folder belongs to root, so the system asks the person for an administrator.
/// </summary>
internal sealed class MacPathsRequest : IPathRequest
{
    // Part of the OS, and named absolutely so PATH cannot shadow it.
    private const string Osascript = "/usr/bin/osascript";

    // AppleScript's own answer when the person cancels the password dialog.
    private const string Cancelled = "(-128)";

    // The folder arrives as an argument and goes through quoted form of, so nothing in it
    // is ever read as AppleScript or as shell.
    private static readonly string[] Script =
    [
        "on run argv",
        "do shell script \"/bin/mkdir -p /etc/paths.d && /bin/echo \" & quoted form of (item 1 of argv) & \" > /etc/paths.d/kitbash && /bin/chmod 644 /etc/paths.d/kitbash\" with prompt \"Kitbash wants to add a folder to PATH so a terminal can run godot.\" with administrator privileges",
        "end run",
    ];

    private readonly IProcessRunner _processes;

    public MacPathsRequest(IProcessRunner processes)
    {
        ArgumentNullException.ThrowIfNull(processes);
        _processes = processes;
    }

    public async Task<PathReach> AskAsync(string directory, CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var arguments = Script.SelectMany(line => new[] { "-e", line }).Append(directory).ToArray();

        try
        {
            var output = await _processes
                .ReadAsync(ProcessRequest.Command(Osascript, arguments), cancellation)
                .ConfigureAwait(false);

            if (output.Succeeded)
            {
                return PathReach.Added;
            }

            return output.StandardError.Contains(Cancelled, StringComparison.Ordinal)
                ? PathReach.Declined
                : PathReach.NotOnPath;
        }
        catch (ProcessStartException)
        {
            return PathReach.NotOnPath;
        }
    }
}
