using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// A host visible buffer. Used for the vertex, index and scene data, which is small and
/// written at most once per frame, so no staging copy is worth the machinery.
/// </summary>
public sealed unsafe class VulkanBuffer : IDisposable
{
    private readonly VulkanContext _context;
    private readonly void* _mapped;

    public VulkanBuffer(VulkanContext context, BufferUsageFlags usage, ulong size)
    {
        _context = context;
        Size = size;

        var info = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        context.Api.CreateBuffer(context.Device, in info, null, out var buffer).ThrowOnError("vkCreateBuffer");
        Handle = buffer;

        context.Api.GetBufferMemoryRequirements(context.Device, buffer, out var requirements);

        var memoryTypeIndex = context.Api.FindMemoryType(
            context.PhysicalDevice,
            requirements.MemoryTypeBits,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        if (memoryTypeIndex < 0)
        {
            throw new InvalidOperationException("No host visible memory type fits this buffer.");
        }

        var allocateInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = (uint)memoryTypeIndex,
        };

        context.Api.AllocateMemory(context.Device, in allocateInfo, null, out var memory)
            .ThrowOnError("vkAllocateMemory");
        Memory = memory;

        context.Api.BindBufferMemory(context.Device, buffer, memory, 0).ThrowOnError("vkBindBufferMemory");

        // Mapped for the buffer's whole life, so writing per frame allocates nothing and
        // makes no call into the driver.
        void* mapped = null;
        context.Api.MapMemory(context.Device, memory, 0, size, 0, ref mapped).ThrowOnError("vkMapMemory");
        _mapped = mapped;
    }

    public Buffer Handle { get; private set; }

    public DeviceMemory Memory { get; private set; }

    public ulong Size { get; }

    public static VulkanBuffer Create<T>(VulkanContext context, BufferUsageFlags usage, ReadOnlySpan<T> data)
        where T : unmanaged
    {
        var buffer = new VulkanBuffer(context, usage, (ulong)(Unsafe.SizeOf<T>() * data.Length));
        buffer.Write(data);
        return buffer;
    }

    /// <summary>The mapped range. Valid for the buffer's whole life.</summary>
    public IntPtr Mapped => (IntPtr)_mapped;

    public void Write<T>(ReadOnlySpan<T> data) where T : unmanaged
        => data.CopyTo(new Span<T>(_mapped, data.Length));

    public void Write<T>(in T value) where T : unmanaged
        => Unsafe.Write(_mapped, value);

    public void Dispose()
    {
        _context.Api.UnmapMemory(_context.Device, Memory);
        _context.Api.DestroyBuffer(_context.Device, Handle, null);
        _context.Api.FreeMemory(_context.Device, Memory, null);

        Handle = default;
        Memory = default;
    }
}
