using Avalonia.Media.Imaging;
using Kitbash.Core.IO;
using Kitbash.Tools;

namespace Kitbash.ViewModels;

/// <summary>
/// A tool's icon as something a card can draw. Held by the file it came from, since the
/// tools page is read again every time anything about it changes.
/// </summary>
public sealed class ToolIconImages
{
    private readonly IToolIcons _icons;
    private readonly IFileSystem _files;
    private readonly ToolLog _log;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Bitmap?> _held = new(StringComparer.Ordinal);

    public ToolIconImages(IToolIcons icons, IFileSystem files, ToolLog log)
    {
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(log);

        _icons = icons;
        _files = files;
        _log = log;
    }

    /// <summary>The icon in an installed version's folder. Reads a disk, so call it off the UI thread.</summary>
    public Task<Bitmap?> ForAsync(InstalledTool tool) => Task.Run(() => Load(_icons.For(tool)));

    /// <summary>The icon an offer names, which is fetched the first time anything asks.</summary>
    public Task<Bitmap?> ForAsync(OfferedTool tool, CancellationToken cancellationToken) =>
        Task.Run(
            async () => Load(await _icons.ForAsync(tool, cancellationToken).ConfigureAwait(false)),
            cancellationToken);

    private Bitmap? Load(string? path)
    {
        if (path is null)
        {
            return null;
        }

        lock (_gate)
        {
            if (_held.TryGetValue(path, out var held))
            {
                return held;
            }
        }

        Bitmap? image = null;

        try
        {
            using var stream = _files.OpenRead(path);

            image = new Bitmap(stream);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A file that is not an image is not a failure. The card keeps its letter, and
            // the null is held too, so nothing tries to decode it again.
            _log.Say($"'{path}' could not be drawn", exception);
        }

        lock (_gate)
        {
            return _held[path] = image;
        }
    }
}
