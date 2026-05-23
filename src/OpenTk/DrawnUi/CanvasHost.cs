using System.Collections.Concurrent;
using DrawnUi.Draw;
using DrawnUi.Draw.ApplicationModel;
using DrawnUi.Views;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;

namespace DrawnUi.OpenTk;

/// <summary>
/// Hosts a DrawnUI Canvas inside an existing GameWindow without requiring DrawnUiGameWindow as base class.
/// Call Initialize() from OnLoad, Resize() from OnResize, Render() from OnRenderFrame.
/// Route input through the Gestures and Input sub-objects.
/// </summary>
public class CanvasHost : IDisposable
{
    private readonly Canvas _canvas;
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private int _windowThreadId;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private GpuDrawable? _drawable;
    private Action? _wakeLoop;

    public DesktopGestureHandler Gestures { get; }
    public DesktopInputHandler Input { get; }

    public CanvasHost(Canvas canvas)
    {
        _canvas = canvas;
        Gestures = new DesktopGestureHandler(canvas);
        Input = new DesktopInputHandler(canvas);
    }

    /// <summary>
    /// Call from GameWindow.OnLoad. GL context must be current.
    /// Pass wakeLoop = GLFW.PostEmptyEvent when using WaitEvents render mode.
    /// </summary>
    public void Initialize(Action? wakeLoop = null)
    {
        _wakeLoop = wakeLoop;
        _windowThreadId = Environment.CurrentManagedThreadId;

        MainThread.Configure(
            action => _mainThreadActions.Enqueue(action),
            () => Environment.CurrentManagedThreadId == _windowThreadId);

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);

        _drawable = new GpuDrawable();
        _canvas.ConnectDesktopDrawable(_drawable);

        Super.MaxFps = GetPrimaryMonitorRefreshRate();
        Super.EnsureFrameLoopStarted();
        Super.OnFrame += OnSuperFrame;
    }

    private void OnSuperFrame(object? sender, EventArgs e) => _wakeLoop?.Invoke();

    /// <summary>
    /// Call from GameWindow.OnResize after GL.Viewport.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (_grContext == null || _drawable == null || width <= 0 || height <= 0)
            return;

        _surface?.Dispose();
        _surface = null;
        _renderTarget?.Dispose();
        _renderTarget = null;

        GL.GetInteger(GetPName.FramebufferBinding, out var framebuffer);
        GL.GetInteger(GetPName.Samples, out var samples);

        var maxSamples = _grContext.GetMaxSurfaceSampleCount(SKColorType.Rgba8888);
        if (samples > maxSamples) samples = maxSamples;

        var fbInfo = new GRGlFramebufferInfo((uint)framebuffer, SKColorType.Rgba8888.ToGlSizedFormat());
        _renderTarget = new GRBackendRenderTarget(width, height, samples, 8, fbInfo);
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);

        _drawable.Surface = _surface;
        _drawable.CanvasSize = new SKSize(width, height);
        _canvas.Repaint();
    }

    /// <summary>
    /// Call after raw OpenGL draw calls and before Render() so Skia knows GL state was modified externally.
    /// Required when mixing native GL rendering with the DrawnUI overlay on the same framebuffer.
    /// </summary>
    public void ResetGrContext() => _grContext?.ResetContext();

    /// <summary>
    /// Call from GameWindow.OnRenderFrame after your own GL draw and before SwapBuffers.
    /// Always renders — dirty check is skipped because the OpenTK game loop is the pace signal.
    /// For idle/event-driven apps using WaitEvents, this is only called when something woke the loop anyway.
    /// </summary>
    public bool Render()
    {
        DrainMainThreadActions();

        if (_grContext == null || _surface == null || _drawable == null)
            return false;

        var frameTime = GetFrameTimestampNanos();
        _drawable.SignalFrame(frameTime);

        _canvas.WidthRequest = _drawable.CanvasSize.Width;
        _canvas.HeightRequest = _drawable.CanvasSize.Height;
        _canvas.RenderExternalSurface(_surface, new SKRect(0, 0, _drawable.CanvasSize.Width, _drawable.CanvasSize.Height), frameTime);

        _surface.Canvas.Flush();
        _grContext.Flush();

        return true;
    }

    public void Dispose()
    {
        Super.OnFrame -= OnSuperFrame;
        MainThread.Reset();
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _canvas.Dispose();
    }

    private void DrainMainThreadActions()
    {
        while (_mainThreadActions.TryDequeue(out var action))
            action();
    }

    private static long GetFrameTimestampNanos() => Super.GetCurrentTimeNanos();

    private static unsafe int GetPrimaryMonitorRefreshRate()
    {
        try
        {
            var monitor = GLFW.GetPrimaryMonitor();
            if (monitor != null)
            {
                var mode = GLFW.GetVideoMode(monitor);
                if (mode != null && mode->RefreshRate > 0)
                    return mode->RefreshRate;
            }
        }
        catch { }
        return 60;
    }
}
