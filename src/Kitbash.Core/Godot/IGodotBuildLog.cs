namespace Kitbash.Core.Godot;

/// <summary>
/// The log the editor keeps of a C# build. Godot writes the compiler's lines there and
/// prints only that the build failed.
/// </summary>
public interface IGodotBuildLog
{
    /// <summary>
    /// The newest log naming this project and written since <paramref name="since"/>, or
    /// null when there is none.
    /// </summary>
    string? Read(InstalledEngine engine, GodotProject project, DateTimeOffset since);
}
