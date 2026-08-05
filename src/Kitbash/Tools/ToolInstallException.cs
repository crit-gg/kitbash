namespace Kitbash.Tools;

/// <summary>
/// An install that did not land. Nothing in use is ever overwritten, so whatever was
/// installed before is still what runs.
/// </summary>
public sealed class ToolInstallException : Exception
{
    public ToolInstallException(string message)
        : base(message)
    {
    }

    public ToolInstallException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
