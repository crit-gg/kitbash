using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// An image, its memory and its views. Exportable ones carry the external memory chain the
/// compositor needs. Layout is tracked here so a caller only names where it wants to go.
/// </summary>
public sealed unsafe class VulkanImage : IDisposable
{
    private static int _live;
    private static int _created;

    private readonly VulkanContext _context;
    private readonly Vk _api;
    private readonly ImageView[] _storageViews;
    private ImageLayout _layout = ImageLayout.Undefined;
    private PipelineStageFlags2 _stage = PipelineStageFlags2.None;
    private AccessFlags2 _access = AccessFlags2.None;

    public VulkanImage(VulkanContext context, in ImageDescription description)
    {
        _context = context;
        _api = context.Api;
        Description = description;
        Aspect = description.Depth ? ImageAspectFlags.DepthBit : ImageAspectFlags.ColorBit;

        var handleType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ExternalMemoryHandleTypeFlags.OpaqueWin32Bit
            : ExternalMemoryHandleTypeFlags.OpaqueFDBit;

        var externalImage = new ExternalMemoryImageCreateInfo
        {
            SType = StructureType.ExternalMemoryImageCreateInfo,
            HandleTypes = handleType,
        };

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            PNext = description.Exportable ? &externalImage : null,
            Flags = description.Cube ? ImageCreateFlags.CreateCubeCompatibleBit : 0,
            ImageType = ImageType.Type2D,
            Format = description.Format,
            Extent = new Extent3D((uint)description.Size.Width, (uint)description.Size.Height, 1),
            MipLevels = description.MipLevels,
            ArrayLayers = description.Layers,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = description.Usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };

        _api.CreateImage(context.Device, in imageInfo, null, out var image).ThrowOnError("vkCreateImage");
        Handle = image;

        _api.GetImageMemoryRequirements(context.Device, image, out var requirements);

        // A dedicated allocation is what lets the importer treat the fd as the whole image.
        var dedicated = new MemoryDedicatedAllocateInfo
        {
            SType = StructureType.MemoryDedicatedAllocateInfo,
            Image = image,
        };

        var export = new ExportMemoryAllocateInfo
        {
            SType = StructureType.ExportMemoryAllocateInfo,
            HandleTypes = handleType,
            PNext = &dedicated,
        };

        var memoryTypeIndex = _api.FindMemoryType(
            context.PhysicalDevice, requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit);

        if (memoryTypeIndex < 0)
        {
            throw new InvalidOperationException("No device local memory type fits this image.");
        }

        var allocateInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            PNext = description.Exportable ? &export : &dedicated,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = (uint)memoryTypeIndex,
        };

        _api.AllocateMemory(context.Device, in allocateInfo, null, out var memory)
            .ThrowOnError("vkAllocateMemory");

        Memory = memory;
        MemorySize = requirements.Size;

        _api.BindImageMemory(context.Device, image, memory, 0).ThrowOnError("vkBindImageMemory");

        // A view is only legal for an image a shader reads or writes. One that exists to be
        // copied through, such as a readback staging target, gets none.
        const ImageUsageFlags viewable =
            ImageUsageFlags.SampledBit
            | ImageUsageFlags.StorageBit
            | ImageUsageFlags.ColorAttachmentBit
            | ImageUsageFlags.DepthStencilAttachmentBit;

        if ((description.Usage & viewable) != 0)
        {
            View = CreateView(
                description.Cube ? ImageViewType.TypeCube : ImageViewType.Type2D,
                0,
                description.MipLevels,
                0,
                description.Layers);
        }

        // A storage image descriptor is only ever one mip, so a compute write needs its own
        // view per level. Cube faces are layers of a 2D array from the shader's side.
        _storageViews = new ImageView[description.MipLevels];

        if (description.Usage.HasFlag(ImageUsageFlags.StorageBit))
        {
            for (var mip = 0u; mip < description.MipLevels; mip++)
            {
                _storageViews[mip] = CreateView(
                    description.Layers > 1 ? ImageViewType.Type2DArray : ImageViewType.Type2D,
                    mip,
                    1,
                    0,
                    description.Layers);
            }
        }

        Interlocked.Increment(ref _live);
        Interlocked.Increment(ref _created);
    }

    public ImageDescription Description { get; }

    public Image Handle { get; private set; }

    /// <summary>Every mip and every layer, for sampling.</summary>
    public ImageView View { get; private set; }

    public DeviceMemory Memory { get; private set; }

    public Format Format => Description.Format;

    public PixelSize Size => Description.Size;

    public uint MipLevels => Description.MipLevels;

    public ulong MemorySize { get; }

    public bool Exportable => Description.Exportable;

    public ImageAspectFlags Aspect { get; }

    public ImageLayout Layout => _layout;

    /// <summary>Images alive now and images ever made, so a test can prove a resize frees them.</summary>
    public static (int Live, int Created) Census => (Volatile.Read(ref _live), Volatile.Read(ref _created));

    /// <summary>A single mip, as a storage image descriptor wants it.</summary>
    public ImageView StorageView(uint mip) => _storageViews[mip];

    private ImageView CreateView(
        ImageViewType type, uint baseMip, uint mipCount, uint baseLayer, uint layerCount)
    {
        var info = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = Handle,
            ViewType = type,
            Format = Description.Format,
            SubresourceRange = new ImageSubresourceRange(Aspect, baseMip, mipCount, baseLayer, layerCount),
        };

        _api.CreateImageView(_context.Device, in info, null, out var view).ThrowOnError("vkCreateImageView");
        return view;
    }

    /// <summary>
    /// Records the transition into an open buffer and remembers where the image now is.
    /// The source side comes from the last transition, so callers never restate it.
    /// </summary>
    public void Transition(
        CommandBuffer buffer,
        ImageLayout layout,
        PipelineStageFlags2 stage,
        AccessFlags2 access)
    {
        _api.Barrier(
            buffer,
            Handle,
            Aspect,
            _layout,
            layout,
            _stage,
            _access,
            stage,
            access,
            0,
            Description.MipLevels,
            Description.Layers);

        _layout = layout;
        _stage = stage;
        _access = access;
    }

    /// <summary>
    /// Fills mips one and up by blitting each level down to the next. The whole image is left
    /// readable by a fragment shader. Only valid on an image with transfer usage on both sides.
    /// </summary>
    public void GenerateMips(CommandBuffer buffer)
    {
        if (Description.MipLevels <= 1)
        {
            Transition(
                buffer,
                ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags2.FragmentShaderBit,
                AccessFlags2.ShaderSampledReadBit);
            return;
        }

        // Only level zero holds anything yet, so only level zero is transitioned from a
        // layout it is really in. The rest are sourced from Undefined, which discards.
        _api.Barrier(
            buffer, Handle, Aspect,
            _layout, ImageLayout.TransferSrcOptimal,
            _stage, _access,
            PipelineStageFlags2.AllTransferBit, AccessFlags2.TransferReadBit,
            0, 1, Description.Layers);

        var width = Description.Size.Width;
        var height = Description.Size.Height;

        for (var mip = 1u; mip < Description.MipLevels; mip++)
        {
            var nextWidth = int.Max(width / 2, 1);
            var nextHeight = int.Max(height / 2, 1);

            // The destination level only, since every level above it is already a source.
            _api.Barrier(
                buffer, Handle, Aspect,
                ImageLayout.Undefined, ImageLayout.TransferDstOptimal,
                PipelineStageFlags2.AllTransferBit, AccessFlags2.None,
                PipelineStageFlags2.AllTransferBit, AccessFlags2.TransferWriteBit,
                mip, 1, Description.Layers);

            var blit = new ImageBlit
            {
                SrcSubresource = new ImageSubresourceLayers(Aspect, mip - 1, 0, Description.Layers),
                DstSubresource = new ImageSubresourceLayers(Aspect, mip, 0, Description.Layers),
            };

            blit.SrcOffsets.Element1 = new Offset3D(width, height, 1);
            blit.DstOffsets.Element1 = new Offset3D(nextWidth, nextHeight, 1);

            _api.CmdBlitImage(
                buffer,
                Handle, ImageLayout.TransferSrcOptimal,
                Handle, ImageLayout.TransferDstOptimal,
                1, in blit, Filter.Linear);

            _api.Barrier(
                buffer, Handle, Aspect,
                ImageLayout.TransferDstOptimal, ImageLayout.TransferSrcOptimal,
                PipelineStageFlags2.AllTransferBit, AccessFlags2.TransferWriteBit,
                PipelineStageFlags2.AllTransferBit, AccessFlags2.TransferReadBit,
                mip, 1, Description.Layers);

            width = nextWidth;
            height = nextHeight;
        }

        // Every level is a transfer source now, so the tracked state is already right.
        _layout = ImageLayout.TransferSrcOptimal;
        _stage = PipelineStageFlags2.AllTransferBit;
        _access = AccessFlags2.TransferReadBit;

        Transition(
            buffer,
            ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags2.FragmentShaderBit,
            AccessFlags2.ShaderSampledReadBit);
    }

    /// <summary>
    /// Forgets the tracked layout. The compositor's own submissions touch an exported image
    /// between our frames, so the next transition has to start from Undefined.
    /// </summary>
    public void Invalidate()
    {
        _layout = ImageLayout.Undefined;
        _stage = PipelineStageFlags2.None;
        _access = AccessFlags2.None;
    }

    /// <summary>
    /// Hands the memory to the compositor. Ownership of the returned handle passes to the
    /// importer, so this is called once per image.
    /// </summary>
    public IPlatformHandle Export()
    {
        if (!Exportable)
        {
            throw new InvalidOperationException("This image was not created exportable.");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!_api.TryGetDeviceExtension<KhrExternalMemoryWin32>(
                    _context.Instance, _context.Device, out var win32))
            {
                throw new InvalidOperationException("VK_KHR_external_memory_win32 is missing.");
            }

            var win32Info = new MemoryGetWin32HandleInfoKHR
            {
                SType = StructureType.MemoryGetWin32HandleInfoKhr,
                Memory = Memory,
                HandleType = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit,
            };

            win32.GetMemoryWin32Handle(_context.Device, in win32Info, out var handle)
                .ThrowOnError("vkGetMemoryWin32HandleKHR");

            return new PlatformHandle(
                handle, KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaqueNtHandle);
        }

        if (!_api.TryGetDeviceExtension<KhrExternalMemoryFd>(_context.Instance, _context.Device, out var fdExt))
        {
            throw new InvalidOperationException("VK_KHR_external_memory_fd is missing.");
        }

        var fdInfo = new MemoryGetFdInfoKHR
        {
            SType = StructureType.MemoryGetFDInfoKhr,
            Memory = Memory,
            HandleType = ExternalMemoryHandleTypeFlags.OpaqueFDBit,
        };

        fdExt.GetMemoryF(_context.Device, in fdInfo, out var fd).ThrowOnError("vkGetMemoryFdKHR");

        return new PlatformHandle(
            new IntPtr(fd), KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor);
    }

    public void Dispose()
    {
        foreach (var view in _storageViews)
        {
            if (view.Handle != default)
            {
                _api.DestroyImageView(_context.Device, view, null);
            }
        }

        if (View.Handle != default)
        {
            _api.DestroyImageView(_context.Device, View, null);
        }

        _api.DestroyImage(_context.Device, Handle, null);
        _api.FreeMemory(_context.Device, Memory, null);

        View = default;
        Handle = default;
        Memory = default;

        Interlocked.Decrement(ref _live);
    }
}
