using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using DrawnUi.Draw;
using DrawnUi.Draw.ApplicationModel;
using DrawnUi.Views;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
using OpenTkMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

namespace DrawnUi.OpenTk;

public class DrawnUiGameWindow : GameWindow
{
    private int _windowThreadId;
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly Canvas _canvas;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private GpuDrawable? _drawable;
    private long _lastRenderTicks;

    // WndProc subclass hook — kept alive to prevent GC collection
    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);
    private WndProcDelegate? _wndProcDelegate;
    private nint _oldWndProc;
    private nint _hwnd;

    // Constant: render every VSync frame (games).
    // Dynamic:  render only when dirty, sleep via GLFW between frames (apps).
    private UpdateModeType UpdateMode => _canvas.UpdateMode;

    public DrawnUiGameWindow(GameWindowSettings gameSettings, NativeWindowSettings nativeSettings, Canvas canvas)
        : base(gameSettings, HideUntilCentered(nativeSettings))
    {
        _canvas = canvas;
    }

    private static NativeWindowSettings HideUntilCentered(NativeWindowSettings s)
    {
        s.StartVisible = false;
        return s;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        Super.Init();

        _windowThreadId = Environment.CurrentManagedThreadId;
        MainThread.Configure(
            action => _mainThreadActions.Enqueue(action),
            () => Environment.CurrentManagedThreadId == _windowThreadId);

        GL.ClearColor(0.07f, 0.08f, 0.11f, 1f);

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);

        _drawable = new GpuDrawable();
        _canvas.ConnectDesktopDrawable(_drawable);

        Super.MaxFps = GetPrimaryMonitorRefreshRate();

        if (UpdateMode == UpdateModeType.Constant)
        {
            // Hardware VSync is the sole pacemaker — no software timer needed.
            VSync = VSyncMode.On;
        }
        else
        {
            // Event-driven: wake the GLFW loop from the DrawnUI software timer.
            VSync = VSyncMode.Off;
            Super.EnsureFrameLoopStarted();
            Super.OnFrame += OnSuperFrame;
        }

        RecreateSurface(ClientSize.X, ClientSize.Y);
        CenterOnScreen();
        IsVisible = true;

        if (OperatingSystem.IsWindows())
        {
            unsafe
            {
                _hwnd = GLFW.GetWin32Window(WindowPtr);
                if (_hwnd != IntPtr.Zero)
                {
                    WindowChrome.AddFullscreenMenuItem(_hwnd);
                    _wndProcDelegate = WndProcHook;
                    _oldWndProc = WindowChrome.SetWindowLongPtr(_hwnd, -4,
                        Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
                    ConfigureWindowChrome(_hwnd);
                }
            }
        }
    }

    /// <summary>
    /// Override to apply custom DWM title bar / border colors on Windows.
    /// Use <see cref="WindowChrome"/> helpers. Never called on non-Windows platforms.
    /// </summary>
    [SupportedOSPlatform("windows")]
    protected virtual void ConfigureWindowChrome(IntPtr hwnd) { }

    [SupportedOSPlatform("windows")]
    private nint WndProcHook(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WindowChrome.WM_SYSCOMMAND && wParam == WindowChrome.ID_TOGGLE_FULLSCREEN)
        {
            ToggleFullscreen();
            return 0;
        }
        return WindowChrome.CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
    }

    // Called from DrawnUI background timer thread — wake the GLFW event loop.
    private void OnSuperFrame(object? sender, EventArgs e) => GLFW.PostEmptyEvent();

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
        RecreateSurface(e.Width, e.Height);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        DrainMainThreadActions();

        if (_grContext == null || _surface == null || _drawable == null || ClientSize.X <= 0 || ClientSize.Y <= 0)
            return;

        if (UpdateMode == UpdateModeType.Constant)
        {
            // Render unconditionally — VSync already caps the rate.
            RenderDrawnUi();
        }
        else
        {
            var frameInterval = 1.0 / Super.MaxFps;

            if (_canvas.WasRendered && !_canvas.IsDirty)
            {
                GLFW.WaitEventsTimeout(frameInterval);
                return;
            }

            var now = Stopwatch.GetTimestamp();
            var elapsed = (now - _lastRenderTicks) / (double)Stopwatch.Frequency;
            if (elapsed < frameInterval)
            {
                GLFW.WaitEventsTimeout(frameInterval - elapsed);
                return;
            }
            _lastRenderTicks = Stopwatch.GetTimestamp();

            RenderDrawnUi();
        }
    }

    protected virtual void RenderDrawnUi()
    {
        var frameTime = GetFrameTimestampNanos();
        _drawable!.CanvasSize = new SKSize(ClientSize.X, ClientSize.Y);
        _drawable.SignalFrame(frameTime);

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        _canvas.WidthRequest = ClientSize.X;
        _canvas.HeightRequest = ClientSize.Y;
        _canvas.RenderExternalSurface(_surface!, new SKRect(0, 0, ClientSize.X, ClientSize.Y), frameTime);

        _surface!.Canvas.Flush();
        _grContext!.Flush();

        SwapBuffers();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != OpenTkMouseButton.Left || _surface == null) return;
        _canvas.HandleDesktopPointerDown(MousePosition.X, MousePosition.Y, ClientSize.X, ClientSize.Y);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        if (_surface == null) return;
        _canvas.HandleDesktopPointerMove(
            MousePosition.X, MousePosition.Y,
            MouseState.IsButtonDown(OpenTkMouseButton.Left),
            ClientSize.X, ClientSize.Y);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != OpenTkMouseButton.Left || _surface == null) return;
        _canvas.HandleDesktopPointerUp(MousePosition.X, MousePosition.Y, ClientSize.X, ClientSize.Y);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        var value = e.AsString;
        if (string.IsNullOrEmpty(value) || value == "\r" || value == "\n" || value == "\t") return;
        _canvas.HandleDesktopTextInput(value);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        var shift = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift);
        var ctrl = KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl);
        switch (e.Key)
        {
            case Keys.F11: ToggleFullscreen(); break;
            case Keys.Escape when WindowState == WindowState.Fullscreen:
                WindowState = WindowState.Normal; break;
            case Keys.Backspace: _canvas.DesktopEditorBackspace(); break;
            case Keys.Delete: _canvas.DesktopEditorDelete(); break;
            case Keys.Enter: _canvas.DesktopEditorEnter(); break;
            case Keys.Left: _canvas.DesktopEditorMoveCursor(-1, shift); break;
            case Keys.Right: _canvas.DesktopEditorMoveCursor(1, shift); break;
            case Keys.Home: _canvas.DesktopEditorMoveToStart(shift); break;
            case Keys.End: _canvas.DesktopEditorMoveToEnd(shift); break;
            case Keys.A when ctrl: _canvas.DesktopEditorSelectAll(); break;
            case Keys.Tab: _canvas.HandleDesktopTextInput("    "); break;
        }
    }

    public void ToggleFullscreen()
    {
        WindowState = WindowState == WindowState.Fullscreen
            ? WindowState.Normal
            : WindowState.Fullscreen;
    }

    protected override void OnUnload()
    {
        if (UpdateMode != UpdateModeType.Constant)
            Super.OnFrame -= OnSuperFrame;

        if (OperatingSystem.IsWindows() && _oldWndProc != 0)
            WindowChrome.SetWindowLongPtr(_hwnd, -4, _oldWndProc);

        MainThread.Reset();
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _canvas.Dispose();
        base.OnUnload();
    }

    private void RecreateSurface(int width, int height)
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

        var framebufferInfo = new GRGlFramebufferInfo((uint)framebuffer, SKColorType.Rgba8888.ToGlSizedFormat());
        _renderTarget = new GRBackendRenderTarget(width, height, samples, 8, framebufferInfo);
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);

        _drawable.Surface = _surface;
        _drawable.CanvasSize = new SKSize(width, height);
        _canvas.Repaint();
    }

    private void DrainMainThreadActions()
    {
        while (_mainThreadActions.TryDequeue(out var action))
            action();
    }

    private static long GetFrameTimestampNanos() => Super.GetCurrentTimeNanos();

    private unsafe void CenterOnScreen()
    {
        try
        {
            var monitor = GLFW.GetPrimaryMonitor();
            if (monitor == null) return;
            var mode = GLFW.GetVideoMode(monitor);
            if (mode == null) return;
            Location = new Vector2i(
                (mode->Width  - ClientSize.X) / 2,
                (mode->Height - ClientSize.Y) / 2);
        }
        catch { }
    }

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
