using Silk.NET.Vulkan;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// One frame handed to a surface. The target is already a color attachment and the buffer
/// is open. Whoever gave it out is what puts it on screen, and a surface never says how.
/// </summary>
public readonly record struct GpuFrame(
    VulkanImage Target, VulkanCommandBufferPool.Recording Recording, object? Owner)
{
    public CommandBuffer Buffer => Recording.Buffer;
}
