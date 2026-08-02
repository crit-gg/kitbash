namespace Workbench.Core.Godot;

/// <summary>
/// The processor a Godot editor build runs on. <see cref="Universal"/> is the macOS
/// build, which carries more than one.
/// </summary>
public enum EngineArchitecture
{
    X64,
    X86,
    Arm64,
    Arm32,
    Universal,
}
