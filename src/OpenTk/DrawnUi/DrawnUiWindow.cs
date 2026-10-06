using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using DrawnUi.Draw;
using DrawnUi.Views;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
using OpenTkMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

namespace DrawnUi.OpenTk;

public class DrawnUiWindow : GameWindow
{
    private int _windowThreadId;
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly Canvas _canvas;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private GpuDrawable? _drawable;
    private FramePacing? _pacing;

    private bool _firstFrameDone;

    // WndProc subclass hook — kept alive to prevent GC collection
    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);
    private WndProcDelegate? _wndProcDelegate;
    private nint _oldWndProc;
    private nint _hwnd;
    private WindowsUiaProvider? _uiaProvider;
    private LinuxAtSpiProvider? _atSpiProvider;

    // Constant: render every VSync frame (games).
    // Dynamic:  render only when dirty, sleep via GLFW between frames (apps).
    private UpdateModeType UpdateMode => _canvas.UpdateMode;

    public DrawnUiWindow(GameWindowSettings gameSettings, NativeWindowSettings nativeSettings, Canvas canvas)
        : base(gameSettings, HideUntilCentered(nativeSettings))
    {
        _canvas = canvas;
        _gestures = new DesktopGestureHandler(canvas);
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

        // Copy of a selected SkiaLabel text goes to the system clipboard (GLFW, window thread).
        Super.SetClipboardText ??= text => MainThread.BeginInvokeOnMainThread(() => ClipboardString = text);

        _windowThreadId = Environment.CurrentManagedThreadId;
        MainThread.Configure(
            action => _mainThreadActions.Enqueue(action),
            () => Environment.CurrentManagedThreadId == _windowThreadId);

        GL.ClearColor(0.07f, 0.08f, 0.11f, 1f);

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);

        _drawable = new GpuDrawable();
        _canvas.ConnectDesktopDrawable(_drawable);

        // GLFW gives whole hertz, 59 for a 59.95 Hz panel: frames paced 16.95 ms apart would fall behind the display,
        // so on Windows the compositor's exact rate is used
        double refreshRate = GetPrimaryMonitorRefreshRate();
        if (OperatingSystem.IsWindows() && WindowChrome.TryGetRefreshRate(out var exact))
            refreshRate = exact;
        Super.MaxFps = (int)Math.Round(refreshRate);

        if (UpdateMode == UpdateModeType.Constant)
        {
            // VSync paces the frames; where the driver ignores it (WSLg's software GL) the window paces them itself.
            VSync = VSyncMode.On;
            _pacing = new FramePacing(refreshRate, alwaysOn: false);
        }
        else
        {
            // Event-driven: wake the GLFW loop from the DrawnUI software timer, frames one refresh apart.
            VSync = VSyncMode.Off;
            _pacing = new FramePacing(refreshRate, alwaysOn: true);
            Super.EnsureFrameLoopStarted();
            Super.OnFrame += OnSuperFrame;
        }

        RecreateSurface(ClientSize.X, ClientSize.Y);
        PositionWindow();

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
                    _uiaProvider = new WindowsUiaProvider(
                        _hwnd, _canvas.AccessibilityManager, () => (float)_canvas.RenderingScale);
                    _canvas.AccessibilityManager.FocusChanged += node => _uiaProvider.NotifyFocusChanged(node);
                }
            }
        }

        if (OperatingSystem.IsLinux())
        {
            // Orca: the snapshot on the AT-SPI bus, once assistive technology turns it on
            _atSpiProvider = new LinuxAtSpiProvider(_canvas.AccessibilityManager, () => (float)_canvas.RenderingScale, () => Title);
            UpdateAtSpiWindow();
            _atSpiProvider.SetActive(IsFocused);
            _atSpiProvider.Start();
        }
    }

    // the client area on screen, for the extents a screen reader asks for
    private void UpdateAtSpiWindow()
    {
        if (OperatingSystem.IsLinux())
            _atSpiProvider?.UpdateWindow(ClientLocation.X, ClientLocation.Y, ClientSize.X, ClientSize.Y);
    }

    protected override void OnMove(WindowPositionEventArgs e)
    {
        base.OnMove(e);
        UpdateAtSpiWindow();
    }

    protected override void OnFocusedChanged(FocusedChangedEventArgs e)
    {
        base.OnFocusedChanged(e);
        if (OperatingSystem.IsLinux())
            _atSpiProvider?.SetActive(e.IsFocused);
    }

    /// <summary>
    /// Called at startup, by default will call CenterOnScreen()
    /// </summary>
    public virtual void PositionWindow()
    {
        CenterOnScreen();
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

        if (_uiaProvider != null)
        {
            var uiaResult = _uiaProvider.HandleMessage(msg, wParam, lParam);
            if (uiaResult != 0) return uiaResult;
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
        UpdateAtSpiWindow();

        // Linux (X11/Wayland) has no modal size loop: the render loop keeps running during a resize
        // and picks up the Repaint from RecreateSurface, so an extra (vsync-blocking) frame here would only add lag.
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            RenderDuringModalLoop();
    }

    protected override void OnRefresh()
    {
        base.OnRefresh();
        RenderDuringModalLoop();
    }

    /// <summary>
    /// Refresh = the OS asks for the window content (expose, uncover, restore). In <see cref="UpdateModeType.Dynamic"/>
    /// a clean canvas would not redraw on its own, and an X11 window without a compositor would stay damaged.
    /// While the user drags a window edge (Windows, macOS) the OS runs its own modal loop and
    /// <see cref="OnRenderFrame"/> does not run until the mouse is released; GLFW still delivers
    /// resize and refresh callbacks from inside that loop, so draw the frame right there.
    /// </summary>
    private void RenderDuringModalLoop()
    {
        if (!_firstFrameDone || _grContext == null || _surface == null || _drawable == null || ClientSize.X <= 0 || ClientSize.Y <= 0)
            return;

        RenderDrawnUi();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        DrainMainThreadActions();

        if (_grContext == null || _surface == null || _drawable == null || ClientSize.X <= 0 || ClientSize.Y <= 0)
            return;

        if (UpdateMode != UpdateModeType.Constant && _canvas.WasRendered && !_canvas.IsDirty)
        {
            // nothing to draw: sleep until an event or the DrawnUI timer wakes the loop
            GLFW.WaitEventsTimeout(1.0 / Super.MaxFps);
            return;
        }

        // Constant: every frame, VSync keeps the pace unless the driver ignores it. Dynamic: frames one refresh
        // apart. Either way a paced frame waits for its slot on the refresh grid.
        if (_pacing?.Hold(Stopwatch.GetTimestamp()) is { } due)
        {
            GLFW.WaitEventsTimeout(Math.Max(0, due - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency);
            return;
        }

        RenderDrawnUi();
    }

    protected virtual void RenderDrawnUi()
    {
        // a paced frame: animations step with its slot on the refresh grid, not with the moment the wake-up came
        var slot = _pacing?.Frame(Stopwatch.GetTimestamp());
        var frameTime = slot is { } ticks ? (long)(1_000_000_000.0 * ticks / Stopwatch.Frequency) : GetFrameTimestampNanos();
        _drawable!.CanvasSize = new SKSize(ClientSize.X, ClientSize.Y);
        _drawable.SignalFrame(frameTime);

        // Restore GL state Skia left dirty from the previous frame's overlay rendering.
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Disable(EnableCap.StencilTest);
        GL.DepthMask(true);
        GL.ColorMask(true, true, true, true);

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        RenderScene();
        _grContext?.ResetContext();

        _canvas.WidthRequest = ClientSize.X;
        _canvas.HeightRequest = ClientSize.Y;
        _canvas.RenderExternalSurface(_surface!, new SKRect(0, 0, ClientSize.X, ClientSize.Y), frameTime);

        _surface!.Canvas.Flush();
        _grContext!.Flush();

        SwapBuffers();

        if (!_firstFrameDone)
        {
            _firstFrameDone = true;
            IsVisible = true;
        }
    }

    /// <summary>
    /// Override to render a 3D/GL scene before the DrawnUI overlay.
    /// Called after GL.Clear and before DrawnUI renders. GRContext is reset automatically after this returns.
    /// Call GL.Finish() at the end of your implementation.
    /// </summary>
    protected virtual void RenderScene() { }

    // Every mouse button reaches the canvas with its PointerData; see DesktopGestureHandler.
    private readonly DesktopGestureHandler _gestures;

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (_surface == null) return;
        _gestures.OnMouseDown(e, MousePosition, ClientSize, MouseState);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        if (_surface == null) return;
        _gestures.OnMouseMove(e, MousePosition, MouseState.IsAnyButtonDown, ClientSize, MouseState);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_surface == null) return;
        _gestures.OnMouseUp(e, MousePosition, ClientSize, MouseState);
    }

    protected override void OnMouseLeave()
    {
        base.OnMouseLeave();
        if (_surface == null) return;
        _gestures.OnMouseLeave();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_surface == null) return;
        _gestures.OnMouseWheel(e, MousePosition, ClientSize);
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
        var alt = KeyboardState.IsKeyDown(Keys.LeftAlt) || KeyboardState.IsKeyDown(Keys.RightAlt);
        switch (e.Key)
        {
            case Keys.F11: ToggleFullscreen(); return;
            case Keys.Escape when WindowState == WindowState.Fullscreen:
                WindowState = WindowState.Normal; return;
            case Keys.Menu:
            case Keys.F10 when shift:
                _gestures.OnContextMenuKey(ClientSize); return;
        }

        // Keyboard navigation, the rules of the other desktop heads (DrawnView.HandleKeyboardNavigation): Tab / Shift+Tab
        // walk the Tab stops, also out of a drawn editor (no tab characters). Enter / Space / Escape / arrows / Home / End /
        // PageUp / PageDown go to the node in keyboard focus while the keyboard is in use, so a game's keys stay its own.
        if (e.Key == Keys.Tab)
        {
            if (_canvas.FocusedChild is SkiaEditor editor)
                _canvas.AccessibilityManager.NotifyFocused(editor); // continue from the field, also when a click focused it
            _canvas.HandleKeyboardNavigation(InputKey.Tab, shift);
            return;
        }

        if (_canvas.FocusedChild is not SkiaEditor && _canvas.KeyboardFocusNode != null
            && OpenTkKeyMapper.Map(e.Key) is { } key && _canvas.HandleKeyboardNavigation(key, shift))
            return;

        switch (e.Key)
        {
            case Keys.Backspace: _canvas.DesktopEditorBackspace(); break;
            case Keys.Delete: _canvas.DesktopEditorDelete(); break;
            case Keys.Enter: _canvas.DesktopEditorEnter(alt, shift); break;
            case Keys.Left: _canvas.DesktopEditorMoveCursor(-1, shift); break;
            case Keys.Right: _canvas.DesktopEditorMoveCursor(1, shift); break;
            case Keys.Home: _canvas.DesktopEditorMoveToStart(shift); break;
            case Keys.End: _canvas.DesktopEditorMoveToEnd(shift); break;
            // this window feeds no KeyboardManager, so a selectable label gets its copy keys here
            case Keys.A when ctrl && _canvas.FocusedChild is SkiaLabel { AccessibilityTextSelectable: true } label:
                label.SelectAll(); break;
            case Keys.C when ctrl && _canvas.FocusedChild is SkiaLabel { AccessibilityTextSelectable: true } label:
                label.CopySelection(); break;
            case Keys.A when ctrl: _canvas.DesktopEditorSelectAll(); break;
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
        {
            WindowChrome.SetWindowLongPtr(_hwnd, -4, _oldWndProc);
            _uiaProvider?.Dispose();
            _uiaProvider = null;
        }

        if (OperatingSystem.IsLinux())
        {
            _atSpiProvider?.Dispose();
            _atSpiProvider = null;
        }

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
