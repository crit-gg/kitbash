namespace Workbench.Core.Godot;

/// <summary>
/// One engine on this machine: what was recorded about it, plus what the disk says now.
/// </summary>
/// <param name="Record">The facts written when it landed.</param>
/// <param name="Directory">Where it lives.</param>
/// <param name="Executable">The editor, as an absolute path.</param>
/// <param name="SizeOnDisk">
/// Bytes under the directory. Walked on every read, since a person can change what is
/// inside it. Nothing caches it.
/// </param>
/// <param name="IsMissing">The folder has gone. Kept in the list rather than dropped.</param>
/// <param name="IsImported">
/// A folder Workbench did not create. Removing one forgets it and never deletes it.
/// </param>
public sealed record InstalledEngine(
    EngineRecord Record,
    string Directory,
    string Executable,
    long SizeOnDisk,
    bool IsMissing,
    bool IsImported)
{
    public EngineId Id => Record.Id;

    public EngineTag Tag => Record.Id.Tag;

    public bool IsMono => Record.Id.IsMono;

    public EngineArchitecture Architecture => Record.Architecture;

    /// <summary>How Godot writes this processor, such as <c>x86_64</c>.</summary>
    public string ArchitectureText => EngineBuild.TextFor(Record.Architecture);

    /// <summary>
    /// True when this build cannot run here. Possible only for an imported engine, since
    /// the page never offers a foreign platform to install.
    /// </summary>
    public bool RunsOn(IEngineFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return Record.Platform == files.Platform;
    }
}
