namespace Kitbash.Ui.Controls;

/// <summary>A card of prose on the canvas, from Rime's comment cards.</summary>
public sealed class GraphNote : GraphItem
{
    private string _text;
    private string? _author;

    public GraphNote(string id, string text, string? author = null)
        : base(id)
    {
        _text = text;
        _author = author;
    }

    public string Text
    {
        get => _text;
        set => SetLook(ref _text, value);
    }

    /// <summary>A note's label is the note. It is what a rename box edits, over several lines.</summary>
    public override string? Label
    {
        get => _text;
        set => SetLook(ref _text, value ?? string.Empty);
    }

    public override bool LabelIsProse => true;

    public string? Author
    {
        get => _author;
        set => SetLook(ref _author, value);
    }

    public override int Layer => 1;
}
