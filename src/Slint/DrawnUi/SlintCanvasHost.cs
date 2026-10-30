using System.Diagnostics;
using System.Runtime.CompilerServices;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;
using SlintComponent = global::Slint.ComponentInstance;
using SlintEventLoop = global::Slint.EventLoop;
using SlintImage = global::Slint.Image;
using SlintTimer = global::Slint.Timer;
using SlintTimerMode = global::Slint.TimerMode;

namespace DrawnUi.Slint;

/// <summary>
/// Runs a DrawnUI <see cref="Views.Canvas"/> in a <c>DrawnUiView</c> of a Slint component, from <c>drawnui.slint</c>.
/// </summary>
/// <remarks>
/// The canvas draws into a raster surface when something changed, and the view shows each frame as an image.
/// The view passes its size, pointer, wheel, and keyboard input back, in logical pixels, which the host scales by
/// the window's scale factor. Use it on the Slint thread: DrawnUI's main thread is the Slint thread.
/// </remarks>
public sealed class SlintCanvasHost : IDisposable
{
    /// <summary>
    /// Attaches a new canvas to the <c>DrawnUiView</c> with <paramref name="viewId"/> in <paramref name="component"/>,
    /// whose main file exports <c>DrawnUiHost</c>.
    /// </summary>
    public static SlintCanvasHost Attach(SlintComponent component, int viewId = 0)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentOutOfRangeException.ThrowIfNegative(viewId);
        EnsureInitialized();
        var views = Views.GetValue(component, c => new ComponentViews(c));
        var host = new SlintCanvasHost(component, viewId, views);
        views.Add(host);
        return host;
    }

    private static readonly ConditionalWeakTable<SlintComponent, ComponentViews> Views = new();
    private static bool _initialized;

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;
        _initialized = true;
        var slintThread = Environment.CurrentManagedThreadId;
        MainThread.Configure(SlintEventLoop.InvokeFromEventLoop, () => Environment.CurrentManagedThreadId == slintThread);
        if (!Super.Initialized)
            Super.Init();
    }

    private readonly SlintComponent _component;
    private readonly ComponentViews _views;
    private readonly SlintDrawable _drawable = new();
    private readonly SlintTimer _frameTimer = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private SKSurface? _surface;
    private int _width, _height;
    private float _logicalWidth, _logicalHeight;
    private bool _resizeScheduled;
    private int? _pressedButton;
    private (float X, float Y) _lastPointer = (float.NaN, float.NaN);
    private SlintImage? _frame;

    private SlintCanvasHost(SlintComponent component, int viewId, ComponentViews views)
    {
        _component = component;
        _views = views;
        ViewId = viewId;
        Canvas = new Canvas();
        Canvas.ConnectDesktopDrawable(_drawable);
        var refreshRate = Super.RefreshRate > 0 ? Super.RefreshRate : 60;
        // Animators and Update() mark the canvas dirty; it draws then, at most once a display frame.
        _frameTimer.Start(SlintTimerMode.Repeated, TimeSpan.FromSeconds(1 / refreshRate), () =>
        {
            if (Canvas.IsDirty || !_drawable.HasDrawn)
                Render();
        });
    }

    /// <summary>The canvas, whose <c>Content</c> the view shows.</summary>
    public Canvas Canvas { get; }

    /// <summary>The <c>view-id</c> of the view that shows the canvas.</summary>
    public int ViewId { get; }

    /// <summary>The window's scale factor: physical pixels per logical pixel.</summary>
    public float Scale => _component.Window.ScaleFactor;

    /// <summary>Raised after each frame with the time it took to draw and pass on, in milliseconds.</summary>
    public event Action<double>? FrameRendered;

    /// <summary>Draws the canvas now, instead of at the next display frame.</summary>
    public void Render()
    {
        if (_surface == null)
            return;
        var started = Stopwatch.GetTimestamp();
        var nanos = _clock.Elapsed.Ticks * 100;
        _drawable.SignalFrame(nanos);
        _drawable.IsDrawing = true;
        try
        {
            Canvas.RenderExternalSurface(_surface, new SKRect(0, 0, _width, _height), nanos);
        }
        finally
        {
            _drawable.IsDrawing = false;
        }
        _drawable.HasDrawn = true;
        _surface.Canvas.Flush();
        using var pixmap = _surface.PeekPixels();
        var frame = SlintImage.FromPremultipliedRgba8(_width, _height, pixmap.GetPixelSpan());
        _views.Show(ViewId, frame);
        _frame?.Dispose();
        _frame = frame;
        FrameRendered?.Invoke(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    /// <summary>Saves the latest frame as a PNG file, such as for tests.</summary>
    public void SaveFrame(string path)
    {
        if (_surface == null)
            throw new InvalidOperationException("The view has no size yet.");
        using var image = _surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    internal void Resize(float width, float height)
    {
        _logicalWidth = width;
        _logicalHeight = height;
        if (_resizeScheduled)
            return;
        // The view reports its size from a change handler, where Slint may still be creating it, and once for each
        // of the width and the height; draw once, after the handler returns.
        _resizeScheduled = true;
        SlintTimer.SingleShot(TimeSpan.Zero, () =>
        {
            _resizeScheduled = false;
            ApplySize();
        });
    }

    private void ApplySize()
    {
        var scale = Scale;
        var width = (int)Math.Round(_logicalWidth * scale);
        var height = (int)Math.Round(_logicalHeight * scale);
        if (width <= 0 || height <= 0 || (width == _width && height == _height && Math.Abs(Canvas.RenderingScale - scale) < 0.001))
            return;
        _width = width;
        _height = height;
        _surface?.Dispose();
        _surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        _drawable.Surface = _surface;
        _drawable.CanvasSize = new SKSize(width, height);
        Super.Screen.Density = scale;
        Canvas.RenderingScale = scale;
        // Like the other .NET heads, the canvas is as large as its surface, in points.
        Canvas.WidthRequest = _logicalWidth;
        Canvas.HeightRequest = _logicalHeight;
        _drawable.HasDrawn = false;
        Render();
    }

    internal void Pointer(int kind, int button, float x, float y)
    {
        float px = x * Scale, py = y * Scale;
        // Slint reports a move without motion after a release or a wheel event, which DrawnUI would take for the
        // user moving the mouse, ending a wheel scroll that is still animating.
        if (kind == 2 && (px, py) == _lastPointer)
            return;
        _lastPointer = (px, py);
        switch (kind)
        {
            case 0:
                if (_pressedButton != null)
                    return;
                _pressedButton = button;
                Canvas.HandleDesktopPointerDown(px, py, _width, _height, DescribePointer(button, AppoMobi.Gestures.MouseButtonState.Pressed));
                break;
            case 1:
                if (_pressedButton != button)
                    return;
                _pressedButton = null;
                Canvas.HandleDesktopPointerUp(px, py, _width, _height, DescribePointer(button, AppoMobi.Gestures.MouseButtonState.Released));
                // The right button's release asks for the context menu, after its tap, as on the OpenTK head.
                if (button == 2)
                    Canvas.HandleDesktopContextMenu(px, py, _width, _height, PointerDeviceType.Mouse);
                break;
            case 2:
                var held = _pressedButton is { } b ? DescribePointer(b, AppoMobi.Gestures.MouseButtonState.Pressed) : null;
                Canvas.HandleDesktopPointerMove(px, py, _pressedButton != null, _width, _height, held);
                break;
            default:
                if (_pressedButton is { } pressed)
                {
                    _pressedButton = null;
                    Canvas.HandleDesktopPointerUp(px, py, _width, _height, DescribePointer(pressed, AppoMobi.Gestures.MouseButtonState.Released));
                }
                Canvas.HandleDesktopPointerLeave();
                break;
        }
    }

    /// <summary>
    /// Slint scrolls 60 logical pixels a wheel notch, and DrawnUI takes the Windows convention of 120 a notch, away from
    /// the user; a touchpad reports both axes, and the dominant one goes on.
    /// </summary>
    internal void Wheel(float x, float y, float deltaX, float deltaY)
    {
        var horizontal = Math.Abs(deltaX) > Math.Abs(deltaY);
        var delta = (int)Math.Round((horizontal ? deltaX : deltaY) * 2);
        Canvas.HandleDesktopWheel(x * Scale, y * Scale, delta, _width, _height, horizontal);
    }

    internal bool Key(bool pressed, string text, bool shift, bool control, bool alt) =>
        pressed ? SlintKeys.Pressed(Canvas, text, shift, control, alt) : SlintKeys.Released(text);

    private static PointerData DescribePointer(int button, AppoMobi.Gestures.MouseButtonState state) => new()
    {
        Button = button switch
        {
            1 => AppoMobi.Gestures.MouseButton.Middle,
            2 => AppoMobi.Gestures.MouseButton.Right,
            _ => AppoMobi.Gestures.MouseButton.Left,
        },
        ButtonNumber = button,
        State = state,
        DeviceType = PointerDeviceType.Mouse,
    };

    /// <summary>Stops drawing, and releases the canvas and its surface.</summary>
    public void Dispose()
    {
        _frameTimer.Dispose();
        _views.Remove(this);
        Canvas.Dispose();
        _surface?.Dispose();
        _surface = null;
        _frame?.Dispose();
    }

    /// <summary>The views of one component: the frames model of its <c>DrawnUiHost</c>, and its callbacks.</summary>
    private sealed class ComponentViews
    {
        private readonly global::Slint.ListModel<SlintImage> _frames = new();
        private readonly Dictionary<int, SlintCanvasHost> _hosts = new();

        public ComponentViews(SlintComponent component)
        {
            const string Global = "DrawnUiHost";
            component.SetGlobalProperty(Global, "frames", _frames);
            component.SetGlobalCallback(Global, "resized", args => Find(args)?.Resize(ToFloat(args[1]), ToFloat(args[2])));
            component.SetGlobalCallback(Global, "pointer", args =>
                Find(args)?.Pointer(ToInt(args[1]), ToInt(args[2]), ToFloat(args[3]), ToFloat(args[4])));
            component.SetGlobalCallback(Global, "wheel", args =>
                Find(args)?.Wheel(ToFloat(args[1]), ToFloat(args[2]), ToFloat(args[3]), ToFloat(args[4])));
            component.SetGlobalCallback(Global, "key", args =>
                Find(args)?.Key((bool)args[1]!, (string)args[2]!, (bool)args[3]!, (bool)args[4]!, (bool)args[5]!) ?? false);
        }

        public void Add(SlintCanvasHost host)
        {
            if (!_hosts.TryAdd(host.ViewId, host))
                throw new InvalidOperationException($"A canvas is already attached to the view {host.ViewId}.");
            while (_frames.Count <= host.ViewId)
                _frames.Add(new SlintImage());
        }

        public void Remove(SlintCanvasHost host)
        {
            if (_hosts.Remove(host.ViewId) && host.ViewId < _frames.Count)
                _frames[host.ViewId] = new SlintImage();
        }

        public void Show(int viewId, SlintImage frame) => _frames[viewId] = frame;

        private SlintCanvasHost? Find(object?[] args) => _hosts.GetValueOrDefault(ToInt(args[0]));

        private static int ToInt(object? value) => Convert.ToInt32(value);

        private static float ToFloat(object? value) => Convert.ToSingle(value);
    }
}
