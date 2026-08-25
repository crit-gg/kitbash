using Avalonia.Media;

namespace Kitbash.Ui.Controls;

/// <summary>What colour a port type is drawn in. A wire takes its source port's colour.</summary>
public interface IPortPalette
{
    IBrush Brush(string type);
}

/// <summary>
/// Colours by type name. An app names the ones it cares about, and anything else is handed a
/// colour off the same short list, chosen from the name so it is the same every run.
/// </summary>
public sealed class PortPalette : IPortPalette
{
    private readonly Dictionary<string, IBrush> _named = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<IBrush> _spare;

    public PortPalette(IReadOnlyList<IBrush> spare)
    {
        _spare = spare.Count > 0 ? spare : [Brushes.White];
    }

    public PortPalette Add(string type, IBrush brush)
    {
        _named[type] = brush;
        return this;
    }

    public IBrush Brush(string type)
    {
        if (_named.TryGetValue(type, out var found))
        {
            return found;
        }

        return _spare[Spread(type) % _spare.Count];
    }

    // FNV over the name, so an unnamed type keeps its colour across runs and machines.
    private static int Spread(string text)
    {
        unchecked
        {
            var hash = 2166136261;

            foreach (var letter in text)
            {
                hash = (hash ^ letter) * 16777619;
            }

            return (int)(hash & 0x7fffffff);
        }
    }
}
