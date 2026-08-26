using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Silk.NET.Vulkan;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// Copies a rendered surface out to a PNG. A compositor that will not raise a window makes
/// a desktop screenshot unreliable, so a GPU surface is checked through this instead.
/// </summary>
public sealed class FrameCapture : IDisposable
{
    private readonly VulkanContext _context;
    private readonly string _path;
    private VulkanBuffer? _staging;
    private PixelSize _size;
    private bool _copied;

    public FrameCapture(VulkanContext context, string path)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _context = context;
        _path = path;
    }

    /// <summary>True once the file is on disk and nothing more is needed.</summary>
    public bool Done { get; private set; }

    /// <summary>
    /// Records the copy. The target is already a transfer source by the time a frame ends,
    /// so this only adds the copy itself.
    /// </summary>
    public void Record(CommandBuffer buffer, VulkanImage target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_copied || Done)
        {
            return;
        }

        _size = target.Size;
        _staging ??= new VulkanBuffer(
            _context, BufferUsageFlags.TransferDstBit, (ulong)(_size.Width * _size.Height * 4));

        target.Transition(
            buffer,
            ImageLayout.TransferSrcOptimal,
            PipelineStageFlags2.AllTransferBit,
            AccessFlags2.TransferReadBit);

        var region = new BufferImageCopy
        {
            ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
            ImageExtent = new Extent3D((uint)_size.Width, (uint)_size.Height, 1),
        };

        _context.Api.CmdCopyImageToBuffer(
            buffer, target.Handle, ImageLayout.TransferSrcOptimal, _staging.Handle, 1, in region);

        _copied = true;
    }

    /// <summary>Waits for the copy and writes the file. Called a frame after <see cref="Record"/>.</summary>
    public void Save()
    {
        if (!_copied || Done || _staging == null)
        {
            return;
        }

        _context.WaitIdle();

        using var bitmap = new Bitmap(
            PixelFormat.Rgba8888,
            AlphaFormat.Opaque,
            _staging.Mapped,
            _size,
            new Vector(96, 96),
            _size.Width * 4);

        using (var file = File.Create(_path))
        {
            bitmap.Save(file, new PngBitmapEncoderOptions());
        }

        Done = true;
    }

    public void Dispose() => _staging?.Dispose();
}
