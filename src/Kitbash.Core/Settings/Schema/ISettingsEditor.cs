namespace Kitbash.Core.Settings.Schema;

/// <summary>
/// An app's own editor for something no descriptor can describe, such as a list of
/// tables. It owns its value and its file, so nothing here has a key and the settings
/// writer never sees it. It still stages, so the window's unsaved count and its Save
/// button mean what they say.
/// </summary>
public interface ISettingsEditor
{
    /// <summary>There is a change here that has not been written.</summary>
    bool IsDirty { get; }

    /// <summary>Everything staged can be written as it stands.</summary>
    bool IsValid { get; }

    /// <summary>
    /// Whether the file behind the page can be written. The window sets it, since only
    /// the window knows that a file is missing or will not parse.
    /// </summary>
    bool IsPageWritable { get; set; }

    /// <summary>Raised whenever <see cref="IsDirty"/> or <see cref="IsValid"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>
    /// Reads what is stored. Awaited on the UI thread, so an editor that touches a disk
    /// puts that part on another thread itself.
    /// </summary>
    Task LoadAsync(CancellationToken token = default);

    /// <summary>
    /// Writes what is staged. Called only when <see cref="IsDirty"/>, and awaited on the
    /// UI thread the same way <see cref="LoadAsync"/> is. The window reads the page again
    /// afterwards.
    /// </summary>
    Task SaveAsync(CancellationToken token = default);

    /// <summary>Throws away everything staged and goes back to what was read.</summary>
    void Discard();
}
