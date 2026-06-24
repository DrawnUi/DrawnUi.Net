global using System.Runtime.InteropServices.JavaScript;
global using DrawnUi.Draw;
global using DrawnUi;
global using DrawnUi.Views;
global using SkiaSharp;

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DrawnUi.Web.Sample;

public static partial class Program
{
    private static WebSkiaView? _skiaView;
    private static Canvas? _drawnCanvas;
    private static SkiaLabel? _label;
    private static int _clickCount;
    private static readonly Stopwatch _frameTimer = Stopwatch.StartNew();

    [JSExport]
    public static async Task Main()
    {
        Super.UseDrawnUi().Build();
        DrawnExtensions.StartupSettings = new DrawnUiStartupSettings { };

        await JSHost.ImportAsync("drawnui-web", "/drawnui-web.js");
        Super.Init();

        // Get initial canvas dimensions
        JsInterop.InitCanvas(800, 600);
        JsInterop.UpdateCanvasSize();
        WebInput.RenderingScale = (float)JsInterop.DevicePixelRatio;

        _drawnCanvas = CreateCanvas();
        WebInput.TargetCanvas = _drawnCanvas;

        // Create WebSkiaView (implements ISkiaDrawable) and attach to Canvas.
        // This makes OnDrawSurface work (CanvasView != null) and enables both
        // RenderingMode.Default (raster) and RenderingMode.Accelerated (GPU).
        _skiaView = new WebSkiaView("drawnui-canvas", OnRenderFrame);
        _skiaView.SetDpi(JsInterop.DevicePixelRatio);
        _skiaView.SetSize(new SKSize(JsInterop.CanvasWidth, JsInterop.CanvasHeight));
        _skiaView.OnDraw = (surface, rect) =>
        {
            var frameTimeNanos = _frameTimer.ElapsedTicks * (1_000_000_000L / Stopwatch.Frequency);
            return _drawnCanvas.RenderExternalSurface(surface, rect, frameTimeNanos);
        };

        // Try GPU first, fall back to raster — decide BEFORE touching RenderingMode.
        var useGPU = _skiaView.Init(preferGPU: true);

        // RenderingMode MUST be set before AttachCanvasView: changing it triggers
        // DrawnView.CreateSkiaView → DestroySkiaView, which disposes the attached
        // CanvasView (our WebSkiaView) and deletes it from the JS view map, killing
        // the render loop. Set it first while nothing is attached.
        _drawnCanvas.RenderingMode = useGPU && _skiaView.IsHardwareAccelerated
            ? RenderingModeType.Accelerated
            : RenderingModeType.Default;

        // Attach so DrawnView.OnDrawSurface doesn't early-return
        _drawnCanvas.AttachCanvasView(_skiaView);

        Console.WriteLine($"DrawnUI.Web: {(_skiaView.IsHardwareAccelerated ? "GPU (WebGL)" : "Raster (CPU)")} rendering");
        _skiaView.SetEnableRenderLoop(true);

        Console.WriteLine("DrawnUI.Web.Sample started successfully!");
    }

    private static Canvas CreateCanvas()
    {
        var canvas = new Canvas
        {
            WidthRequest = JsInterop.CanvasWidth,
            HeightRequest = JsInterop.CanvasHeight,
            Gestures = GesturesMode.Enabled,
            BackgroundColor = SKColors.Pink,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = CreateContent()
        };
        return canvas;
    }

    private static List<SkiaControl> CreateContent()
    {
        var layout = new SkiaLayout
        {
            Type = LayoutType.Column,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            Spacing = 20,
            Padding = new Thickness(40),
            BackgroundColor = SKColors.LightGray
        };

        _label = new SkiaLabel
        {
            Text = "Hello DrawnUI on Web!",
            FontSize = 24,
            TextColor = SKColors.DarkBlue,
            HorizontalOptions = LayoutOptions.Center
        };

        var button = new SkiaButton
        {
            Text = "Click Me!",
            FontSize = 18,
            TextColor = SKColors.White,
            BackgroundColor = SKColors.SeaGreen,
            CornerRadius = 8,
            Padding = new Thickness(20, 10),
            HorizontalOptions = LayoutOptions.Center
        }.OnTapped(OnButtonTapped);

        layout.AddSubView(_label);
        layout.AddSubView(button);
        return new List<SkiaControl> { layout };
    }

    /// <summary>
    /// Called by JS requestAnimationFrame each frame (via WebSkiaView callback).
    /// Must fire Super.OnFrame so DrawnUI animators tick (ripple, etc).
    /// </summary>
    private static void OnRenderFrame()
    {
        var frameTimeNanos = _frameTimer.ElapsedTicks * (1_000_000_000L / Stopwatch.Frequency);
        // Fire Super.OnFrame so DrawnUI animators tick (ripple, etc)
        Super.OnBrowserFrame(frameTimeNanos);
        // Set FrameTime on the drawable so CreateContext reads correct time for animators
        _skiaView?.SignalFrame(frameTimeNanos);
        _skiaView?.OnRenderFrame();
    }

    [JSExport]
    public static void OnCanvasResize(int width, int height, double pixelRatio)
    {
        JsInterop.OnCanvasResize(width, height, pixelRatio);
        WebInput.RenderingScale = (float)pixelRatio;
        _skiaView?.SetDpi(pixelRatio);
        _skiaView?.SetSize(new SKSize(width, height));
        if (_drawnCanvas != null)
        {
            _drawnCanvas.WidthRequest = width;
            _drawnCanvas.HeightRequest = height;
        }
    }

    // --- Input pass-throughs (WebInput is in the library assembly) ---

    [JSExport]
    public static void OnPointerDown(int pointerId, double x, double y, int button, int buttons)
        => WebInput.OnPointerDown(pointerId, x, y, button, buttons);

    [JSExport]
    public static void OnPointerMove(int pointerId, double x, double y, int buttons)
        => WebInput.OnPointerMove(pointerId, x, y, buttons);

    [JSExport]
    public static void OnPointerUp(int pointerId, double x, double y, int button, int buttons)
        => WebInput.OnPointerUp(pointerId, x, y, button, buttons);

    [JSExport]
    public static void OnPointerCancel(int pointerId)
        => WebInput.OnPointerCancel(pointerId);

    [JSExport]
    public static void OnWheel(double deltaX, double deltaY, int deltaMode, double x, double y)
        => WebInput.OnWheel(deltaX, deltaY, deltaMode, x, y);

    private static void OnButtonTapped(SkiaControl control)
    {
        _clickCount++;
        if (_label != null)
            _label.Text = $"Clicked {_clickCount} time(s)!";
        Console.WriteLine($"Button clicked! Count: {_clickCount}");
    }
}
