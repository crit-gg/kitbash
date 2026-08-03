namespace Kitbash.Core.Platform;

/// <summary>Thrown when a process could not be started at all.</summary>
public sealed class ProcessStartException : Exception
{
    public ProcessStartException(string fileName, Exception innerException)
        : base($"Could not start '{fileName}'.", innerException)
    {
        FileName = fileName;
    }

    public string FileName { get; }
}
