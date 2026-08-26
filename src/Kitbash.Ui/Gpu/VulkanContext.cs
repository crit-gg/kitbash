using System.Runtime.InteropServices;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// An instance, device and queue on the same physical GPU the compositor is using.
/// Avalonia's own device is never touched. Only exported handles cross between them.
/// </summary>
public sealed unsafe class VulkanContext : IDisposable
{
    private const string ValidationLayer = "VK_LAYER_KHRONOS_validation";

    /// <summary>Said to a person, so it names no handle type.</summary>
    private const string Cannot = "The display system here cannot take an image from the GPU.";

    /// <summary>Held so the callback is not collected while the messenger is alive.</summary>
    private static PfnDebugUtilsMessengerCallbackEXT _logCallback;

    private static readonly Lock Sharing = new();
    private static VulkanContext? _shared;
    private static string _sharedInfo = "";
    private static int _users;

    /// <summary>One callback for the process, so the messages it takes belong to no instance.</summary>
    private static readonly List<string> Reported = [];

    private readonly ExtDebugUtils? _debugUtils;
    private readonly DebugUtilsMessengerEXT _messenger;

    private VulkanContext(
        Vk api,
        Instance instance,
        PhysicalDevice physicalDevice,
        Device device,
        Queue queue,
        uint queueFamilyIndex,
        ExtDebugUtils? debugUtils,
        DebugUtilsMessengerEXT messenger)
    {
        Api = api;
        Instance = instance;
        PhysicalDevice = physicalDevice;
        Device = device;
        Queue = queue;
        QueueFamilyIndex = queueFamilyIndex;
        _debugUtils = debugUtils;
        _messenger = messenger;
        Pool = new VulkanCommandBufferPool(api, device, queue, queueFamilyIndex);
    }

    /// <summary>
    /// There is one queue behind every surface, so anything that submits takes this first.
    /// A preview bake on a worker thread and a frame on the UI thread share it.
    /// </summary>
    public static Lock Gate { get; } = new();

    public Vk Api { get; }

    public Instance Instance { get; }

    public PhysicalDevice PhysicalDevice { get; }

    public Device Device { get; }

    public Queue Queue { get; }

    public uint QueueFamilyIndex { get; }

    public VulkanCommandBufferPool Pool { get; }

    /// <summary>What the device advertised, in a form a person can be shown.</summary>
    public string Description { get; private init; } = "";

    /// <summary>True when the validation layer is loaded, which only a developer sees.</summary>
    public bool Validating { get; private init; }

    /// <summary>The device's anisotropy ceiling, or one when it has no anisotropic filtering.</summary>
    public float MaxAnisotropy { get; private init; } = 1f;

    /// <summary>Everything the validation layer has said since the device was made.</summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            lock (Reported)
            {
                return [.. Reported];
            }
        }
    }

    /// <summary>
    /// The one context, made on the first call and shared after it. Every caller pairs this
    /// with <see cref="Release"/>, and the device goes when the last one lets go.
    /// </summary>
    /// <returns>The context, or null with a sentence saying why there is none.</returns>
    public static (VulkanContext? Context, string Info) Acquire(
        ICompositionGpuInterop interop, VulkanNeeds needs = default)
    {
        ArgumentNullException.ThrowIfNull(interop);

        lock (Sharing)
        {
            if (_shared != null)
            {
                _users++;

                return (_shared, _sharedInfo);
            }

            var (context, info) = Create(interop, needs);

            _shared = context;
            _sharedInfo = info;
            _users = context == null ? 0 : 1;

            return (context, info);
        }
    }

    /// <summary>Gives up one hold on the shared context. The device goes with the last one.</summary>
    public static void Release()
    {
        VulkanContext? going = null;

        lock (Sharing)
        {
            if (_shared == null || --_users > 0)
            {
                return;
            }

            going = _shared;
            _shared = null;
            _sharedInfo = "";
            _users = 0;
        }

        going.Dispose();
    }

    /// <summary>Blocks until the device is idle, so teardown order stops mattering.</summary>
    public void WaitIdle() => Api.DeviceWaitIdle(Device);

    public void Dispose()
    {
        Api.DeviceWaitIdle(Device);
        Pool.Dispose();
        Api.DestroyDevice(Device, null);

        if (_debugUtils != null)
        {
            _debugUtils.DestroyDebugUtilsMessenger(Instance, _messenger, null);
            _debugUtils.Dispose();
        }

        Api.DestroyInstance(Instance, null);
    }

    private static (VulkanContext? Context, string Info) Create(
        ICompositionGpuInterop interop, VulkanNeeds needs)
    {
        var api = Vk.GetApi();

        var instanceExtensions = new List<string>
        {
            "VK_KHR_get_physical_device_properties2",
            "VK_KHR_external_memory_capabilities",
            "VK_KHR_external_semaphore_capabilities",
            ExtDebugUtils.ExtensionName,
        };

        var layers = new List<string>();
        var validating = IsLayerAvailable(api, ValidationLayer);

        if (validating)
        {
            layers.Add(ValidationLayer);
        }

        // Interop needs a handle type both sides know. Nothing below works without one.
        var deviceExtensions = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!interop.SupportedImageHandleTypes.Contains(
                    KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaqueNtHandle))
            {
                return (null, Cannot);
            }

            deviceExtensions.Add(KhrExternalMemoryWin32.ExtensionName);
            deviceExtensions.Add(KhrExternalSemaphoreWin32.ExtensionName);
        }
        else
        {
            if (!interop.SupportedImageHandleTypes.Contains(
                    KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor)
                || !interop.SupportedSemaphoreTypes.Contains(
                    KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaquePosixFileDescriptor))
            {
                return (null, Cannot);
            }

            deviceExtensions.Add(KhrExternalMemoryFd.ExtensionName);
            deviceExtensions.Add(KhrExternalSemaphoreFd.ExtensionName);
        }

        Instance vkInstance = default;

        try
        {
            using var applicationName = new ByteString("Kitbash");
            using var engineName = new ByteString("Kitbash");

            var applicationInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = applicationName,
                PEngineName = engineName,
                ApplicationVersion = new Version32(1, 0, 0),
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version13,
            };

            using var pExtensions = new ByteStringList(instanceExtensions);
            using var pLayers = new ByteStringList(layers);

            var instanceInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &applicationInfo,
                PpEnabledExtensionNames = pExtensions,
                EnabledExtensionCount = pExtensions.Count,
                PpEnabledLayerNames = pLayers,
                EnabledLayerCount = pLayers.Count,
            };

            api.CreateInstance(in instanceInfo, null, out vkInstance).ThrowOnError("vkCreateInstance");

            ExtDebugUtils? debugUtils = null;
            DebugUtilsMessengerEXT messenger = default;

            if (api.TryGetInstanceExtension(vkInstance, out ExtDebugUtils utils))
            {
                _logCallback = new PfnDebugUtilsMessengerCallbackEXT(Log);

                var messengerInfo = new DebugUtilsMessengerCreateInfoEXT
                {
                    SType = StructureType.DebugUtilsMessengerCreateInfoExt,
                    MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
                                      | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
                    MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                                  | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                                  | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
                    PfnUserCallback = _logCallback,
                };

                utils.CreateDebugUtilsMessenger(vkInstance, in messengerInfo, null, out messenger)
                    .ThrowOnError("vkCreateDebugUtilsMessengerEXT");

                debugUtils = utils;
            }

            var (context, info) = Pick(
                api, vkInstance, interop, deviceExtensions, needs, debugUtils, messenger, validating);

            if (context != null)
            {
                return (context, info);
            }

            if (debugUtils != null)
            {
                debugUtils.DestroyDebugUtilsMessenger(vkInstance, messenger, null);
                debugUtils.Dispose();
            }

            api.DestroyInstance(vkInstance, null);

            return (null, info);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException)
        {
            if (vkInstance.Handle != default)
            {
                api.DestroyInstance(vkInstance, null);
            }

            return (null, exception.Message);
        }
    }

    /// <summary>
    /// Finds the GPU the compositor is on. UUID on Linux and LUID on Windows are the only
    /// things that tie this instance's physical device to the one behind Avalonia's.
    /// </summary>
    private static (VulkanContext? Context, string Info) Pick(
        Vk api,
        Instance instance,
        ICompositionGpuInterop interop,
        List<string> deviceExtensions,
        VulkanNeeds needs,
        ExtDebugUtils? debugUtils,
        DebugUtilsMessengerEXT messenger,
        bool validating)
    {
        uint deviceCount = 0;
        api.EnumeratePhysicalDevices(instance, ref deviceCount, null).ThrowOnError("vkEnumeratePhysicalDevices");
        var devices = stackalloc PhysicalDevice[(int)deviceCount];
        api.EnumeratePhysicalDevices(instance, ref deviceCount, devices).ThrowOnError("vkEnumeratePhysicalDevices");

        var rejected = new List<string>();

        for (var i = 0u; i < deviceCount; i++)
        {
            var physicalDevice = devices[i];

            var identity = new PhysicalDeviceIDProperties { SType = StructureType.PhysicalDeviceIDProperties };
            var properties = new PhysicalDeviceProperties2
            {
                SType = StructureType.PhysicalDeviceProperties2,
                PNext = &identity,
            };
            api.GetPhysicalDeviceProperties2(physicalDevice, &properties);

            var name = Marshal.PtrToStringAnsi((IntPtr)properties.Properties.DeviceName) ?? "unnamed";
            var apiVersion = properties.Properties.ApiVersion;
            var version = $"{apiVersion >> 22}.{(apiVersion >> 12) & 0x3ff}.{apiVersion & 0xfff}";

            var missing = deviceExtensions.Where(x => !api.IsDeviceExtensionPresent(physicalDevice, x)).ToList();

            if (missing.Count > 0)
            {
                rejected.Add($"{name}: missing {string.Join(", ", missing)}");
                continue;
            }

            if (apiVersion < Vk.Version13)
            {
                rejected.Add($"{name}: reports Vulkan {version}, and 1.3 is needed");
                continue;
            }

            if (!MatchesCompositor(interop, identity))
            {
                rejected.Add($"{name}: not the GPU the compositor is on");
                continue;
            }

            var queueFamilyIndex = FindQueueFamily(api, physicalDevice);

            if (queueFamilyIndex == null)
            {
                rejected.Add($"{name}: no queue family with both graphics and compute");
                continue;
            }

            api.GetPhysicalDeviceFeatures(physicalDevice, out var available);

            if (needs.Tessellation && !available.TessellationShader)
            {
                rejected.Add($"{name}: no tessellation shader stage");
                continue;
            }

            var device = CreateDevice(
                api, physicalDevice, queueFamilyIndex.Value, deviceExtensions, needs, available);
            api.GetDeviceQueue(device, queueFamilyIndex.Value, 0, out var queue);

            var context = new VulkanContext(
                api, instance, physicalDevice, device, queue, queueFamilyIndex.Value, debugUtils, messenger)
            {
                Description = $"{name}, Vulkan {version}",
                Validating = validating,
                MaxAnisotropy = available.SamplerAnisotropy
                    ? properties.Properties.Limits.MaxSamplerAnisotropy
                    : 1f,
            };

            return (context, context.Description);
        }

        return (null, $"No usable GPU. {string.Join("; ", rejected)}");
    }

    private static bool MatchesCompositor(ICompositionGpuInterop interop, PhysicalDeviceIDProperties identity)
    {
        if (interop.DeviceLuid != null && identity.DeviceLuidvalid)
        {
            return new Span<byte>(identity.DeviceLuid, 8).SequenceEqual(interop.DeviceLuid);
        }

        if (interop.DeviceUuid != null)
        {
            return new Span<byte>(identity.DeviceUuid, 16).SequenceEqual(interop.DeviceUuid);
        }

        // Nothing to match against, so anything that imports the handle type will do.
        return true;
    }

    private static uint? FindQueueFamily(Vk api, PhysicalDevice physicalDevice)
    {
        uint familyCount = 0;
        api.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref familyCount, null);
        var families = stackalloc QueueFamilyProperties[(int)familyCount];
        api.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref familyCount, families);

        for (var i = 0u; i < familyCount; i++)
        {
            var flags = families[i].QueueFlags;

            // One queue for both, so a dispatch and a draw need no ownership transfer.
            if (flags.HasFlag(QueueFlags.GraphicsBit) && flags.HasFlag(QueueFlags.ComputeBit))
            {
                return i;
            }
        }

        return null;
    }

    private static Device CreateDevice(
        Vk api,
        PhysicalDevice physicalDevice,
        uint queueFamilyIndex,
        List<string> deviceExtensions,
        VulkanNeeds needs,
        PhysicalDeviceFeatures available)
    {
        var priority = 1f;

        var queueInfo = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = queueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &priority,
        };

        // Dynamic rendering and synchronization2 are what remove render passes, framebuffers
        // and the old barrier form from everything downstream. They are core but opt in.
        var features13 = new PhysicalDeviceVulkan13Features
        {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            DynamicRendering = true,
            Synchronization2 = true,
        };

        var features12 = new PhysicalDeviceVulkan12Features
        {
            SType = StructureType.PhysicalDeviceVulkan12Features,
            TimelineSemaphore = true,
            DescriptorIndexing = true,
            PNext = &features13,
        };

        var features = new PhysicalDeviceFeatures2
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = &features12,
            Features = new PhysicalDeviceFeatures
            {
                TessellationShader = needs.Tessellation,
                FillModeNonSolid = available.FillModeNonSolid,
                SamplerAnisotropy = available.SamplerAnisotropy,
                ShaderSampledImageArrayDynamicIndexing = available.ShaderSampledImageArrayDynamicIndexing,
            },
        };

        using var pDeviceExtensions = new ByteStringList(deviceExtensions);

        var deviceInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            PNext = &features,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueInfo,
            PpEnabledExtensionNames = pDeviceExtensions,
            EnabledExtensionCount = pDeviceExtensions.Count,
        };

        api.CreateDevice(physicalDevice, in deviceInfo, null, out var device).ThrowOnError("vkCreateDevice");

        return device;
    }

    private static bool IsLayerAvailable(Vk api, string layerName)
    {
        uint count = 0;
        api.EnumerateInstanceLayerProperties(&count, null).ThrowOnError("vkEnumerateInstanceLayerProperties");
        var layers = stackalloc LayerProperties[(int)count];
        api.EnumerateInstanceLayerProperties(&count, layers).ThrowOnError("vkEnumerateInstanceLayerProperties");

        for (var i = 0u; i < count; i++)
        {
            if (Marshal.PtrToStringAnsi((IntPtr)layers[i].LayerName) == layerName)
            {
                return true;
            }
        }

        return false;
    }

    private static uint Log(
        DebugUtilsMessageSeverityFlagsEXT severity,
        DebugUtilsMessageTypeFlagsEXT types,
        DebugUtilsMessengerCallbackDataEXT* data,
        void* userData)
    {
        var message = Marshal.PtrToStringAnsi((IntPtr)data->PMessage) ?? "";

        lock (Reported)
        {
            // Bounded, because a broken frame says the same thing every frame and the list
            // is only ever read by a developer.
            if (Reported.Count < 64)
            {
                Reported.Add(message);
            }
        }

        return Vk.False;
    }
}
