using System.ComponentModel;
using System.Runtime.CompilerServices;
using Workbench.Ui.Controls;

namespace Workbench.Gallery.Views;

/// <summary>
/// What the gallery's flat grid is made of. Structure, not content: an identifier, a name,
/// a couple of values and a status, which is every kind of cell the design draws.
/// </summary>
public sealed class Entry(string id, string name, string kind, double value, int tier, PillStatus status, string state)
    : INotifyPropertyChanged, IEditableObject
{
    private double amount = value;
    private double saved;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public string Kind { get; } = kind;

    /// <summary>The one editable cell on the page.</summary>
    public double Value
    {
        get => amount;
        set
        {
            if (amount.Equals(value))
            {
                return;
            }

            amount = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public int Tier { get; } = tier;

    public PillStatus Status { get; } = status;

    public string State { get; } = state;

    public override string ToString() => Id;

    // The grid drives these, which is what makes Escape able to put the old value back.

    void IEditableObject.BeginEdit() => saved = amount;

    void IEditableObject.CancelEdit() => Value = saved;

    void IEditableObject.EndEdit()
    {
    }
}
