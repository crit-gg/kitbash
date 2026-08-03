namespace Workbench.Core.Settings;

/// <summary>
/// A settings file is there and cannot be used, because it will not parse or because it
/// would not open. Thrown by a read that has no way to report the failure, and by every
/// write.
/// </summary>
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
