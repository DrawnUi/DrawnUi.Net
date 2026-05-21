using System.Diagnostics;
using System.Drawing;
using System.Collections.Concurrent;
using AppoMobi.Gestures;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Draw.ApplicationModel;
using DrawnUi.Infrastructure.Enums;
using DrawnUi.Views;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SkiaSharp;
using Color = DrawnUi.Color;
using OpenTkMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

Super.Init();

var gameSettings = new GameWindowSettings
{
    UpdateFrequency = 60
};

var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(1280, 800),
    Title = "DrawnUi.Net OpenTK GPU Host",
    API = ContextAPI.OpenGL,
    APIVersion = new Version(3, 3),
    Profile = ContextProfile.Core
};

using var window = new DrawnUiGpuWindow(gameSettings, nativeSettings);
window.Run();

internal sealed class DrawnUiGpuWindow : GameWindow
{
    private int _windowThreadId;
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly Canvas _canvas;
    private readonly GpuDrawable _drawable;
    private readonly SkiaButton _button;
    private readonly SkiaEditor _editor;
    private readonly SkiaLabel _status;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private TouchActionEventArgs? _pointerDownArgs;
    private TouchActionEventArgs? _previousTouchArgs;
    private const long PointerId = 1;

    public DrawnUiGpuWindow(GameWindowSettings gameSettings, NativeWindowSettings nativeSettings)
        : base(gameSettings, nativeSettings)
    {
        _status = new SkiaLabel
        {
            Text = "Initializing GPU surface...",
            FontSize = 18,
            TextColor = Color.FromArgb("#D6E4F0"),
            Margin = new Thickness(24, 0, 24, 12)
        };

        _editor = new SkiaEditor
        {
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 180,
            BackgroundColor = Color.FromArgb("#F7F9FC"),
            TextColor = Colors.Black,
            CursorColor = Color.FromArgb("#2E86DE"),
            SelectionColor = Color.FromArgb("#55358CFF"),
            PlaceholderText = "Click here and type. Drag to select. Backspace/Delete/Enter/Home/End/Arrows work.",
            PlaceholderColor = Color.FromArgb("#728197"),
            FontSize = 22,
            MaxLines = 6,
            Margin = new Thickness(24, 0, 24, 18),
            Padding = new Thickness(16, 12)
        };

        _button = new SkiaButton("Tap Me")
        {
            HorizontalOptions = LayoutOptions.Start,
            HeightRequest = 46,
            Margin = new Thickness(24, 0, 24, 0),
            BackgroundColor = Color.FromArgb("#2E86DE"),
            TextColor = Colors.White,
            Padding = new Thickness(18, 10)
        };

        _button.Tapped += (_, _) => _status.Text = "Button tapped via DrawnUI gesture routing.";
        _editor.FocusChanged += (_, focused) =>
            _status.Text = focused
                ? "Editor focused. Type into the window."
                : "Editor focus cleared.";
        _editor.TextChanged += (_, text) =>
            _status.Text = $"Editor text length: {text?.Length ?? 0}";

        _canvas = CreateScene(ClientSize.X, ClientSize.Y, _status, _editor, _button);
        _drawable = new GpuDrawable();

        _canvas.AttachCanvasView(_drawable);
        _canvas.ConnectedHandler();
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        _windowThreadId = Environment.CurrentManagedThreadId;
        MainThread.Configure(action => _mainThreadActions.Enqueue(action), () => Environment.CurrentManagedThreadId == _windowThreadId);

        GL.ClearColor(0.07f, 0.08f, 0.11f, 1f);
        VSync = VSyncMode.On;

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);
        RecreateSurface(ClientSize.X, ClientSize.Y);
        UpdateStatus();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);
        RecreateSurface(e.Width, e.Height);
        UpdateStatus();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        DrainMainThreadActions();

        if (_grContext == null || _surface == null || ClientSize.X <= 0 || ClientSize.Y <= 0)
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

        if (e.Button != OpenTkMouseButton.Left || _surface == null)
            return;

        var args = CreateTouchArgs(TouchActionType.Pressed, GetPointerLocation());
        args.IsInContact = true;
        args.Distance = new TouchActionEventArgs.DistanceInfo();
        _pointerDownArgs = args;
        _previousTouchArgs = args;

        _canvas.OnGestureEvent(TouchActionType.Pressed, args, TouchActionResult.Down);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);

        if (_surface == null)
            return;

        var actionType = IsPointerButtonDown(OpenTkMouseButton.Left) ? TouchActionType.Moved : TouchActionType.Pointer;
        var args = CreateTouchArgs(actionType, GetPointerLocation());

        if (_previousTouchArgs != null)
        {
            TouchActionEventArgs.FillDistanceInfo(args, _previousTouchArgs);
        }

        if (actionType == TouchActionType.Pointer)
        {
            _canvas.OnGestureEvent(actionType, args, TouchActionResult.Pointer);
            return;
        }

        if (args.Distance.Delta.X != 0 || args.Distance.Delta.Y != 0)
        {
            _canvas.OnGestureEvent(actionType, args, TouchActionResult.Panning);
        }

        _previousTouchArgs = args;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != OpenTkMouseButton.Left || _pointerDownArgs == null || _surface == null)
            return;

        var args = CreateTouchArgs(TouchActionType.Released, GetPointerLocation());
        args.IsInContact = false;

        if (_previousTouchArgs != null)
        {
            TouchActionEventArgs.FillDistanceInfo(args, _previousTouchArgs);
        }

        var threshold = TouchEffect.TappedCancelMoveThresholdPoints * Math.Max(0.1f, TouchEffect.Density);
        if (Math.Abs(args.Distance.Total.X) < threshold && Math.Abs(args.Distance.Total.Y) < threshold)
        {
            _canvas.OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Tapped);
        }

        _canvas.OnGestureEvent(TouchActionType.Released, args, TouchActionResult.Up);
        _pointerDownArgs = null;
        _previousTouchArgs = null;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (_canvas.FocusedChild is not SkiaEditor editor)
            return;

        var value = e.AsString;
        if (string.IsNullOrEmpty(value) || value == "\r" || value == "\n" || value == "\t")
            return;

        editor.StubTypeText(value);
        _canvas.Repaint();
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_canvas.FocusedChild is not SkiaEditor editor)
            return;

        var shift = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift);
        var control = KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl);

        switch (e.Key)
        {
            case Keys.Backspace:
                editor.StubBackspace();
                break;
            case Keys.Delete:
                editor.StubDelete();
                break;
            case Keys.Enter:
                editor.StubPressEnter();
                break;
            case Keys.Left:
                editor.StubMoveCursor(-1, shift);
                break;
            case Keys.Right:
                editor.StubMoveCursor(1, shift);
                break;
            case Keys.Home:
                editor.StubMoveCursor(-editor.CursorPosition, shift);
                break;
            case Keys.End:
                editor.StubMoveCursor((editor.Text?.Length ?? 0) - editor.CursorPosition, shift);
                break;
            case Keys.A when control:
                editor.StubSelectAll();
                break;
            case Keys.Tab:
                editor.StubTypeText("    ");
                break;
            default:
                return;
        }

        _canvas.Repaint();
    }

    protected override void OnUnload()
    {
        MainThread.Reset();
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _canvas.Dispose();

        base.OnUnload();
    }

    private void RecreateSurface(int width, int height)
    {
        if (_grContext == null || width <= 0 || height <= 0)
            return;

        _surface?.Dispose();
        _surface = null;

        _renderTarget?.Dispose();
        _renderTarget = null;

        GL.GetInteger(GetPName.FramebufferBinding, out var framebuffer);
        var stencilBits = 8;
        GL.GetInteger(GetPName.Samples, out var samples);

        var maxSamples = _grContext.GetMaxSurfaceSampleCount(SKColorType.Rgba8888);
        if (samples > maxSamples)
            samples = maxSamples;

        var framebufferInfo = new GRGlFramebufferInfo((uint)framebuffer, SKColorType.Rgba8888.ToGlSizedFormat());
        _renderTarget = new GRBackendRenderTarget(width, height, samples, stencilBits, framebufferInfo);
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);

        _drawable.Surface = _surface;
        _drawable.CanvasSize = new SKSize(width, height);
    }

    private void UpdateStatus()
    {
        var backend = _grContext == null ? "Unavailable" : "OpenGL";
        _status.Text = $"Backend: {backend} | Size: {ClientSize.X}x{ClientSize.Y} | Click the editor, type, or tap the button";
    }

    private PointF GetPointerLocation()
    {
        return new PointF(MousePosition.X, MousePosition.Y);
    }

    private TouchActionEventArgs CreateTouchArgs(TouchActionType type, PointF location)
    {
        var args = new TouchActionEventArgs(
            PointerId,
            type,
            location,
            null,
            (float)Math.Max(0.1, _canvas.RenderingScale));

        args.IsInsideView = location.X >= 0 && location.Y >= 0 && location.X <= ClientSize.X && location.Y <= ClientSize.Y;
        args.NumberOfTouches = 1;
        args.StartingLocation = _pointerDownArgs?.StartingLocation ?? location;

        return args;
    }

    private bool IsPointerButtonDown(OpenTkMouseButton button)
    {
        return MouseState.IsButtonDown(button);
    }

    private void DrainMainThreadActions()
    {
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action();
        }
    }

    private static Canvas CreateScene(int width, int height, SkiaLabel status, SkiaEditor editor, SkiaButton button)
    {
        var title = new SkiaLabel
        {
            Text = "DrawnUi.Net on a GPU-backed SKSurface",
            FontSize = 34,
            TextColor = Colors.White,
            Margin = new Thickness(24, 24, 24, 12)
        };

        var subtitle = new SkiaLabel
        {
            Text = "Host: OpenTK GameWindow -> OpenGL framebuffer -> GRContext -> SKSurface -> DrawnUI",
            FontSize = 18,
            TextColor = Color.FromArgb("#9FB3C8"),
            Margin = new Thickness(24, 0, 24, 18)
        };

        var notes = new SkiaLabel
        {
            Text = "This sample forwards OpenTK mouse and keyboard events into DrawnUI. Use it to validate focus, taps, typing, and drag selection.",
            FontSize = 16,
            TextColor = Color.FromArgb("#C4CED8"),
            Margin = new Thickness(24, 0, 24, 24)
        };

        var root = new SkiaLayout
        {
            Type = LayoutType.Column,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Spacing = 0,
            BackgroundColor = Color.FromArgb("#11161C"),
            Children =
            {
                title,
                subtitle,
                status,
                notes,
                editor,
                button
            }
        };

        return new Canvas
        {
            WidthRequest = width,
            HeightRequest = height,
            BackgroundColor = Color.FromArgb("#0B0F14"),
            RenderingMode = RenderingModeType.AcceleratedRetained,
            Children = new List<SkiaControl> { root }
        };
    }

    private static long GetFrameTimestampNanos()
    {
        var timestamp = Stopwatch.GetTimestamp();
        return (long)(1_000_000_000.0 * timestamp / Stopwatch.Frequency);
    }
}

internal sealed class GpuDrawable : ISkiaDrawable
{
    public Func<SKSurface, SKRect, bool> OnDraw { get; set; } = static (_, _) => false;

    public SKSurface? Surface { get; set; }

    SKSurface ISkiaDrawable.Surface => Surface!;

    public bool IsHardwareAccelerated => true;

    public double FPS => 0;

    public bool IsDrawing => false;

    public bool HasDrawn => Surface != null;

    public long FrameTime { get; private set; }

    public Guid Uid { get; } = Guid.NewGuid();

    public SKSize CanvasSize { get; set; }

    public bool Update(long nanos = 0)
    {
        if (nanos > 0)
        {
            FrameTime = nanos;
        }

        return true;
    }

    public void SignalFrame(long nanoseconds)
    {
        FrameTime = nanoseconds;
    }

    public void Dispose()
    {
    }
}