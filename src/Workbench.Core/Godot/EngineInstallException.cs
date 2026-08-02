namespace Workbench.Core.Godot;

/// <summary>
/// Thrown when an install could not be completed. Its message is written to be shown, since
/// the failure toast states where the download stopped and why.
/// </summary>
public sealed class EngineInstallException : Exception
{
    public EngineInstallException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
