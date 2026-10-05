using System.Runtime.InteropServices;

namespace DrawnUi.Vulkan;

/// <summary>
/// The Vulkan calls the Android Vulkan renderer needs, bound to the system loader (libvulkan.so, API 24+). Android's
/// loader exports the core and the surface / swapchain / android-surface entry points, so they are called directly.
/// Dispatchable handles (instance, physical device, device, queue, command buffer) are pointers; the others are 64-bit.
/// </summary>
internal static unsafe class Vk
{
    private const string Lib = "libvulkan.so";

    public const int SUCCESS = 0;
    public const int NOT_READY = 1;
    public const int TIMEOUT = 2;
    public const int SUBOPTIMAL_KHR = 1000001003;
    public const int ERROR_OUT_OF_DATE_KHR = -1000001004;
    public const int ERROR_SURFACE_LOST_KHR = -1000000000;
    public const int ERROR_DEVICE_LOST = -4;

    public const uint API_VERSION_1_1 = (1u << 22) | (1u << 12);

    public const uint QUEUE_GRAPHICS_BIT = 0x1;

    public const uint FORMAT_R8G8B8A8_UNORM = 37;
    public const uint FORMAT_B8G8R8A8_UNORM = 44;
    public const uint COLOR_SPACE_SRGB_NONLINEAR_KHR = 0;

    public const uint IMAGE_USAGE_TRANSFER_SRC_BIT = 0x1;
    public const uint IMAGE_USAGE_TRANSFER_DST_BIT = 0x2;
    public const uint IMAGE_USAGE_SAMPLED_BIT = 0x4;
    public const uint IMAGE_USAGE_COLOR_ATTACHMENT_BIT = 0x10;
    public const uint IMAGE_USAGE_INPUT_ATTACHMENT_BIT = 0x80;

    public const uint COMPOSITE_ALPHA_OPAQUE_BIT_KHR = 0x1;
    public const uint COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR = 0x2;
    public const uint COMPOSITE_ALPHA_POST_MULTIPLIED_BIT_KHR = 0x4;
    public const uint COMPOSITE_ALPHA_INHERIT_BIT_KHR = 0x8;

    public const uint PRESENT_MODE_FIFO_KHR = 2;

    public const uint IMAGE_LAYOUT_UNDEFINED = 0;
    public const uint IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL = 2;
    public const uint IMAGE_LAYOUT_PRESENT_SRC_KHR = 1000001002;

    public const uint IMAGE_TILING_OPTIMAL = 0;
    public const uint SHARING_MODE_EXCLUSIVE = 0;
    public const uint IMAGE_ASPECT_COLOR_BIT = 0x1;
    public const uint QUEUE_FAMILY_IGNORED = ~0u;

    public const uint PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT = 0x2000;
    public const uint PIPELINE_STAGE_ALL_COMMANDS_BIT = 0x10000;
    public const uint ACCESS_MEMORY_READ_BIT = 0x8000;
    public const uint ACCESS_MEMORY_WRITE_BIT = 0x10000;

    public const uint FENCE_CREATE_SIGNALED_BIT = 0x1;
    public const uint COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT = 0x2;
    public const uint COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT = 0x1;

    public const uint STRUCTURE_TYPE_APPLICATION_INFO = 0;
    public const uint STRUCTURE_TYPE_INSTANCE_CREATE_INFO = 1;
    public const uint STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO = 2;
    public const uint STRUCTURE_TYPE_DEVICE_CREATE_INFO = 3;
    public const uint STRUCTURE_TYPE_SUBMIT_INFO = 4;
    public const uint STRUCTURE_TYPE_FENCE_CREATE_INFO = 8;
    public const uint STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO = 9;
    public const uint STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO = 39;
    public const uint STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO = 40;
    public const uint STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO = 42;
    public const uint STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER = 45;
    public const uint STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR = 1000001000;
    public const uint STRUCTURE_TYPE_PRESENT_INFO_KHR = 1000001001;
    public const uint STRUCTURE_TYPE_ANDROID_SURFACE_CREATE_INFO_KHR = 1000008000;

    [StructLayout(LayoutKind.Sequential)]
    public struct ApplicationInfo
    {
        public uint sType;
        public IntPtr pNext;
        public IntPtr pApplicationName;
        public uint applicationVersion;
        public IntPtr pEngineName;
        public uint engineVersion;
        public uint apiVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InstanceCreateInfo
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
        public ApplicationInfo* pApplicationInfo;
        public uint enabledLayerCount;
        public IntPtr ppEnabledLayerNames;
        public uint enabledExtensionCount;
        public IntPtr ppEnabledExtensionNames;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QueueFamilyProperties
    {
        public uint queueFlags;
        public uint queueCount;
        public uint timestampValidBits;
        public uint granularityWidth;
        public uint granularityHeight;
        public uint granularityDepth;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceQueueCreateInfo
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
        public uint queueFamilyIndex;
        public uint queueCount;
        public float* pQueuePriorities;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceCreateInfo
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
        public uint queueCreateInfoCount;
        public DeviceQueueCreateInfo* pQueueCreateInfos;
        public uint enabledLayerCount;
        public IntPtr ppEnabledLayerNames;
        public uint enabledExtensionCount;
        public IntPtr ppEnabledExtensionNames;
        public IntPtr pEnabledFeatures;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AndroidSurfaceCreateInfoKHR
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
        public IntPtr window;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SurfaceCapabilitiesKHR
    {
        public uint minImageCount;
        public uint maxImageCount;
        public uint currentWidth;
        public uint currentHeight;
        public uint minWidth;
        public uint minHeight;
        public uint maxWidth;
        public uint maxHeight;
        public uint maxImageArrayLayers;
        public uint supportedTransforms;
        public uint currentTransform;
        public uint supportedCompositeAlpha;
        public uint supportedUsageFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SurfaceFormatKHR
    {
        public uint format;
        public uint colorSpace;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SwapchainCreateInfoKHR
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
        public ulong surface;
        public uint minImageCount;
        public uint imageFormat;
        public uint imageColorSpace;
        public uint imageWidth;
        public uint imageHeight;
        public uint imageArrayLayers;
        public uint imageUsage;
        public uint imageSharingMode;
        public uint queueFamilyIndexCount;
        public IntPtr pQueueFamilyIndices;
        public uint preTransform;
        public uint compositeAlpha;
        public uint presentMode;
        public uint clipped;
        public ulong oldSwapchain;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SemaphoreCreateInfo
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FenceCreateInfo
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CommandPoolCreateInfo
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
        public uint queueFamilyIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CommandBufferAllocateInfo
    {
        public uint sType;
        public IntPtr pNext;
        public ulong commandPool;
        public uint level;
        public uint commandBufferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CommandBufferBeginInfo
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
        public IntPtr pInheritanceInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageMemoryBarrier
    {
        public uint sType;
        public IntPtr pNext;
        public uint srcAccessMask;
        public uint dstAccessMask;
        public uint oldLayout;
        public uint newLayout;
        public uint srcQueueFamilyIndex;
        public uint dstQueueFamilyIndex;
        public ulong image;
        public uint aspectMask;
        public uint baseMipLevel;
        public uint levelCount;
        public uint baseArrayLayer;
        public uint layerCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SubmitInfo
    {
        public uint sType;
        public IntPtr pNext;
        public uint waitSemaphoreCount;
        public ulong* pWaitSemaphores;
        public uint* pWaitDstStageMask;
        public uint commandBufferCount;
        public IntPtr* pCommandBuffers;
        public uint signalSemaphoreCount;
        public ulong* pSignalSemaphores;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PresentInfoKHR
    {
        public uint sType;
        public IntPtr pNext;
        public uint waitSemaphoreCount;
        public ulong* pWaitSemaphores;
        public uint swapchainCount;
        public ulong* pSwapchains;
        public uint* pImageIndices;
        public int* pResults;
    }

    [DllImport(Lib)] public static extern int vkCreateInstance(InstanceCreateInfo* createInfo, IntPtr allocator, out IntPtr instance);
    [DllImport(Lib)] public static extern void vkDestroyInstance(IntPtr instance, IntPtr allocator);
    [DllImport(Lib)] public static extern int vkEnumeratePhysicalDevices(IntPtr instance, ref uint count, IntPtr* devices);
    [DllImport(Lib)] public static extern void vkGetPhysicalDeviceProperties(IntPtr physicalDevice, byte* properties);
    [DllImport(Lib)] public static extern void vkGetPhysicalDeviceQueueFamilyProperties(IntPtr physicalDevice, ref uint count, QueueFamilyProperties* properties);
    [DllImport(Lib)] public static extern int vkCreateDevice(IntPtr physicalDevice, DeviceCreateInfo* createInfo, IntPtr allocator, out IntPtr device);
    [DllImport(Lib)] public static extern void vkDestroyDevice(IntPtr device, IntPtr allocator);
    [DllImport(Lib)] public static extern void vkGetDeviceQueue(IntPtr device, uint familyIndex, uint queueIndex, out IntPtr queue);
    [DllImport(Lib)] public static extern int vkDeviceWaitIdle(IntPtr device);
    [DllImport(Lib)] public static extern IntPtr vkGetInstanceProcAddr(IntPtr instance, [MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport(Lib)] public static extern IntPtr vkGetDeviceProcAddr(IntPtr device, [MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(Lib)] public static extern int vkCreateAndroidSurfaceKHR(IntPtr instance, AndroidSurfaceCreateInfoKHR* createInfo, IntPtr allocator, out ulong surface);
    [DllImport(Lib)] public static extern void vkDestroySurfaceKHR(IntPtr instance, ulong surface, IntPtr allocator);
    [DllImport(Lib)] public static extern int vkGetPhysicalDeviceSurfaceSupportKHR(IntPtr physicalDevice, uint queueFamilyIndex, ulong surface, out uint supported);
    [DllImport(Lib)] public static extern int vkGetPhysicalDeviceSurfaceCapabilitiesKHR(IntPtr physicalDevice, ulong surface, out SurfaceCapabilitiesKHR capabilities);
    [DllImport(Lib)] public static extern int vkGetPhysicalDeviceSurfaceFormatsKHR(IntPtr physicalDevice, ulong surface, ref uint count, SurfaceFormatKHR* formats);

    [DllImport(Lib)] public static extern int vkCreateSwapchainKHR(IntPtr device, SwapchainCreateInfoKHR* createInfo, IntPtr allocator, out ulong swapchain);
    [DllImport(Lib)] public static extern void vkDestroySwapchainKHR(IntPtr device, ulong swapchain, IntPtr allocator);
    [DllImport(Lib)] public static extern int vkGetSwapchainImagesKHR(IntPtr device, ulong swapchain, ref uint count, ulong* images);
    [DllImport(Lib)] public static extern int vkAcquireNextImageKHR(IntPtr device, ulong swapchain, ulong timeout, ulong semaphore, ulong fence, out uint imageIndex);
    [DllImport(Lib)] public static extern int vkQueuePresentKHR(IntPtr queue, PresentInfoKHR* presentInfo);

    [DllImport(Lib)] public static extern int vkCreateSemaphore(IntPtr device, SemaphoreCreateInfo* createInfo, IntPtr allocator, out ulong semaphore);
    [DllImport(Lib)] public static extern void vkDestroySemaphore(IntPtr device, ulong semaphore, IntPtr allocator);
    [DllImport(Lib)] public static extern int vkCreateFence(IntPtr device, FenceCreateInfo* createInfo, IntPtr allocator, out ulong fence);
    [DllImport(Lib)] public static extern void vkDestroyFence(IntPtr device, ulong fence, IntPtr allocator);
    [DllImport(Lib)] public static extern int vkWaitForFences(IntPtr device, uint count, ulong* fences, uint waitAll, ulong timeout);
    [DllImport(Lib)] public static extern int vkResetFences(IntPtr device, uint count, ulong* fences);

    [DllImport(Lib)] public static extern int vkCreateCommandPool(IntPtr device, CommandPoolCreateInfo* createInfo, IntPtr allocator, out ulong pool);
    [DllImport(Lib)] public static extern void vkDestroyCommandPool(IntPtr device, ulong pool, IntPtr allocator);
    [DllImport(Lib)] public static extern int vkAllocateCommandBuffers(IntPtr device, CommandBufferAllocateInfo* allocateInfo, IntPtr* commandBuffers);
    [DllImport(Lib)] public static extern int vkResetCommandBuffer(IntPtr commandBuffer, uint flags);
    [DllImport(Lib)] public static extern int vkBeginCommandBuffer(IntPtr commandBuffer, CommandBufferBeginInfo* beginInfo);
    [DllImport(Lib)] public static extern int vkEndCommandBuffer(IntPtr commandBuffer);
    [DllImport(Lib)] public static extern void vkCmdPipelineBarrier(IntPtr commandBuffer, uint srcStageMask, uint dstStageMask, uint dependencyFlags,
        uint memoryBarrierCount, IntPtr memoryBarriers, uint bufferBarrierCount, IntPtr bufferBarriers, uint imageBarrierCount, ImageMemoryBarrier* imageBarriers);
    [DllImport(Lib)] public static extern int vkQueueSubmit(IntPtr queue, uint submitCount, SubmitInfo* submits, ulong fence);

    [DllImport("libc.so")] private static extern int __system_property_get([MarshalAs(UnmanagedType.LPStr)] string name, byte* value);

    /// <summary>An Android system property, empty when it is not set.</summary>
    public static string SystemProperty(string name)
    {
        var value = stackalloc byte[92]; // PROP_VALUE_MAX
        var length = __system_property_get(name, value);
        return length > 0 ? Marshal.PtrToStringUTF8((IntPtr)value, length) : string.Empty;
    }

    [DllImport("libandroid.so")] public static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("libandroid.so")] public static extern void ANativeWindow_release(IntPtr window);
}
