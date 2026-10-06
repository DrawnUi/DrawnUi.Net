using Android.Content;
using Android.Graphics;
using Android.Opengl;
using Android.Runtime;
using Android.Views;
using DrawnUi.Vulkan;
using SKPaintGLSurfaceEventArgs = SkiaSharp.Views.Android.SKPaintGLSurfaceEventArgs;

namespace DrawnUi.Views;

/// <summary>
/// The hardware-accelerated canvas drawn with Vulkan: a TextureView whose window gets a Vulkan swapchain, drawn on a
/// thread of its own like the OpenGL TextureView it replaces (same PaintSurface, RenderMode and RequestRender).
/// Each frame DrawnUI draws straight into the acquired swapchain image. SkiaSharp cannot hand Skia semaphores or a
/// present layout, so the view orders the work itself on the one queue: a barrier that waits for the acquire moves the
/// image to color-attachment layout before Skia's commands, and one after Skia's flush moves it to present layout and
/// signals the present. When Vulkan fails here, <see cref="VulkanFailed"/> asks for an OpenGL canvas instead.
/// </summary>
public unsafe class SkiaVulkanTextureView : TextureView, TextureView.ISurfaceTextureListener, IGpuTextureView
{
    private const int FramesInFlight = 2;
    private const ulong WaitNanos = 2_000_000_000; // a frame that cannot get an image or a fence in 2 s: the window is gone

    private readonly object _sync = new();
    private readonly float _density;
    private Thread _thread;

    // shared with the UI thread, under _sync
    private bool _exit;
    private bool _renderRequested;
    private bool _continuous;
    private IntPtr _window;
    private Surface _windowSurface;
    private bool _windowChanged;
    private int _width;
    private int _height;
    private bool _sizeChanged;
    private bool _releaseWindow;
    private bool _windowReleased;

    // render thread only
    private VulkanGpu _gpu;
    private ulong _surface;
    private ulong _swapchain;
    private bool _recreateSwapchain;
    private ulong[] _images = Array.Empty<ulong>();
    private SKSurface[] _surfaces = Array.Empty<SKSurface>();
    private GRBackendRenderTarget[] _targets = Array.Empty<GRBackendRenderTarget>();
    private SKPaintGLSurfaceEventArgs[] _args = Array.Empty<SKPaintGLSurfaceEventArgs>();
    private ulong[] _renderDone = Array.Empty<ulong>();
    private readonly ulong[] _acquired = new ulong[FramesInFlight];
    private readonly ulong[] _fences = new ulong[FramesInFlight];
    private readonly IntPtr[] _before = new IntPtr[FramesInFlight];
    private readonly IntPtr[] _after = new IntPtr[FramesInFlight];
    private ulong _commandPool;
    private uint _format;
    private SKColorType _colorType;
    private int _swapWidth;
    private int _swapHeight;
    private int _frame;
    private SKSurface _retained;
    private SKPaintGLSurfaceEventArgs _retainedArgs;

    public SkiaVulkanTextureView(Context context) : base(context)
    {
        _density = Resources?.DisplayMetrics?.Density ?? 1;
        SurfaceTextureListener = this;
    }

    protected SkiaVulkanTextureView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    /// <summary>Raised on the render thread with the surface to draw this frame on.</summary>
    public event EventHandler<SKPaintGLSurfaceEventArgs> PaintSurface;

    /// <summary>Raised on the UI thread when Vulkan failed on this device: the canvas should be recreated with OpenGL.</summary>
    public event EventHandler<string> VulkanFailed;

    /// <summary>The Skia context drawing on this view, null until the first frame.</summary>
    public GRContext GRContext => _gpu?.Context;

    /// <summary>Draws in device-independent units, the surface scaled by the screen density.</summary>
    public bool IgnorePixelScaling { get; set; }

    /// <summary>
    /// Keep the drawing across frames (AcceleratedRetained): DrawnUI draws on a surface of its own that is copied into
    /// the swapchain image every frame, as a swapchain image does not keep the previous frame.
    /// </summary>
    public bool Retained { get; set; }

    public Rendermode RenderMode
    {
        get => _continuous ? Rendermode.Continuously : Rendermode.WhenDirty;
        set
        {
            lock (_sync)
            {
                _continuous = value == Rendermode.Continuously;
                Monitor.PulseAll(_sync);
            }
        }
    }

    public void RequestRender()
    {
        lock (_sync)
        {
            _renderRequested = true;
            Monitor.PulseAll(_sync);
        }
    }

    #region SURFACE (UI thread)

    public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
    {
        var windowSurface = new Surface(surface);
        var window = Vk.ANativeWindow_fromSurface(JNIEnv.Handle, windowSurface.Handle);
        lock (_sync)
        {
            _windowSurface = windowSurface;
            _window = window;
            _windowChanged = true;
            _width = width;
            _height = height;
            _sizeChanged = true;
            _renderRequested = true;
            if (_thread == null)
            {
                _thread = new Thread(Run) { Name = "DrawnUI Vulkan", IsBackground = true };
                _thread.Start();
            }

            Monitor.PulseAll(_sync);
        }
    }

    public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
    {
        lock (_sync)
        {
            _width = width;
            _height = height;
            _sizeChanged = true;
            _renderRequested = true;
            Monitor.PulseAll(_sync);
        }
    }

    public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
    {
        ReleaseWindow();
        return true;
    }

    public void OnSurfaceTextureUpdated(SurfaceTexture surface)
    {
    }

    /// <summary>The render thread lets go of the window (swapchain and Vulkan surface), then the window is released.</summary>
    private void ReleaseWindow()
    {
        IntPtr window;
        Surface windowSurface;
        lock (_sync)
        {
            if (_thread is { IsAlive: true } && _window != IntPtr.Zero)
            {
                _releaseWindow = true;
                _windowReleased = false;
                Monitor.PulseAll(_sync);
                var until = DateTime.UtcNow.AddSeconds(2);
                while (!_windowReleased && _thread.IsAlive && DateTime.UtcNow < until)
                    Monitor.Wait(_sync, 100);
            }

            window = _window;
            windowSurface = _windowSurface;
            _window = IntPtr.Zero;
            _windowSurface = null;
        }

        if (window != IntPtr.Zero)
            Vk.ANativeWindow_release(window);
        windowSurface?.Release();
        windowSurface?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReleaseWindow();
            Thread thread;
            lock (_sync)
            {
                _exit = true;
                thread = _thread;
                Monitor.PulseAll(_sync);
            }

            thread?.Join(2000);
        }

        base.Dispose(disposing);
    }

    #endregion

    #region RENDER THREAD

    private void Run()
    {
        try
        {
            while (true)
            {
                IntPtr window;
                int width, height;
                bool windowChanged, sizeChanged, release, render;
                lock (_sync)
                {
                    while (!_exit && !_releaseWindow && !_windowChanged
                           && !(_window != IntPtr.Zero && _width > 0 && _height > 0 && (_renderRequested || _continuous || _recreateSwapchain)))
                        Monitor.Wait(_sync);

                    if (_exit)
                        break;

                    release = _releaseWindow;
                    windowChanged = _windowChanged;
                    _windowChanged = false;
                    window = _window;
                    width = _width;
                    height = _height;
                    sizeChanged = _sizeChanged;
                    _sizeChanged = false;
                    render = _renderRequested || _continuous;
                    _renderRequested = false;
                }

                if (release)
                {
                    DestroySurface();
                    lock (_sync)
                    {
                        _releaseWindow = false;
                        _windowReleased = true;
                        Monitor.PulseAll(_sync);
                    }

                    continue;
                }

                if (windowChanged)
                    DestroySurface();

                if (window == IntPtr.Zero || width <= 0 || height <= 0)
                    continue;

                if (_gpu == null)
                {
                    _gpu = VulkanGpu.Create();
                    if (_gpu == null || !CreateFrameSync())
                    {
                        Fail("no Vulkan device or Skia context");
                        return;
                    }
                }

                if (_surface == 0)
                {
                    if (!CreateSurface(window))
                    {
                        Fail("cannot create a Vulkan surface for the window");
                        return;
                    }

                    sizeChanged = true;
                }

                if (sizeChanged || _recreateSwapchain || _swapchain == 0)
                {
                    _recreateSwapchain = false;
                    if (!CreateSwapchain(width, height))
                    {
                        Fail("cannot create a swapchain");
                        return;
                    }

                    render = true;
                }

                if (render && !RenderFrame())
                    return;
            }
        }
        catch (Exception e)
        {
            Super.Log(e);
            Fail(e.Message);
        }
        finally
        {
            DestroyAll();
            lock (_sync)
            {
                _windowReleased = true;
                Monitor.PulseAll(_sync);
            }
        }
    }

    private void Fail(string why)
    {
        VulkanGpu.Disable(why);
        PostToMain(() => VulkanFailed?.Invoke(this, why));
    }

    private bool CreateFrameSync()
    {
        var device = _gpu.Device;
        var semaphoreInfo = new Vk.SemaphoreCreateInfo { sType = Vk.STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO };
        var fenceInfo = new Vk.FenceCreateInfo { sType = Vk.STRUCTURE_TYPE_FENCE_CREATE_INFO, flags = Vk.FENCE_CREATE_SIGNALED_BIT };
        var poolInfo = new Vk.CommandPoolCreateInfo
        {
            sType = Vk.STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags = Vk.COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT,
            queueFamilyIndex = _gpu.QueueFamily,
        };
        if (Vk.vkCreateCommandPool(device, &poolInfo, IntPtr.Zero, out _commandPool) != Vk.SUCCESS)
            return false;

        var allocate = new Vk.CommandBufferAllocateInfo
        {
            sType = Vk.STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            commandPool = _commandPool,
            commandBufferCount = 1,
        };
        for (var i = 0; i < FramesInFlight; i++)
        {
            if (Vk.vkCreateSemaphore(device, &semaphoreInfo, IntPtr.Zero, out _acquired[i]) != Vk.SUCCESS
                || Vk.vkCreateFence(device, &fenceInfo, IntPtr.Zero, out _fences[i]) != Vk.SUCCESS)
                return false;

            IntPtr before, after;
            if (Vk.vkAllocateCommandBuffers(device, &allocate, &before) != Vk.SUCCESS
                || Vk.vkAllocateCommandBuffers(device, &allocate, &after) != Vk.SUCCESS)
                return false;
            _before[i] = before;
            _after[i] = after;
        }

        return true;
    }

    private bool CreateSurface(IntPtr window)
    {
        var info = new Vk.AndroidSurfaceCreateInfoKHR
        {
            sType = Vk.STRUCTURE_TYPE_ANDROID_SURFACE_CREATE_INFO_KHR,
            window = window,
        };
        if (Vk.vkCreateAndroidSurfaceKHR(_gpu.Instance, &info, IntPtr.Zero, out _surface) != Vk.SUCCESS)
        {
            _surface = 0;
            return false;
        }

        Vk.vkGetPhysicalDeviceSurfaceSupportKHR(_gpu.PhysicalDevice, _gpu.QueueFamily, _surface, out var supported);
        return supported != 0;
    }

    private bool CreateSwapchain(int width, int height)
    {
        var device = _gpu.Device;
        Vk.vkDeviceWaitIdle(device);
        DestroySwapchainImages();

        if (Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(_gpu.PhysicalDevice, _surface, out var caps) != Vk.SUCCESS)
            return false;

        // the view's size, not the surface's current extent: right after a resize the window still reports the old
        // buffer size, and a swapchain of that size would be stretched over the new view. Android sizes the buffers to
        // the swapchain, any extent between the min and the max.
        width = (int)Math.Clamp((uint)width, Math.Max(1, caps.minWidth), Math.Max(1, caps.maxWidth));
        height = (int)Math.Clamp((uint)height, Math.Max(1, caps.minHeight), Math.Max(1, caps.maxHeight));

        // the format Skia draws: RGBA where the window offers it (Android always does), else BGRA
        uint formatCount = 0;
        Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(_gpu.PhysicalDevice, _surface, ref formatCount, null);
        var formats = stackalloc Vk.SurfaceFormatKHR[(int)Math.Max(1, formatCount)];
        Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(_gpu.PhysicalDevice, _surface, ref formatCount, formats);
        _format = 0;
        for (var i = 0; i < formatCount && _format == 0; i++)
            if (formats[i].format == Vk.FORMAT_R8G8B8A8_UNORM)
                _format = Vk.FORMAT_R8G8B8A8_UNORM;
        for (var i = 0; i < formatCount && _format == 0; i++)
            if (formats[i].format == Vk.FORMAT_B8G8R8A8_UNORM)
                _format = Vk.FORMAT_B8G8R8A8_UNORM;
        if (_format == 0)
            return false;
        _colorType = _format == Vk.FORMAT_R8G8B8A8_UNORM ? SKColorType.Rgba8888 : SKColorType.Bgra8888;

        // a TextureView composites with alpha: let the window's own blending apply, as the OpenGL canvas does
        var alpha = (caps.supportedCompositeAlpha & Vk.COMPOSITE_ALPHA_INHERIT_BIT_KHR) != 0 ? Vk.COMPOSITE_ALPHA_INHERIT_BIT_KHR
            : (caps.supportedCompositeAlpha & Vk.COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR) != 0 ? Vk.COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR
            : (caps.supportedCompositeAlpha & Vk.COMPOSITE_ALPHA_POST_MULTIPLIED_BIT_KHR) != 0 ? Vk.COMPOSITE_ALPHA_POST_MULTIPLIED_BIT_KHR
            : Vk.COMPOSITE_ALPHA_OPAQUE_BIT_KHR;

        var usage = caps.supportedUsageFlags & (Vk.IMAGE_USAGE_COLOR_ATTACHMENT_BIT | Vk.IMAGE_USAGE_TRANSFER_SRC_BIT
                                                | Vk.IMAGE_USAGE_TRANSFER_DST_BIT | Vk.IMAGE_USAGE_SAMPLED_BIT | Vk.IMAGE_USAGE_INPUT_ATTACHMENT_BIT);
        if ((usage & Vk.IMAGE_USAGE_COLOR_ATTACHMENT_BIT) == 0)
            return false;

        var count = Math.Max(caps.minImageCount, 3);
        if (caps.maxImageCount > 0)
            count = Math.Min(count, caps.maxImageCount);

        var old = _swapchain;
        var info = new Vk.SwapchainCreateInfoKHR
        {
            sType = Vk.STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
            surface = _surface,
            minImageCount = count,
            imageFormat = _format,
            imageColorSpace = Vk.COLOR_SPACE_SRGB_NONLINEAR_KHR,
            imageWidth = (uint)width,
            imageHeight = (uint)height,
            imageArrayLayers = 1,
            imageUsage = usage,
            imageSharingMode = Vk.SHARING_MODE_EXCLUSIVE,
            preTransform = caps.currentTransform,
            compositeAlpha = alpha,
            presentMode = Vk.PRESENT_MODE_FIFO_KHR,
            clipped = 1,
            oldSwapchain = old,
        };
        var result = Vk.vkCreateSwapchainKHR(device, &info, IntPtr.Zero, out var swapchain);
        if (old != 0)
            Vk.vkDestroySwapchainKHR(device, old, IntPtr.Zero);
        _swapchain = 0;
        if (result != Vk.SUCCESS)
            return false;
        _swapchain = swapchain;
        _swapWidth = width;
        _swapHeight = height;

        uint imageCount = 0;
        Vk.vkGetSwapchainImagesKHR(device, swapchain, ref imageCount, null);
        _images = new ulong[imageCount];
        fixed (ulong* images = _images)
            Vk.vkGetSwapchainImagesKHR(device, swapchain, ref imageCount, images);

        _surfaces = new SKSurface[imageCount];
        _targets = new GRBackendRenderTarget[imageCount];
        _args = new SKPaintGLSurfaceEventArgs[imageCount];
        _renderDone = new ulong[imageCount];
        var semaphoreInfo = new Vk.SemaphoreCreateInfo { sType = Vk.STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO };
        for (var i = 0; i < imageCount; i++)
        {
            // the layout the barrier before Skia's commands puts the image in, every frame
            var imageInfo = new GRVkImageInfo
            {
                Image = _images[i],
                ImageTiling = Vk.IMAGE_TILING_OPTIMAL,
                ImageLayout = Vk.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                Format = _format,
                ImageUsageFlags = usage,
                SampleCount = 1,
                LevelCount = 1,
                CurrentQueueFamily = _gpu.QueueFamily,
                SharingMode = Vk.SHARING_MODE_EXCLUSIVE,
            };
            _targets[i] = new GRBackendRenderTarget(width, height, imageInfo);
            _surfaces[i] = SKSurface.Create(_gpu.Context, _targets[i], GRSurfaceOrigin.TopLeft, _colorType);
            if (_surfaces[i] == null)
                return false;
            _args[i] = new SKPaintGLSurfaceEventArgs(_surfaces[i], _targets[i], GRSurfaceOrigin.TopLeft, _colorType);
            if (Vk.vkCreateSemaphore(device, &semaphoreInfo, IntPtr.Zero, out _renderDone[i]) != Vk.SUCCESS)
                return false;
        }

        if (Retained)
        {
            _retained?.Dispose();
            _retained = SKSurface.Create(_gpu.Context, false, new SKImageInfo(width, height, _colorType, SKAlphaType.Premul));
            _retainedArgs = _retained != null
                ? new SKPaintGLSurfaceEventArgs(_retained, _targets[0], GRSurfaceOrigin.TopLeft, _colorType)
                : null;
            _retained?.Canvas.Clear(SKColors.Transparent);
        }

        return true;
    }

    private bool RenderFrame()
    {
        var device = _gpu.Device;
        var slot = _frame % FramesInFlight;

        var fence = _fences[slot];
        if (Vk.vkWaitForFences(device, 1, &fence, 1, WaitNanos) != Vk.SUCCESS)
        {
            Fail("a frame did not finish");
            return false;
        }

        var result = Vk.vkAcquireNextImageKHR(device, _swapchain, WaitNanos, _acquired[slot], 0, out var index);
        if (result == Vk.ERROR_OUT_OF_DATE_KHR)
        {
            _recreateSwapchain = true;
            return true;
        }

        if (result != Vk.SUCCESS && result != Vk.SUBOPTIMAL_KHR)
        {
            if (result == Vk.TIMEOUT || result == Vk.NOT_READY)
            {
                RequestRender();
                return true;
            }

            Fail($"cannot acquire a swapchain image ({result})");
            return false;
        }

        Vk.vkResetFences(device, 1, &fence);
        var image = _images[index];

        // before Skia: wait for the image, then color-attachment layout (contents discarded: the frame is redrawn)
        Barrier(_before[slot], image, Vk.IMAGE_LAYOUT_UNDEFINED, Vk.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            Vk.PIPELINE_STAGE_ALL_COMMANDS_BIT, 0,
            Vk.PIPELINE_STAGE_ALL_COMMANDS_BIT, Vk.ACCESS_MEMORY_READ_BIT | Vk.ACCESS_MEMORY_WRITE_BIT);
        var acquired = _acquired[slot];
        var waitStage = Vk.PIPELINE_STAGE_ALL_COMMANDS_BIT;
        Submit(_before[slot], &acquired, &waitStage, null, 0);

        var surface = _surfaces[index];
        if (Retained && _retained != null)
        {
            Paint(_retained, _retainedArgs);
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawSurface(_retained, 0, 0);
        }
        else
        {
            surface.Canvas.Clear(SKColors.Transparent);
            Paint(surface, _args[index]);
        }

        _gpu.Context.Flush(true, false);

        // after Skia: present layout, then the present may start
        Barrier(_after[slot], image, Vk.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, Vk.IMAGE_LAYOUT_PRESENT_SRC_KHR,
            Vk.PIPELINE_STAGE_ALL_COMMANDS_BIT, Vk.ACCESS_MEMORY_WRITE_BIT,
            Vk.PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, 0);
        var renderDone = _renderDone[index];
        Submit(_after[slot], null, null, &renderDone, fence);

        var swapchain = _swapchain;
        var present = new Vk.PresentInfoKHR
        {
            sType = Vk.STRUCTURE_TYPE_PRESENT_INFO_KHR,
            waitSemaphoreCount = 1,
            pWaitSemaphores = &renderDone,
            swapchainCount = 1,
            pSwapchains = &swapchain,
            pImageIndices = &index,
        };
        result = Vk.vkQueuePresentKHR(_gpu.Queue, &present);
        _frame++;

        if (result == Vk.ERROR_OUT_OF_DATE_KHR || result == Vk.SUBOPTIMAL_KHR)
            _recreateSwapchain = true;
        else if (result == Vk.ERROR_DEVICE_LOST)
        {
            Fail("device lost");
            return false;
        }

        return true;
    }

    private void Paint(SKSurface surface, SKPaintGLSurfaceEventArgs args)
    {
        var canvas = surface.Canvas;
        var restore = canvas.Save();
        try
        {
            if (IgnorePixelScaling)
            {
                canvas.Scale(_density);
                var size = new SKSizeI((int)(args.Info.Width / _density), (int)(args.Info.Height / _density));
                args = new SKPaintGLSurfaceEventArgs(args.Surface, args.BackendRenderTarget, args.Origin, args.Info.WithSize(size), args.Info);
            }

            PaintSurface?.Invoke(this, args);
        }
        catch (Exception e)
        {
            Super.Log(e);
        }
        finally
        {
            canvas.RestoreToCount(restore);
        }
    }

    private void Barrier(IntPtr commandBuffer, ulong image, uint oldLayout, uint newLayout,
        uint srcStage, uint srcAccess, uint dstStage, uint dstAccess)
    {
        Vk.vkResetCommandBuffer(commandBuffer, 0);
        var begin = new Vk.CommandBufferBeginInfo
        {
            sType = Vk.STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
            flags = Vk.COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT,
        };
        Vk.vkBeginCommandBuffer(commandBuffer, &begin);
        var barrier = new Vk.ImageMemoryBarrier
        {
            sType = Vk.STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
            srcAccessMask = srcAccess,
            dstAccessMask = dstAccess,
            oldLayout = oldLayout,
            newLayout = newLayout,
            srcQueueFamilyIndex = Vk.QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vk.QUEUE_FAMILY_IGNORED,
            image = image,
            aspectMask = Vk.IMAGE_ASPECT_COLOR_BIT,
            levelCount = 1,
            layerCount = 1,
        };
        Vk.vkCmdPipelineBarrier(commandBuffer, srcStage, dstStage, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, 1, &barrier);
        Vk.vkEndCommandBuffer(commandBuffer);
    }

    private void Submit(IntPtr commandBuffer, ulong* wait, uint* waitStage, ulong* signal, ulong fence)
    {
        var submit = new Vk.SubmitInfo
        {
            sType = Vk.STRUCTURE_TYPE_SUBMIT_INFO,
            waitSemaphoreCount = wait != null ? 1u : 0u,
            pWaitSemaphores = wait,
            pWaitDstStageMask = waitStage,
            commandBufferCount = 1,
            pCommandBuffers = &commandBuffer,
            signalSemaphoreCount = signal != null ? 1u : 0u,
            pSignalSemaphores = signal,
        };
        Vk.vkQueueSubmit(_gpu.Queue, 1, &submit, fence);
    }

    private void DestroySwapchainImages()
    {
        foreach (var surface in _surfaces)
            surface?.Dispose();
        foreach (var target in _targets)
            target?.Dispose();
        _surfaces = Array.Empty<SKSurface>();
        _targets = Array.Empty<GRBackendRenderTarget>();
        _args = Array.Empty<SKPaintGLSurfaceEventArgs>();

        if (_gpu != null)
            foreach (var semaphore in _renderDone)
                if (semaphore != 0)
                    Vk.vkDestroySemaphore(_gpu.Device, semaphore, IntPtr.Zero);
        _renderDone = Array.Empty<ulong>();
        _images = Array.Empty<ulong>();
    }

    /// <summary>Swapchain and Vulkan surface go; the device and its Skia context stay for the next window.</summary>
    private void DestroySurface()
    {
        if (_gpu == null)
            return;

        Vk.vkDeviceWaitIdle(_gpu.Device);
        DestroySwapchainImages();
        _retained?.Dispose();
        _retained = null;
        _retainedArgs = null;
        if (_swapchain != 0)
        {
            Vk.vkDestroySwapchainKHR(_gpu.Device, _swapchain, IntPtr.Zero);
            _swapchain = 0;
        }

        if (_surface != 0)
        {
            Vk.vkDestroySurfaceKHR(_gpu.Instance, _surface, IntPtr.Zero);
            _surface = 0;
        }
    }

    private void DestroyAll()
    {
        if (_gpu == null)
            return;

        DestroySurface();
        var device = _gpu.Device;
        for (var i = 0; i < FramesInFlight; i++)
        {
            if (_acquired[i] != 0)
                Vk.vkDestroySemaphore(device, _acquired[i], IntPtr.Zero);
            if (_fences[i] != 0)
                Vk.vkDestroyFence(device, _fences[i], IntPtr.Zero);
            _acquired[i] = 0;
            _fences[i] = 0;
        }

        if (_commandPool != 0)
            Vk.vkDestroyCommandPool(device, _commandPool, IntPtr.Zero);
        _commandPool = 0;

        _gpu.Dispose();
        _gpu = null;
    }

    private static void PostToMain(Action action)
    {
        new Android.OS.Handler(Android.OS.Looper.MainLooper!).Post(action);
    }

    #endregion
}

/// <summary>What the canvas handler needs from the accelerated Android view, OpenGL or Vulkan.</summary>
public interface IGpuTextureView
{
    Rendermode RenderMode { get; set; }

    void RequestRender();

    GRContext GRContext { get; }

    bool IgnorePixelScaling { get; set; }

    event EventHandler<SKPaintGLSurfaceEventArgs> PaintSurface;
}
