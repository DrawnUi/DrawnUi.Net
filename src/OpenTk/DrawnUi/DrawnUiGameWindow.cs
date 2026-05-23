using System.Collections.Concurrent;
using System.Diagnostics;
using DrawnUi.Draw;
using DrawnUi.Draw.ApplicationModel;
using DrawnUi.Views;
using OpenTK.Graphics.OpenGL4;
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

    public DrawnUiGameWindow(GameWindowSettings gameSettings, NativeWindowSettings nativeSettings, Canvas canvas)
        : base(gameSettings, nativeSettings)
    {
        _canvas = canvas;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        _windowThreadId = Environment.CurrentManagedThreadId;
        MainThread.Configure(
            action => _mainThreadActions.Enqueue(action),
            () => Environment.CurrentManagedThreadId == _windowThreadId);

        GL.ClearColor(0.07f, 0.08f, 0.11f, 1f);
        VSync = VSyncMode.On;

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);

        _drawable = new GpuDrawable();
        _canvas.ConnectDesktopDrawable(_drawable);

        // Start DrawnUI animation ticker (drives cursor blink, animated controls, etc.)
        // When it fires, wake the idle event loop so we render the next animation frame.
        Super.EnsureFrameLoopStarted();
        Super.OnFrame += OnSuperFrame;

        RecreateSurface(ClientSize.X, ClientSize.Y);
    }

    // Called from DrawnUI background timer thread — wake the GLFW event loop.
    private void OnSuperFrame(object? sender, EventArgs e)
    {
        GLFW.PostEmptyEvent();
    }

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

        // Only render when canvas has pending updates.
        // Skip GL.Clear and SwapBuffers so front buffer keeps showing last frame.
        if (_canvas.WasRendered && !_canvas.IsDirty)
            return;

        var frameTime = GetFrameTimestampNanos();
        _drawable.CanvasSize = new SKSize(ClientSize.X, ClientSize.Y);
        _drawable.SignalFrame(frameTime);

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        _canvas.WidthRequest = ClientSize.X;
        _canvas.HeightRequest = ClientSize.Y;
        _canvas.RenderExternalSurface(_surface, new SKRect(0, 0, ClientSize.X, ClientSize.Y), frameTime);

        _surface.Canvas.Flush();
        _grContext.Flush();

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

    protected override void OnUnload()
    {
        Super.OnFrame -= OnSuperFrame;
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

    private static long GetFrameTimestampNanos()
    {
        var timestamp = Stopwatch.GetTimestamp();
        return (long)(1_000_000_000.0 * timestamp / Stopwatch.Frequency);
    }
}
