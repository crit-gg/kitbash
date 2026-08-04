namespace Kitbash.Core.Platform;

/// <summary>
/// The desktop could not pick a colour. A person changing their mind is not this, since that
/// is an answer rather than a failure.
/// </summary>
public sealed class ScreenColourException : Exception
{
    public ScreenColourException(string message)
        : base(message)
    {
    }

    public ScreenColourException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
