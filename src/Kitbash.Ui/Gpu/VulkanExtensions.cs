using Silk.NET.Vulkan;

namespace Kitbash.Ui.Gpu;

/// <summary>The small helpers every call site here would otherwise repeat.</summary>
public static class VulkanExtensions
{
    /// <param name="call">The entry point, so a failure says which one.</param>
    public static void ThrowOnError(this Result result, string call = "vulkan call")
    {
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"{call} failed with {result}.");
        }
    }

    /// <returns>An index into the device's memory types, or minus one when none match.</returns>
    public static int FindMemoryType(
        this Vk api, PhysicalDevice physicalDevice, uint memoryTypeBits, MemoryPropertyFlags flags)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.GetPhysicalDeviceMemoryProperties(physicalDevice, out var properties);

        for (var i = 0; i < properties.MemoryTypeCount; i++)
        {
            var isCandidate = (memoryTypeBits & (1u << i)) != 0;

            if (isCandidate && properties.MemoryTypes[i].PropertyFlags.HasFlag(flags))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>One image barrier through synchronization2, recorded into an open buffer.</summary>
    public static unsafe void Barrier(
        this Vk api,
        CommandBuffer buffer,
        Image image,
        ImageAspectFlags aspect,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        PipelineStageFlags2 sourceStage,
        AccessFlags2 sourceAccess,
        PipelineStageFlags2 destinationStage,
        AccessFlags2 destinationAccess,
        uint baseMip = 0,
        uint mipCount = 1,
        uint layerCount = 1)
    {
        ArgumentNullException.ThrowIfNull(api);

        var barrier = new ImageMemoryBarrier2
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = sourceStage,
            SrcAccessMask = sourceAccess,
            DstStageMask = destinationStage,
            DstAccessMask = destinationAccess,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(aspect, baseMip, mipCount, 0, layerCount),
        };

        var dependency = new DependencyInfo
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier,
        };

        api.CmdPipelineBarrier2(buffer, &dependency);
    }
}
