namespace Workbench.Core.Settings;

/// <summary>
/// A settings file is there and cannot be used, because it will not parse or because it
/// would not open. Thrown by a read that has no way to report the failure, and by every
/// write.
/// </summary>
/// <remarks>
/// On the write side this is the guard behind everything. A write reads the file,
/// changes the keys it was given and writes the whole document back. Against a file that
/// would not parse, the read produces an empty document, and writing that back would
/// replace every hand written value with almost nothing under a button that said Save
/// changes. So the refusal is on the parse result rather than on the document being
/// empty, since after the fact those two look identical.
/// </remarks>
public sealed class SettingsFileUnreadableException : IOException
{
    public SettingsFileUnreadableException(string path, string message, Exception? inner = null)
        : base(message, inner)
    {
        Path = path;
    }

    /// <summary>The file that could not be read.</summary>
    public string Path { get; }
}
