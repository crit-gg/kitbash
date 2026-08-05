namespace Kitbash.Ui.Controls;

/// <summary>Whether a run of lines can be picked out of a diff.</summary>
public enum TextDiffPicking
{
    /// <summary>
    /// The diff is read only. The default, so a diff that was never told stays safe: a format
    /// whose lines reference each other cannot be taken apart a line at a time.
    /// </summary>
    None,

    /// <summary>A run of lines can be picked, by selecting it or by pointing at it.</summary>
    Lines,
}

/// <summary>What a picked run of lines can be asked to do.</summary>
[Flags]
public enum TextDiffActions
{
    None = 0,

    /// <summary>Put these lines into the index.</summary>
    Stage = 1,

    /// <summary>Take these lines back out of the index.</summary>
    Unstage = 2,

    /// <summary>Throw these lines away, putting the file on disk back.</summary>
    Discard = 4,
}

/// <summary>
/// A run of lines a gesture would act on, and where it sits in the view. The lines count from
/// one and both ends are in it.
/// </summary>
/// <param name="Top">Where the run starts, in the view's own space.</param>
public sealed record TextDiffChunk(int First, int Last, double Top, double Height);

/// <summary>A run of lines and what was asked of it.</summary>
public sealed class TextDiffChunkEventArgs(TextDiffActions action, TextDiffChunk chunk) : EventArgs
{
    public TextDiffActions Action { get; } = action;

    public TextDiffChunk Chunk { get; } = chunk;
}
