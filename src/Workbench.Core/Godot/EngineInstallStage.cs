namespace Workbench.Core.Godot;

/// <summary>Where an install has got to. The order is the order they happen in.</summary>
public enum EngineInstallStage
{
    /// <summary>Waiting for a slot. Several installs run at once and the rest queue.</summary>
    Queued,

    Downloading,

    /// <summary>Hashing the archive against the checksum the project published.</summary>
    Verifying,

    Extracting,

    /// <summary>Asking the editor what it is and writing the record.</summary>
    Registering,

    Done,
}
