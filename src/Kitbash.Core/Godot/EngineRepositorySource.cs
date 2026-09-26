using Kitbash.Core.Platform;

namespace Kitbash.Core.Godot;

/// <summary>
/// One entry of <c>godot.repositories</c>: a name a workspace pins by, and the address
/// the installs are keyed by.
/// </summary>
/// <param name="Name">What <c>godot.repository</c> names. Compared ignoring case.</param>
/// <param name="Url">The repository as written in the file.</param>
/// <param name="Origin">Where the entry is listed, in words, so a message can say which file.</param>
public sealed record EngineRepositorySource(
    string Name,
    EngineRepositoryAddress Address,
    WebAddress Url,
    string Origin)
{
    public override string ToString() => Name;
}
