using System.Runtime.InteropServices;
using Android.OS;

namespace DrawnUi.Vulkan;

/// <summary>
/// One Vulkan device with its graphics queue and the Skia context drawing on it. The instance and the physical device
/// are shared by the process; every canvas gets its own device and context, because each renders on its own thread and
/// a Vulkan queue may not be used from two threads at once.
/// </summary>
internal sealed unsafe class VulkanGpu : IDisposable
{
    private static readonly object Lock = new();
    private static bool _probed;
    private static bool _available;
    private static IntPtr _instance;
    private static IntPtr _physicalDevice;
    private static uint _queueFamily;
    private static uint _apiVersion;
    private static string _deviceName;

    private static readonly string[] InstanceExtensions = { "VK_KHR_surface", "VK_KHR_android_surface" };
    private static readonly string[] DeviceExtensions = { "VK_KHR_swapchain" };

    // Skia asks for every Vulkan function by name: instance-level through the instance, the rest through the device.
    private static readonly GRVkGetProcedureAddressDelegate GetProc = (name, instance, device) =>
        device != IntPtr.Zero ? Vk.vkGetDeviceProcAddr(device, name) : Vk.vkGetInstanceProcAddr(instance, name);

    public IntPtr Instance => _instance;
    public IntPtr PhysicalDevice => _physicalDevice;
    public uint QueueFamily => _queueFamily;
    public IntPtr Device { get; private set; }
    public IntPtr Queue { get; private set; }
    public GRContext Context { get; private set; }

    private GRVkBackendContext _backend;

    /// <summary>
    /// Whether this device can draw with Vulkan: Android 7+, a Vulkan 1.1 GPU with a graphics queue. Probed once.
    /// False after <see cref="Disable"/>, when a Vulkan canvas failed at run time.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            lock (Lock)
            {
                if (!_probed)
                {
                    _probed = true;
                    try
                    {
                        _available = Probe();
                    }
                    catch (Exception e)
                    {
                        Super.Log($"[Vulkan] not available: {e.Message}");
                        _available = false;
                    }
                }

                return _available;
            }
        }
    }

    /// <summary>The GPU's name, for logs.</summary>
    public static string DeviceName => _deviceName;

    /// <summary>Vulkan failed on this device: every canvas created from now on uses OpenGL.</summary>
    public static void Disable(string why)
    {
        lock (Lock)
        {
            _probed = true;
            _available = false;
        }

        Super.Log($"[Vulkan] disabled, falling back to OpenGL: {why}");
    }

    private static bool Probe()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.N)
            return false;

        var appName = Marshal.StringToHGlobalAnsi("DrawnUI");
        var extensions = AllocStrings(InstanceExtensions);
        try
        {
            var app = new Vk.ApplicationInfo
            {
                sType = Vk.STRUCTURE_TYPE_APPLICATION_INFO,
                pApplicationName = appName,
                pEngineName = appName,
                apiVersion = Vk.API_VERSION_1_1,
            };
            var info = new Vk.InstanceCreateInfo
            {
                sType = Vk.STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pApplicationInfo = &app,
                enabledExtensionCount = (uint)InstanceExtensions.Length,
                ppEnabledExtensionNames = extensions,
            };
            if (Vk.vkCreateInstance(&info, IntPtr.Zero, out var instance) != Vk.SUCCESS || instance == IntPtr.Zero)
                return false;

            uint count = 0;
            Vk.vkEnumeratePhysicalDevices(instance, ref count, null);
            if (count == 0)
            {
                Vk.vkDestroyInstance(instance, IntPtr.Zero);
                return false;
            }

            var devices = stackalloc IntPtr[(int)count];
            Vk.vkEnumeratePhysicalDevices(instance, ref count, devices);

            for (var d = 0; d < count; d++)
            {
                // VkPhysicalDeviceProperties: apiVersion first, deviceName (256 chars) at offset 20
                var properties = stackalloc byte[1024];
                Vk.vkGetPhysicalDeviceProperties(devices[d], properties);
                var apiVersion = *(uint*)properties;
                if (apiVersion < Vk.API_VERSION_1_1)
                    continue;

                uint families = 0;
                Vk.vkGetPhysicalDeviceQueueFamilyProperties(devices[d], ref families, null);
                var props = stackalloc Vk.QueueFamilyProperties[(int)families];
                Vk.vkGetPhysicalDeviceQueueFamilyProperties(devices[d], ref families, props);
                for (uint f = 0; f < families; f++)
                {
                    if ((props[f].queueFlags & Vk.QUEUE_GRAPHICS_BIT) == 0 || props[f].queueCount == 0)
                        continue;

                    _instance = instance;
                    _physicalDevice = devices[d];
                    _queueFamily = f;
                    _apiVersion = Vk.API_VERSION_1_1;
                    _deviceName = Marshal.PtrToStringUTF8((IntPtr)(properties + 20));
                    Super.Log($"[Vulkan] available: {_deviceName}, API {apiVersion >> 22}.{(apiVersion >> 12) & 0x3FF}");
                    return true;
                }
            }

            Vk.vkDestroyInstance(instance, IntPtr.Zero);
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(appName);
            FreeStrings(extensions, InstanceExtensions.Length);
        }
    }

    /// <summary>A device, its queue and a Skia context on it, or null when Vulkan does not work here.</summary>
    public static VulkanGpu Create()
    {
        if (!IsAvailable)
            return null;

        var gpu = new VulkanGpu();
        try
        {
            if (gpu.CreateDevice())
                return gpu;
        }
        catch (Exception e)
        {
            Super.Log($"[Vulkan] device: {e.Message}");
        }

        gpu.Dispose();
        return null;
    }

    private bool CreateDevice()
    {
        var priority = 1f;
        var queueInfo = new Vk.DeviceQueueCreateInfo
        {
            sType = Vk.STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
            queueFamilyIndex = _queueFamily,
            queueCount = 1,
            pQueuePriorities = &priority,
        };
        var extensions = AllocStrings(DeviceExtensions);
        try
        {
            var info = new Vk.DeviceCreateInfo
            {
                sType = Vk.STRUCTURE_TYPE_DEVICE_CREATE_INFO,
                queueCreateInfoCount = 1,
                pQueueCreateInfos = &queueInfo,
                enabledExtensionCount = (uint)DeviceExtensions.Length,
                ppEnabledExtensionNames = extensions,
            };
            if (Vk.vkCreateDevice(_physicalDevice, &info, IntPtr.Zero, out var device) != Vk.SUCCESS || device == IntPtr.Zero)
                return false;

            Device = device;
            Vk.vkGetDeviceQueue(device, _queueFamily, 0, out var queue);
            Queue = queue;
        }
        finally
        {
            FreeStrings(extensions, DeviceExtensions.Length);
        }

        _backend = new GRVkBackendContext
        {
            VkInstance = _instance,
            VkPhysicalDevice = _physicalDevice,
            VkDevice = Device,
            VkQueue = Queue,
            GraphicsQueueIndex = _queueFamily,
            MaxAPIVersion = _apiVersion,
            GetProcedureAddress = GetProc,
            Extensions = GRVkExtensions.Create(GetProc, _instance, _physicalDevice, InstanceExtensions, DeviceExtensions),
        };

        Context = GRContext.CreateVulkan(_backend);
        return Context != null;
    }

    public void Dispose()
    {
        if (Device != IntPtr.Zero)
            Vk.vkDeviceWaitIdle(Device);

        Context?.AbandonContext(true);
        Context?.Dispose();
        Context = null;

        _backend?.Extensions?.Dispose();
        _backend?.Dispose();
        _backend = null;

        if (Device != IntPtr.Zero)
        {
            Vk.vkDestroyDevice(Device, IntPtr.Zero);
            Device = IntPtr.Zero;
        }
    }

    private static IntPtr AllocStrings(string[] values)
    {
        var array = Marshal.AllocHGlobal(IntPtr.Size * values.Length);
        for (var i = 0; i < values.Length; i++)
            Marshal.WriteIntPtr(array, i * IntPtr.Size, Marshal.StringToHGlobalAnsi(values[i]));
        return array;
    }

    private static void FreeStrings(IntPtr array, int count)
    {
        for (var i = 0; i < count; i++)
            Marshal.FreeHGlobal(Marshal.ReadIntPtr(array, i * IntPtr.Size));
        Marshal.FreeHGlobal(array);
    }
}
