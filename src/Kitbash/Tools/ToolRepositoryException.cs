namespace Kitbash.Tools;

/// <summary>
/// A repository that could not be reached or could not be understood. Both are the same
/// outcome on screen, which is that the tools it offers are simply absent.
/// </summary>
public sealed class ToolRepositoryException : Exception
{
    public ToolRepositoryException(string message)
        : base(message)
    {
    }

    public ToolRepositoryException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
