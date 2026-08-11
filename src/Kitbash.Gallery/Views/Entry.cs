using System.ComponentModel;
using System.Runtime.CompilerServices;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views;

/// <summary>
/// What the gallery's flat grid is made of. Structure, not content: an identifier, a name,
/// a couple of values and a status, which is every kind of cell the design draws.
/// </summary>
public sealed class Entry(string id, string name, string kind, double value, int tier, PillStatus status, string state)
    : INotifyPropertyChanged, IEditableObject
{
    private double amount = value;
    private double saved;
    private double committed = value;
    private string title = name;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; } = id;

    public string Name
    {
        get => title;
        set
        {
            if (title == value)
            {
                return;
            }

            title = value;
            Raise(nameof(Name));
        }
    }

    public string Kind { get; private set; } = kind;

    /// <summary>Takes a value as the text it arrived as, which is what a paste hands over.</summary>
    public void Put(string column, string? text)
    {
        switch (column)
        {
            case "NAME":
                Name = text ?? string.Empty;
                break;

            case "KIND":
                Kind = text ?? string.Empty;
                Raise(nameof(Kind));
                break;

            case "VALUE":
                Value = double.TryParse(text, out var number) ? number : 0;
                break;
        }
    }

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
            Raise(nameof(Value));
            Raise(nameof(IsModified));
        }
    }

    /// <summary>Whether the value has moved since it was last saved.</summary>
    public bool IsModified => !amount.Equals(committed);

    public int Tier { get; } = tier;

    public PillStatus Status { get; } = status;

    public string State { get; } = state;

    /// <summary>What this row points at, for the reference cell. Derived, so nothing new is stored.</summary>
    public string Machine => $"MCH_{Kind.Replace(" ", string.Empty)}";

    /// <summary>What it is filed under, for the tags cell.</summary>
    public string[] Tags => Tier > 2
        ? [Kind.ToLowerInvariant(), $"tier {Tier}", "review", "bulk"]
        : [Kind.ToLowerInvariant(), $"tier {Tier}"];

    /// <summary>How far along it is, for the ratio cell.</summary>
    public double Yield => Math.Clamp((Value % 20) / 20, 0, 1);

    /// <summary>The same as a percentage, which is the caller's reading of it.</summary>
    public string YieldText => $"{Yield * 100:N0}%";

    public override string ToString() => Id;

    /// <summary>Takes the value as saved, which is what clears the modified mark.</summary>
    public void Save()
    {
        committed = amount;
        Raise(nameof(IsModified));
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    // The grid drives these, which is what makes Escape able to put the old value back.

    void IEditableObject.BeginEdit() => saved = amount;

    void IEditableObject.CancelEdit() => Value = saved;

    void IEditableObject.EndEdit()
    {
    }
}
