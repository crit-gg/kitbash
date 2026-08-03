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

    /// <summary>
    /// Replaces the file with the document. Comments, blank lines and key order are the
    /// document's, which holds none of them, so a file that had any loses them. For a
    /// file the app generates and nobody edits.
    /// </summary>
    void Write(string path, SettingsDocument document);

    /// <summary>
    /// Applies every edit to one file and writes it once, leaving everything the edits did
    /// not name exactly as it was, comments included. Refuses a file it could not read,
    /// with <see cref="SettingsFileUnreadableException"/>. Writes nothing when no edit
    /// changed anything.
    /// </summary>
    void Apply(string path, IReadOnlyList<SettingsEdit> edits);
}
