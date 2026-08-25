using System.Globalization;
using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>Which of the graph's four type roles a run of text is.</summary>
public enum GraphTextRole
{
    /// <summary>A node title. Medium weight in the UI family.</summary>
    Title,

    /// <summary>A port name, a frame label, a note.</summary>
    Label,

    /// <summary>A value. Mono, because it is read as data.</summary>
    Value,

    /// <summary>A badge.</summary>
    Badge,
}

/// <summary>
/// Laid out text, kept. Text is the expensive part of drawing a graph at scale, so nothing
/// is laid out twice: a title survives a pan, a zoom and its own container being recycled.
/// </summary>
public sealed class GraphText
{
    // Enough for a very large graph's worth of visible strings. Past it the whole cache goes
    // rather than the oldest entry, since a graph that big is scrolling and will refill.
    private const int Ceiling = 4096;

    private readonly Dictionary<Key, FormattedText> _laid = [];

    private Typeface _ui = Typeface.Default;
    private Typeface _mono = Typeface.Default;
    private double _size = 11.5;
    private double _monoSize = 11;

    /// <summary>Says what the families and sizes are. Everything laid out before is dropped.</summary>
    public void Describe(Typeface ui, Typeface mono, double size, double monoSize)
    {
        if (_ui == ui && _mono == mono && _size.Equals(size) && _monoSize.Equals(monoSize))
        {
            return;
        }

        _ui = ui;
        _mono = mono;
        _size = size;
        _monoSize = monoSize;
        _laid.Clear();
    }

    public void Clear() => _laid.Clear();

    /// <summary>
    /// Text laid out and trimmed to a width. A width of zero or less means no trimming, which
    /// is what a value or a badge takes.
    /// </summary>
    public FormattedText Get(string text, GraphTextRole role, IBrush brush, double width = 0, bool wrap = false)
    {
        var key = new Key(text, role, brush, width > 0 ? (int)width : 0, wrap);

        if (_laid.TryGetValue(key, out var found))
        {
            return found;
        }

        if (_laid.Count >= Ceiling)
        {
            _laid.Clear();
        }

        var mono = role == GraphTextRole.Value;
        var face = mono ? _mono : _ui;
        var size = mono ? _monoSize : _size;

        var laid = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            role switch
            {
                GraphTextRole.Title => new Typeface(face.FontFamily, face.Style, FontWeight.SemiBold),
                GraphTextRole.Badge => new Typeface(face.FontFamily, face.Style, FontWeight.Medium),
                _ => face,
            },
            size,
            brush);

        if (width > 0)
        {
            laid.MaxTextWidth = width;

            if (!wrap)
            {
                laid.MaxLineCount = 1;
                laid.Trimming = TextTrimming.CharacterEllipsis;
            }
        }

        _laid[key] = laid;
        return laid;
    }

    private readonly record struct Key(string Text, GraphTextRole Role, IBrush Brush, int Width, bool Wrap);
}
