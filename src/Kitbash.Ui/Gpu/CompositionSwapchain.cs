using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// A ring of exported images the compositor imports once each. Not a VK_KHR_swapchain:
/// there is no surface and no present mode, only handles the compositor reads.
/// </summary>
public sealed class CompositionSwapchain : IAsyncDisposable
{
    private readonly VulkanContext _context;
    private readonly ICompositionGpuInterop _interop;
    private readonly CompositionDrawingSurface _target;
    private readonly List<SwapchainImage> _pending = new();

    public CompositionSwapchain(
        VulkanContext context, ICompositionGpuInterop interop, CompositionDrawingSurface target)
    {
        _context = context;
        _interop = interop;
        _target = target;
    }

    /// <summary>
    /// Opens a command buffer with the target already a colour attachment. Returns false
    /// when every image of this size is still in flight, which is the frame to skip.
    /// </summary>
    public bool TryBeginFrame(PixelSize size, out Frame frame)
    {
        frame = default;

        if (size.Width <= 0 || size.Height <= 0)
        {
            return false;
        }

        _context.Pool.Recycle();

        var image = Reclaim(size) ?? new SwapchainImage(_context, _interop, size);
        _pending.Remove(image);

        var recording = _context.Pool.Begin();

        // The compositor's own submissions touched this image since we last wrote it, so the
        // transition starts from Undefined and the previous contents are discarded.
        image.Target.Invalidate();
        image.Target.Transition(
            recording.Buffer,
            ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags2.ColorAttachmentOutputBit,
            AccessFlags2.ColorAttachmentWriteBit);

        frame = new Frame(image, recording);
        return true;
    }

    /// <summary>
    /// Closes the frame with one submission and hands the image over. The wait and signal
    /// semaphores are what keep the compositor and this device off each other's writes.
    /// </summary>
    public void EndFrame(Frame frame)
    {
        var image = frame.Image;

        image.Target.Transition(
            frame.Recording.Buffer,
            ImageLayout.TransferSrcOptimal,
            PipelineStageFlags2.AllTransferBit,
            AccessFlags2.TransferReadBit);

        Span<Semaphore> wait = [image.Semaphores.Available];
        Span<Semaphore> signal = [image.Semaphores.Finished];

        // Nothing has signalled Available before the first hand over, so the first frame of
        // an image waits on nothing.
        frame.Recording.Submit(image.FirstUse ? default : wait, signal);

        image.Present(_target);
        _pending.Add(image);
    }

    /// <summary>
    /// Drops images the compositor is done with that no longer match, and returns one that
    /// does. Null unless at least two of this size are ready, so one stays in flight and
    /// the UI thread never waits on the image it just handed over.
    /// </summary>
    private SwapchainImage? Reclaim(PixelSize size)
    {
        SwapchainImage? found = null;
        var foundMore = false;

        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var image = _pending[i];
            var ready = image.LastPresent is null or { Status: TaskStatus.RanToCompletion };
            var matches = image.Size == size;

            if (image.LastPresent?.IsFaulted == true || (!matches && ready))
            {
                _ = image.DisposeAsync();
                _pending.RemoveAt(i);
                continue;
            }

            if (!matches || !ready)
            {
                continue;
            }

            if (found == null)
            {
                found = image;
            }
            else
            {
                foundMore = true;
            }
        }

        return foundMore ? found : null;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var image in _pending)
        {
            await image.DisposeAsync();
        }

        _pending.Clear();
    }

    public readonly record struct Frame(SwapchainImage Image, VulkanCommandBufferPool.Recording Recording);
}

/// <summary>One image in the ring, with the semaphores and imports that belong to it.</summary>
public sealed class SwapchainImage : IAsyncDisposable
{
    private readonly ICompositionGpuInterop _interop;
    private ICompositionImportedGpuImage? _imported;
    private ICompositionImportedGpuSemaphore? _importedAvailable;
    private ICompositionImportedGpuSemaphore? _importedFinished;

    public SwapchainImage(VulkanContext context, ICompositionGpuInterop interop, PixelSize size)
    {
        _interop = interop;
        Size = size;

        // R8G8B8A8UNorm is the one format the importer understands, so a surface working in
        // any other format converts on the way into this image.
        Target = new VulkanImage(context, new ImageDescription
        {
            Format = Format.R8G8B8A8Unorm,
            Size = size,
            Usage = ImageUsageFlags.ColorAttachmentBit
                    | ImageUsageFlags.TransferSrcBit
                    | ImageUsageFlags.TransferDstBit
                    | ImageUsageFlags.SampledBit,
            Exportable = true,
        });

        Semaphores = new VulkanSemaphorePair(context);
    }

    public VulkanImage Target { get; }

    public VulkanSemaphorePair Semaphores { get; }

    public PixelSize Size { get; }

    public Task? LastPresent { get; private set; }

    public bool FirstUse { get; private set; } = true;

    public void Present(CompositionDrawingSurface target)
    {
        // Exported once. The importer owns each handle it is given, so a second export
        // would leak a file descriptor per frame.
        _imported ??= _interop.ImportImage(
            Target.Export(),
            new PlatformGraphicsExternalImageProperties
            {
                Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
                Width = Size.Width,
                Height = Size.Height,
                MemorySize = Target.MemorySize,
                TopLeftOrigin = true,
            });

        _importedFinished ??= _interop.ImportSemaphore(Semaphores.Export(true));
        _importedAvailable ??= _interop.ImportSemaphore(Semaphores.Export(false));

        FirstUse = false;
        LastPresent = target.UpdateWithSemaphoresAsync(_imported, _importedFinished, _importedAvailable);
    }

    public async ValueTask DisposeAsync()
    {
        if (LastPresent != null)
        {
            // A faulted present is still a finished one, and the images behind it have to go.
            try
            {
                await LastPresent;
            }
            catch (Exception)
            {
            }
        }

        if (_imported != null)
        {
            await _imported.DisposeAsync();
        }

        if (_importedFinished != null)
        {
            await _importedFinished.DisposeAsync();
        }

        if (_importedAvailable != null)
        {
            await _importedAvailable.DisposeAsync();
        }

        Semaphores.Dispose();
        Target.Dispose();
    }
}
