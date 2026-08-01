namespace Workbench.Core.Settings;

/// <summary>Reads and writes settings documents. The only place the file format lives.</summary>
internal interface ISettingsDocumentStore
{
    /// <summary>A missing file reads as an empty document.</summary>
    SettingsDocument Read(string path);

    void Write(string path, SettingsDocument document);
}
