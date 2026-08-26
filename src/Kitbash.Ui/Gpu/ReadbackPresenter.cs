using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Silk.NET.Vulkan;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// The path for a compositor that imports nothing. The frame is drawn into an image of our
/// own, copied back and painted like any other bitmap, so the app's own render backend is
/// left alone and no window gives up its transparency for it.
/// </summary>
internal sealed unsafe class ReadbackPresenter : IGpuPresenter
{
    private readonly VulkanContext _context;

    /// <summary>
    /// Two, because the render thread may still be reading the one it was given while the
    /// next frame is written. Writing into that one would tear.
    /// </summary>
    private readonly WriteableBitmap?[] _bitmaps = new WriteableBitmap?[2];

    private VulkanImage? _target;
    private VulkanBuffer? _staging;
    private PixelSize _size;
    private int _next;
    private WriteableBitmap? _shown;

    public ReadbackPresenter(VulkanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <summary>The frame is copied back, so the surface paints it and the layout clips it.</summary>
    public bool IsHandedOver => false;

    public bool TryBeginFrame(PixelSize size, out GpuFrame frame)
    {
        frame = default;

        if (size.Width <= 0 || size.Height <= 0)
        {
            return false;
        }

        _context.Pool.Recycle();
        Allocate(size);

        var recording = _context.Pool.Begin();

        _target!.Transition(
            recording.Buffer,
            ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            AccessFlags2.ColorAttachmentWriteBit);

        frame = new GpuFrame(_target, recording, this);

        return true;
    }

    public void EndFrame(GpuFrame frame)
    {
        var target = _target!;
        var staging = _staging!;

        target.Transition(
            frame.Buffer,
            ImageLayout.TransferSrcOptimal,
            PipelineStageFlags2.AllTransferBit,
            AccessFlags2.TransferReadBit);

        var region = new BufferImageCopy
        {
            ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
            ImageExtent = new Extent3D((uint)_size.Width, (uint)_size.Height, 1),
        };

        _context.Api.CmdCopyImageToBuffer(
            frame.Buffer, target.Handle, ImageLayout.TransferSrcOptimal, staging.Handle, 1, in region);

        var fence = frame.Recording.Fence;
        frame.Recording.Submit();

        // The pixels are wanted now, so this waits rather than pipelining. A viewport sized
        // frame is well under a millisecond and only a moving view asks for another.
        _context.Api.WaitForFences(_context.Device, 1, in fence, true, ulong.MaxValue)
            .ThrowOnError("vkWaitForFences");

        _shown = Take();
    }

    public void Present(DrawingContext context, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_shown is { } bitmap)
        {
            context.DrawImage(bitmap, new Rect(bitmap.Size), bounds);
        }
    }

    public ValueTask DisposeAsync()
    {
        _shown = null;

        for (var i = 0; i < _bitmaps.Length; i++)
        {
            _bitmaps[i]?.Dispose();
            _bitmaps[i] = null;
        }

        _staging?.Dispose();
        _target?.Dispose();
        _staging = null;
        _target = null;

        return ValueTask.CompletedTask;
    }

    /// <summary>Copies the staged pixels into the bitmap the render thread is not holding.</summary>
    private WriteableBitmap Take()
    {
        var bitmap = _bitmaps[_next]!;
        _next = (_next + 1) % _bitmaps.Length;

        using var locked = bitmap.Lock();

        var rowBytes = _size.Width * 4;
        var source = (byte*)_staging!.Mapped;
        var target = (byte*)locked.Address;

        for (var row = 0; row < _size.Height; row++)
        {
            System.Buffer.MemoryCopy(
                source + ((long)row * rowBytes), target + ((long)row * locked.RowBytes), rowBytes, rowBytes);
        }

        return bitmap;
    }

    private void Allocate(PixelSize size)
    {
        if (_size == size && _target != null)
        {
            return;
        }

        _context.WaitIdle();

        _shown = null;
        _staging?.Dispose();
        _target?.Dispose();

        _target = new VulkanImage(_context, new ImageDescription
        {
            Format = Format.R8G8B8A8Unorm,
            Size = size,
            Usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
        });

        _staging = new VulkanBuffer(
            _context, BufferUsageFlags.TransferDstBit, (ulong)((long)size.Width * size.Height * 4));

        for (var i = 0; i < _bitmaps.Length; i++)
        {
            _bitmaps[i]?.Dispose();
            _bitmaps[i] = new WriteableBitmap(
                size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
        }

        _size = size;
        _next = 0;
    }
}
