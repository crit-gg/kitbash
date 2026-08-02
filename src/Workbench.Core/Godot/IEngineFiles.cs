namespace Workbench.Core.Godot;

/// <summary>
/// What an engine on disk looks like on this machine. The only part of engine handling
/// that differs per OS, and it is smaller than it looks: naming is not here, because a
/// release manifest names the files and this app never derives one.
/// </summary>
public interface IEngineFiles
{
    /// <summary>The platform this machine runs, which is the only one the page offers.</summary>
    EnginePlatform Platform { get; }

    /// <summary>
    /// The processor this machine runs.
    /// </summary>
    /// <remarks>
    /// Read from the operating system rather than from this process. Workbench ships as
    /// x64 only, so a Windows machine on arm64 runs it under emulation while still wanting
    /// the native arm64 editor.
    /// </remarks>
    EngineArchitecture Architecture { get; }

    /// <summary>
    /// The editor in an extracted tree, or null when there is nothing that looks like one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// **Found rather than named.** The layout differs per platform and per .NET flag, and
    /// it has moved before. Measured across twenty archives spanning 4.0-alpha1 to
    /// 4.8-dev2: Linux standard is one file, Linux .NET is a folder, Windows standard is
    /// two loose files and Windows .NET is a folder. What holds throughout is that on Unix
    /// the editor carries the executable bit and is not under <c>GodotSharp/</c>, and on
    /// Windows it is the <c>.exe</c> that is not the console one.
    /// </para>
    /// <para>
    /// **Only the top level is searched.** Every install Workbench makes has the editor
    /// there, because extraction strips a single wrapper folder, and a person importing one
    /// points at the folder holding the binary. Searching deeper was tried and it is wrong:
    /// pointing at a folder that merely contains an engine folder registered the outer
    /// folder as the install, which then reports the wrong size and, for an install
    /// Workbench owns, would delete the wrong tree. A probe caught it.
    /// </para>
    /// </remarks>
    string? FindEditor(string directory);

    /// <summary>
    /// Makes a file runnable, where that means anything.
    /// </summary>
    /// <remarks>
    /// Not a rare guard. <c>ZipFile.ExtractToDirectory</c> carries an archive's Unix mode
    /// across, but unpacking entry by entry does not, and the prefix strip and the two
    /// extraction guards mean this app unpacks by hand. So the installer calls this for
    /// every entry the archive recorded as executable. Measured: without it, an install
    /// completes and holds an editor at 0644 that nothing can find or run.
    /// </remarks>
    void MakeExecutable(string path);

    /// <summary>
    /// Hides a file from a person browsing the folder.
    /// </summary>
    /// <remarks>
    /// A leading dot does it on Unix and means nothing on Windows, which needs the hidden
    /// attribute set. The name carries the dot either way, so this is the second half on
    /// the one platform that needs it.
    /// </remarks>
    void Hide(string path);
}
