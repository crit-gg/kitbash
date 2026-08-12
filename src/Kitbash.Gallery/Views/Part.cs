using System.ComponentModel;
using Kitbash.Ui.Controls;

namespace Kitbash.Gallery.Views;

/// <summary>
/// What the cell forms grid and the row model grid are made of. One property per kind a
/// column can hold, so all nine in cell forms are read down a single row.
/// </summary>
public sealed class Part(
    string id,
    bool isOn,
    string kind,
    double count,
    string label,
    string source,
    string[] tags,
    double done,
    ColorValue tint,
    string? stage = null)
    : INotifyPropertyChanged, IEditableObject
{
    /// <summary>What the enum column offers. A view binds this rather than writing it again.</summary>
    public static readonly string[] Kinds = ["mesh", "material", "texture", "curve"];

    /// <summary>The stages in the order they happen, which is what the stage comparer reads.</summary>
    public static readonly string[] Stages = ["draft", "review", "final"];

    private bool on = isOn;
    private string family = kind;
    private double amount = count;
    private string title = label;
    private double reach = done;
    private ColorValue colour = tint;
    private string? step = stage;

    private (bool On, string Family, double Amount, string Title, double Reach, ColorValue Colour, string? Step)? held;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; } = id;

    /// <summary>The tick. Content rather than an editor, so a click on it lands straight away.</summary>
    public bool IsOn
    {
        get => on;
        set => Set(ref on, value, nameof(IsOn));
    }

    public string Kind
    {
        get => family;
        set => Set(ref family, value, nameof(Kind));
    }

    public double Count
    {
        get => amount;
        set => Set(ref amount, value, nameof(Count));
    }

    public string Label
    {
        get => title;
        set => Set(ref title, value, nameof(Label));
    }

    /// <summary>What this row points at, for the reference cell.</summary>
    public string Source { get; } = source;

    public string[] Tags { get; } = tags;

    /// <summary>How far along it is, from 0 to 1.</summary>
    public double Done
    {
        get => reach;
        set
        {
            if (Set(ref reach, value, nameof(Done)))
            {
                Raise(nameof(DoneText));
            }
        }
    }

    /// <summary>The same as a percentage, which is the caller's reading of it.</summary>
    public string DoneText => $"{Done * 100:N0}%";

    public ColorValue Tint
    {
        get => colour;
        set
        {
            if (Set(ref colour, value, nameof(Tint)))
            {
                Raise(nameof(TintText));
            }
        }
    }

    /// <summary>The colour as its hex, which is what the cell prints beside the swatch.</summary>
    public string TintText => Tint.ToText();

    /// <summary>Where it has got to, or null, which is the row the nulls first rule is read on.</summary>
    public string? Stage
    {
        get => step;
        set => Set(ref step, value, nameof(Stage));
    }

    public override string ToString() => Id;

    /// <summary>Takes a value as the text it arrived as, which is what a paste hands over.</summary>
    public void Put(string column, string? text)
    {
        switch (column)
        {
            case "LABEL":
                Label = text ?? string.Empty;
                break;

            case "KIND":
                Kind = text ?? string.Empty;
                break;

            case "COUNT":
                Count = double.TryParse(text, out var number) ? number : 0;
                break;

            case "STAGE":
                Stage = string.IsNullOrWhiteSpace(text) ? null : text;
                break;
        }
    }

    private bool Set<T>(ref T field, T value, string property)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(property);

        return true;
    }

    private void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    // The grid drives these. Under a row transaction they wrap every cell touched in the
    // row, so Escape puts all of them back rather than only the last one.

    void IEditableObject.BeginEdit() => held ??= (on, family, amount, title, reach, colour, step);

    void IEditableObject.CancelEdit()
    {
        if (held is not { } was)
        {
            return;
        }

        held = null;

        IsOn = was.On;
        Kind = was.Family;
        Count = was.Amount;
        Label = was.Title;
        Done = was.Reach;
        Tint = was.Colour;
        Stage = was.Step;
    }

    void IEditableObject.EndEdit() => held = null;
}
