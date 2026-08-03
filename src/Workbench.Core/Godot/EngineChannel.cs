namespace Workbench.Core.Godot;

/// <summary>
/// The kind of a Godot release. Declared in release order, so comparing two of these
/// ranks a dev snapshot below a release candidate.
/// </summary>
public enum EngineChannel
{
    Dev,
    Alpha,
    Beta,
    Rc,
    Stable,
}
