using Android.Content;
using Android.Opengl;
using DrawnUi;
using DrawnUi.Vulkan;
using Microsoft.Maui;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using SkiaSharp.Views.Android;
using SkiaSharp.Views.Maui.Platform;
using SKPaintGLSurfaceEventArgs = SkiaSharp.Views.Android.SKPaintGLSurfaceEventArgs;

namespace DrawnUi.Views;

/// <summary>
/// The accelerated canvas on Android: Vulkan where the device supports it (<see cref="SkiaVulkanTextureView"/>),
/// otherwise, or when Vulkan fails on it, OpenGL ES (<see cref="SkiaGLTexture"/>).
/// </summary>
public partial class SKGLViewHandlerRetained : ViewHandler<ISKGLView, Android.Views.View>
{

    private SKSizeI lastCanvasSize;
    private GRContext? lastGRContext;
    // one per surface drawn on: the OpenGL canvas has one, the Vulkan canvas one per swapchain image
    private readonly SkiaSharp.Views.Maui.SKPaintGLSurfaceEventArgs[] _cachedVirtualViewArgs = new SkiaSharp.Views.Maui.SKPaintGLSurfaceEventArgs[4];
    private int _nextCachedArgs;

    protected override Android.Views.View CreatePlatformView()
    {
        var superview = (VirtualView as SkiaViewAccelerated)?.Superview;
        if (DrawnExtensions.StartupSettings?.UseVulkan != false && VulkanGpu.IsAvailable)
        {
            var vulkan = new SkiaVulkanTextureView(Context)
            {
                Retained = superview?.RenderingMode == RenderingModeType.AcceleratedRetained
            };
            vulkan.SetOpaque(false);
            return vulkan;
        }

        var view = new MauiSKGLTextureView(Context);
        view.SetOpaque(false);
        return view;
    }

    protected override void ConnectHandler(Android.Views.View platformView)
    {
        if (platformView is IGpuTextureView gpu)
            gpu.PaintSurface += OnPaintSurface;
        if (platformView is SkiaVulkanTextureView vulkan)
            vulkan.VulkanFailed += OnVulkanFailed;

        base.ConnectHandler(platformView);
    }

    protected override void DisconnectHandler(Android.Views.View platformView)
    {
        if (platformView is IGpuTextureView gpu)
            gpu.PaintSurface -= OnPaintSurface;
        if (platformView is SkiaVulkanTextureView vulkan)
            vulkan.VulkanFailed -= OnVulkanFailed;

        base.DisconnectHandler(platformView);

        platformView?.Dispose();  //MAUI is not disposing PlatformView in base, avoid the leak
    }

    /// <summary>Vulkan failed on this device (now disabled for the process): the canvas is recreated, with OpenGL.</summary>
    private void OnVulkanFailed(object? sender, string why)
    {
        (VirtualView as SkiaViewAccelerated)?.Superview?.RecreateCanvasView();
    }

    // Mapper actions / properties

    public static void OnInvalidateSurface(SKGLViewHandlerRetained handler, ISKGLView view, object? args)
    {
        if (handler?.PlatformView is not IGpuTextureView pv)
            return;

        if (pv.RenderMode == Rendermode.WhenDirty)
            pv.RequestRender();
    }

    public static void MapIgnorePixelScaling(SKGLViewHandlerRetained handler, ISKGLView view)
    {
        if (handler?.PlatformView is not IGpuTextureView pv)
            return;

        pv.IgnorePixelScaling = view.IgnorePixelScaling;
        pv.RequestRender();
    }

    public static void MapHasRenderLoop(SKGLViewHandlerRetained handler, ISKGLView view)
    {
        if (handler?.PlatformView is not IGpuTextureView pv)
            return;

        pv.RenderMode = view.HasRenderLoop
            ? Rendermode.Continuously
            : Rendermode.WhenDirty;
    }

    public static void MapEnableTouchEvents(SKGLViewHandlerRetained handler, ISKGLView view)
    {
        //handler.touchHandler ??= new SKTouchHandler(
        //    args => view.OnTouch(args),
        //    (x, y) => handler.OnGetScaledCoord(x, y));

        //handler.touchHandler?.SetEnabled(handler.PlatformView, view.EnableTouchEvents);
    }

    // helper methods

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        var newCanvasSize = e.Info.Size;
        if (lastCanvasSize != newCanvasSize)
        {
            lastCanvasSize = newCanvasSize;
            Array.Clear(_cachedVirtualViewArgs);
            VirtualView?.OnCanvasSizeChanged(newCanvasSize);
        }

        if (sender is IGpuTextureView platformView)
        {
            var newGRContext = platformView.GRContext;
            if (lastGRContext != newGRContext)
            {
                lastGRContext = newGRContext;
                Array.Clear(_cachedVirtualViewArgs);
                VirtualView?.OnGRContextChanged(newGRContext);
            }
        }

        SkiaSharp.Views.Maui.SKPaintGLSurfaceEventArgs args = null;
        foreach (var cached in _cachedVirtualViewArgs)
        {
            if (cached != null && cached.Surface == e.Surface && cached.Info == e.Info)
            {
                args = cached;
                break;
            }
        }

        if (args == null)
        {
            args = new SkiaSharp.Views.Maui.SKPaintGLSurfaceEventArgs(e.Surface, e.BackendRenderTarget, e.Origin, e.Info, e.RawInfo);
            _cachedVirtualViewArgs[_nextCachedArgs++ % _cachedVirtualViewArgs.Length] = args;
        }

        VirtualView?.OnPaintSurface(args);
    }

    private SKPoint OnGetScaledCoord(double x, double y)
    {
        if (VirtualView?.IgnorePixelScaling == true && Context != null)
        {
            x = Context.FromPixels(x);
            y = Context.FromPixels(y);
        }

        return new SKPoint((float)x, (float)y);
    }

    private class MauiSKGLTextureView : SkiaGLTexture, IGpuTextureView
    {
        private float density;

        public MauiSKGLTextureView(Context context)
            : base(context)
        {
            density = Resources?.DisplayMetrics?.Density ?? 1;
        }

        public bool IgnorePixelScaling { get; set; }

        protected override void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
        {
            if (IgnorePixelScaling)
            {
                var userVisibleSize = new SKSizeI((int)(e.Info.Width / density), (int)(e.Info.Height / density));
                var canvas = e.Surface.Canvas;
                canvas.Scale(density);
                canvas.Save();

                e = new SKPaintGLSurfaceEventArgs(e.Surface, e.BackendRenderTarget, e.Origin,
                    e.Info.WithSize(userVisibleSize), e.Info);
            }

            base.OnPaintSurface(e);
        }
    }
}
