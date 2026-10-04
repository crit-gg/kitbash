namespace Kitbash.Core.Platform;

/// <summary>
/// The folder this person's own commands go in, and whether a terminal opened now finds
/// them there.
/// </summary>
public interface ICommandFolder
{
    /// <summary>The folder, or null when this machine gives no answer, such as no home.</summary>
    string? Directory { get; }

    /// <summary>Whether this copy can write a command here at all.</summary>
    bool CanWrite { get; }

    /// <summary>What a command of this name would be found as, or null with no folder.</summary>
    string? PathOf(string name);

    /// <summary>What is in the folder under this name now.</summary>
    CommandEntry Read(string name);

    /// <summary>Makes the name run the program, replacing what is there in one step.</summary>
    /// <exception cref="IOException">The folder or the entry could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not ours to write.</exception>
    void Write(string name, string program);

    /// <summary>Removes the name, doing nothing when it is already gone.</summary>
    /// <exception cref="IOException">The entry could not be removed.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not ours to write.</exception>
    void Remove(string name);

    /// <summary>
    /// Puts the folder on PATH where this desktop allows it, then says what a new terminal
    /// will run for the name. Never throws.
    /// </summary>
    /// <param name="mayAsk">Whether the person may be asked to allow it.</param>
    Task<CommandReach> ReachAsync(string name, bool mayAsk, CancellationToken cancellation);

    /// <summary>
    /// Takes the folder off PATH, where Kitbash put it there and nothing else could need it.
    /// Never throws.
    /// </summary>
    void Unreach();

    /// <summary>Removes the name and takes the folder off PATH, for an uninstaller. Never throws.</summary>
    void Forget(string name);
}
