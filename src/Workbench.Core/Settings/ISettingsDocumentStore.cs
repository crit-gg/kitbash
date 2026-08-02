namespace Workbench.Core.Settings;

/// <summary>Reads and writes settings documents. The only place the file format lives.</summary>
internal interface ISettingsDocumentStore
{
    /// <summary>
    /// A missing file reads as an empty document. A file that will not parse throws,
    /// which is what stops a caller that cannot report the failure from writing over it.
    /// </summary>
    SettingsDocument Read(string path);

    /// <summary>
    /// As <see cref="Read"/>, with a parse failure arriving as a value instead. For
    /// callers that draw the failure rather than fail. Never throws for bad content.
    /// </summary>
    SettingsFile Open(string path);

    void Write(string path, SettingsDocument document);

    /// <summary>
    /// Applies every edit to one file and writes it once. Refuses a file it could not
    /// read, with <see cref="SettingsFileUnreadableException"/>. Writes nothing when no
    /// edit changed anything, since a write costs the file its comments.
    /// </summary>
    void Apply(string path, IReadOnlyList<SettingsEdit> edits);
}
