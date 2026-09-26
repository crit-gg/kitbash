namespace Kitbash.Core.Platform;

/// <summary>Whether a program on disk is running for this person right now.</summary>
public interface IRunningPrograms
{
    /// <summary>
    /// True when any process of this person's was started from the file. Walks every
    /// process, which took 225 ms over 646 of them on Linux, so keep it off the UI thread.
    /// </summary>
    bool IsRunning(string executable);
}
