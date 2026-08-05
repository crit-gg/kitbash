namespace Kitbash.Tools;

/// <summary>
/// A manifest that could not be used. <see cref="Exception.Message"/> is written for a
/// person, since a tool that will not install has to say why.
/// </summary>
public sealed class ToolManifestException : Exception
{
    public ToolManifestException(string message)
        : base(message)
    {
    }

    public ToolManifestException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
