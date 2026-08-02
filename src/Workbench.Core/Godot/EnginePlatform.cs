namespace Workbench.Core.Godot;

/// <summary>
/// A platform Godot publishes a desktop editor for.
/// </summary>
/// <remarks>
/// <para>
/// This classifies a published file. It is not a choice anyone makes. The page lists the
/// builds for the machine it is running on and nothing else, so a person never sees a
/// platform named, and macOS is here only so a macOS download is recognised and dropped
/// rather than falling through as something unknown.
/// </para>
/// <para>
/// The web and Android editors are absent for the same reason they are not installable.
/// Neither is a desktop editor and neither has a name shape in the nineteen.
/// </para>
/// </remarks>
public enum EnginePlatform
{
    Windows,
    Linux,
    MacOS,
}
