namespace Kitbash.Core.Godot;

/// <summary>
/// One Godot project about to be written into a folder.
/// </summary>
/// <param name="Directory">Where the files go. It has to exist already.</param>
/// <param name="Name">
/// <c>application/config/name</c>. Trimmed before it is written, the way Godot trims it.
/// </param>
/// <param name="Engine">
/// The engine the project is made for. Its major and minor go into
/// <c>application/config/features</c>, which is what Godot writes there.
/// </param>
/// <param name="Renderer">The renderer the project starts on.</param>
public sealed record NewGodotProject(
    string Directory,
    string Name,
    EngineTag Engine,
    GodotRenderer Renderer);
