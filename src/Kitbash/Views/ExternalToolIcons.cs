using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Kitbash.Views;

/// <summary>
/// The brand mark for one tool, looked up by the key its opener carries. A mark is full
/// colour and belongs to somebody else, so these are the launcher's rather than the
/// design library's.
/// </summary>
public sealed class ExternalToolIcons
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Bitmap?> _held = new(StringComparer.Ordinal);

    /// <summary>
    /// The mark, or null when there is none. A missing file is not a failure, so a tool
    /// whose mark has not been added still draws its row.
    /// </summary>
    public Bitmap? Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        lock (_gate)
        {
            if (_held.TryGetValue(key, out var held))
            {
                return held;
            }

            return _held[key] = Load(key);
        }
    }

    private static Bitmap? Load(string key)
    {
        try
        {
            using var asset = AssetLoader.Open(new Uri($"avares://Kitbash/Assets/ExternalTools/{key}.png"));

            return new Bitmap(asset);
        }
        catch (Exception exception) when (exception is FileNotFoundException or UriFormatException or ArgumentException)
        {
            return null;
        }
    }
}
