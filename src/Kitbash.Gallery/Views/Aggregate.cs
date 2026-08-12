using System.ComponentModel;

namespace Kitbash.Gallery.Views;

/// <summary>
/// What the gallery's tree grid is made of. Every cell is already written out, because a
/// branch row shows what its children add up to and a leaf shows its own value, and the
/// grid is not the thing that decides which.
/// </summary>
public sealed class Aggregate(
    string name,
    string kind,
    string value,
    string tier,
    string state,
    IReadOnlyList<Aggregate>? children = null)
    : INotifyPropertyChanged, IEditableObject
{
    private string amount = value;
    private string committed = value;
    private string saved = value;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; } = name;

    public string Kind { get; } = kind;

    /// <summary>The one cell a person can type into, which is what the tree grid edits.</summary>
    public string Value
    {
        get => amount;
        set
        {
            if (amount == value)
            {
                return;
            }

            amount = value;
            Raise(nameof(Value));
            Raise(nameof(IsModified));
        }
    }

    /// <summary>Whether the value has moved since it was last saved.</summary>
    public bool IsModified => amount != committed;

    public string Tier { get; } = tier;

    public string State { get; } = state;

    public IReadOnlyList<Aggregate> Children { get; } = children ?? [];

    public override string ToString() => Name;

    /// <summary>Takes the value as saved, which is what clears the modified mark.</summary>
    public void Save()
    {
        committed = amount;
        Raise(nameof(IsModified));
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    // The tree grid drives these through the same body the flat grid uses, so Escape puts
    // the old value back here as well.

    void IEditableObject.BeginEdit() => saved = amount;

    void IEditableObject.CancelEdit() => Value = saved;

    void IEditableObject.EndEdit()
    {
    }
}
