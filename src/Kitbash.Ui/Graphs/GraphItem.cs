using Avalonia;

namespace Kitbash.Ui.Controls;

/// <summary>
/// Anything on the canvas that has a box: a node, a frame, a note. The box is data rather
/// than layout, so the graph knows where everything is with no control realised.
/// </summary>
public abstract class GraphItem
{
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private bool _isSelected;

    protected GraphItem(string id)
    {
        Id = id;
    }

    /// <summary>Unique across the model. What a link names its ends by.</summary>
    public string Id { get; }

    /// <summary>Anything the app wants to hang off this item.</summary>
    public object? Tag { get; set; }

    public double X
    {
        get => _x;
        set => SetBox(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetBox(ref _y, value);
    }

    public double Width
    {
        get => _width;
        set => SetBox(ref _width, value);
    }

    /// <summary>
    /// A node works this out from its ports and writes it back, so reading it never runs a
    /// calculation and writing it on a node is undone the next time its shape changes.
    /// </summary>
    public double Height
    {
        get => _height;
        set => SetBox(ref _height, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetLook(ref _isSelected, value);
    }

    /// <summary>The item's box in graph units.</summary>
    public Rect Bounds => new(_x, _y, _width, _height);

    /// <summary>Which layer it belongs to. Lower draws first.</summary>
    public abstract int Layer { get; }

    /// <summary>
    /// What a person calls it, or null when it has nothing to rename. This is the one thing
    /// the rename box reads and writes, so it works the same over a node, a frame and a note.
    /// </summary>
    public virtual string? Label
    {
        get => null;
        set { }
    }

    /// <summary>Whether the label is prose rather than a name, so it edits over several lines.</summary>
    public virtual bool LabelIsProse => false;

    /// <summary>Where the label sits, in graph units. What the rename box is put over.</summary>
    public virtual Rect LabelBox(GraphMetrics metrics) => Bounds;

    /// <summary>The model holding it, or null while it is loose.</summary>
    internal GraphModel? Owner { get; set; }

    /// <summary>Which query last returned it, so one spanning several cells is returned once.</summary>
    internal int QueryStamp { get; set; }

    /// <summary>The metrics in force, which is the model's or the dense set while loose.</summary>
    protected GraphMetrics Metrics => Owner?.Metrics ?? GraphMetrics.Dense;

    public void MoveTo(double x, double y)
    {
        if (_x.Equals(x) && _y.Equals(y))
        {
            return;
        }

        var was = Bounds;
        _x = x;
        _y = y;
        Owner?.ItemChanged(this, was, GraphChange.Box);
    }

    /// <summary>Works the box out again, which a node does whenever its shape changes.</summary>
    internal virtual void Refresh()
    {
    }

    /// <summary>
    /// Says the pins have moved without the box having changed. Every wire on the item is
    /// routed again, which nothing else would ask for, since a look change leaves them alone.
    /// </summary>
    protected void Reshaped() => Owner?.ItemChanged(this, Bounds, GraphChange.Box);

    protected bool SetLook<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Owner?.ItemChanged(this, Bounds, GraphChange.Look);
        return true;
    }

    protected bool SetBox<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        var was = Bounds;
        field = value;
        Owner?.ItemChanged(this, was, GraphChange.Box);
        return true;
    }
}
