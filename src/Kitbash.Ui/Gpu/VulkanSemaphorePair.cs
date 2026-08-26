using System.Runtime.InteropServices;
using Avalonia.Platform;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// The two exportable binary semaphores a composition surface update needs. Both Linux
/// interop backends report Semaphores rather than TimelineSemaphores, so this is the pair
/// form rather than a counter.
/// </summary>
public sealed unsafe class VulkanSemaphorePair : IDisposable
{
    private readonly VulkanContext _context;

    public VulkanSemaphorePair(VulkanContext context)
    {
        _context = context;

        var export = new ExportSemaphoreCreateInfo
        {
            SType = StructureType.ExportSemaphoreCreateInfo,
            HandleTypes = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit
                : ExternalSemaphoreHandleTypeFlags.OpaqueFDBit,
        };

        var info = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo,
            PNext = &export,
        };

        context.Api.CreateSemaphore(context.Device, in info, null, out var available)
            .ThrowOnError("vkCreateSemaphore");
        Available = available;

        context.Api.CreateSemaphore(context.Device, in info, null, out var finished)
            .ThrowOnError("vkCreateSemaphore");
        Finished = finished;
    }

    /// <summary>Signalled by the compositor when it has finished reading the image.</summary>
    public Semaphore Available { get; }

    /// <summary>Signalled by us when the frame is written and the compositor may read.</summary>
    public Semaphore Finished { get; }

    public IPlatformHandle Export(bool finished)
    {
        var semaphore = finished ? Finished : Available;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!_context.Api.TryGetDeviceExtension<KhrExternalSemaphoreWin32>(
                    _context.Instance, _context.Device, out var win32))
            {
                throw new InvalidOperationException("VK_KHR_external_semaphore_win32 is missing.");
            }

            var win32Info = new SemaphoreGetWin32HandleInfoKHR
            {
                SType = StructureType.SemaphoreGetWin32HandleInfoKhr,
                Semaphore = semaphore,
                HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit,
            };

            win32.GetSemaphoreWin32Handle(_context.Device, in win32Info, out var handle)
                .ThrowOnError("vkGetSemaphoreWin32HandleKHR");

            return new PlatformHandle(
                handle, KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaqueNtHandle);
        }

        if (!_context.Api.TryGetDeviceExtension<KhrExternalSemaphoreFd>(
                _context.Instance, _context.Device, out var fdExt))
        {
            throw new InvalidOperationException("VK_KHR_external_semaphore_fd is missing.");
        }

        var fdInfo = new SemaphoreGetFdInfoKHR
        {
            SType = StructureType.SemaphoreGetFDInfoKhr,
            Semaphore = semaphore,
            HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueFDBit,
        };

        fdExt.GetSemaphoreF(_context.Device, in fdInfo, out var fd).ThrowOnError("vkGetSemaphoreFdKHR");

        return new PlatformHandle(
            new IntPtr(fd), KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaquePosixFileDescriptor);
    }

    public void Dispose()
    {
        _context.Api.DestroySemaphore(_context.Device, Available, null);
        _context.Api.DestroySemaphore(_context.Device, Finished, null);
    }
}
