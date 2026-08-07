namespace Kitbash.Tools;

/// <summary>
/// The program a tool runs as, and what goes in front of the arguments the launcher adds.
/// A payload names one file inside itself and a folder on this machine may name a whole
/// command line, such as a build tool run against a project.
/// </summary>
/// <param name="Program">A full path, or a bare name the operating system looks up on PATH.</param>
public sealed record ToolCommand(string Program, IReadOnlyList<string> Arguments)
{
    /// <summary>The one file a payload names, inside the folder it was unpacked into.</summary>
    public static ToolCommand For(ToolPayload payload, string directory)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new ToolCommand(payload.ExecutableIn(directory), []);
    }

    /// <summary>The program is a file this launcher resolved rather than a name to look up.</summary>
    public bool IsFile => Path.IsPathRooted(Program);
}
