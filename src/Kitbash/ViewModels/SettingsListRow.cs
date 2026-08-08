using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kitbash.ViewModels;

/// <summary>
/// One row of a settings list, drawn by <c>ui:DataGrid</c>. The grid calls
/// <see cref="IEditableObject"/> around an edit, so Escape puts the row back and clicking
/// away keeps it.
/// </summary>
public abstract class SettingsListRow : ObservableObject, IEditableObject
{
    /// <summary>Why this row cannot be written, in a person's words, or blank when it can.</summary>
    public abstract string Problem { get; }

    public bool IsValid => Problem.Length == 0;

    /// <summary>Takes a copy to go back to, since Escape has to put every field back.</summary>
    public abstract void BeginEdit();

    /// <summary>Puts every field back to the copy taken when the edit began.</summary>
    public abstract void CancelEdit();

    /// <summary>Keeps what was typed. The copy is dropped.</summary>
    public abstract void EndEdit();

    /// <summary>Call from a field's setter, so the row's own verdict follows it.</summary>
    protected void Announce()
    {
        OnPropertyChanged(nameof(Problem));
        OnPropertyChanged(nameof(IsValid));
    }
}
