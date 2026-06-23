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
    private static WebCanvas? _canvas;
    private static Canvas? _drawnCanvas;
    private static SkiaLabel? _label;
    private static int _clickCount;
    private static readonly Stopwatch _frameTimer = Stopwatch.StartNew();

    [JSExport]
    public static async Task Main()
    {
        // Initialize DrawnUI fonts + global state (Init runs after JS module import,
        // because Init() reads devicePixelRatio via JSImport)
        Super.UseDrawnUi().Build();

        // Set up logging
        DrawnExtensions.StartupSettings = new DrawnUiStartupSettings
        {
            // Logger can be set here if needed
        };

        // Initialize JS module (must happen before any JSImport calls).
        // Path is relative to the page URL (root-served).
        await JSHost.ImportAsync("drawnui-web", "/drawnui-web.js");

        // Now safe to call Init (uses getDevicePixelRatio JSImport)
        Super.Init();

        // Initialize canvas (will be sized by JS)
        JsInterop.InitCanvas(800, 600);
        JsInterop.UpdateCanvasSize();

        // Set up input target
        WebInput.RenderingScale = (float)JsInterop.DevicePixelRatio;

        // Create DrawnUI Canvas
        _drawnCanvas = CreateCanvas();
        WebInput.TargetCanvas = _drawnCanvas;

        // Create our WebCanvas for rendering
        _canvas = new WebCanvas(JsInterop.CanvasWidth, JsInterop.CanvasHeight, JsInterop.DevicePixelRatio);

        // Start frame loop
        Super.OnFrame += OnFrame;
        JsInterop.RequestAnimationFrame();

        Console.WriteLine("DrawnUI.Web.Sample started successfully!");
    }

    private static Canvas CreateCanvas()
    {
        var canvas = new Canvas
        {
            WidthRequest = JsInterop.CanvasWidth,
            HeightRequest = JsInterop.CanvasHeight,
            Gestures = GesturesMode.Enabled,
            BackgroundColor = SKColors.White,
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

        // Create a label
        _label = new SkiaLabel
        {
            Text = "Hello DrawnUI on Web!",
            FontSize = 24,
            TextColor = SKColors.DarkBlue,
            HorizontalOptions = LayoutOptions.Center
        };

        // Create a button
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
    /// Frame callback from JS requestAnimationFrame
    /// </summary>
    [JSExport]
    public static void OnBrowserFrame(double timestamp)
    {
        Super.OnBrowserFrame(timestamp);
        JsInterop.RequestAnimationFrame();
    }

    /// <summary>
    /// Canvas resize callback from JS
    /// </summary>
    [JSExport]
    public static void OnCanvasResize(int width, int height, double pixelRatio)
    {
        JsInterop.OnCanvasResize(width, height, pixelRatio);
        WebInput.RenderingScale = (float)pixelRatio;
        _canvas?.Resize(width, height);
    }

    // --- Input pass-throughs (WebInput is in the library assembly, which doesn't
    //     generate JS exports; re-export from the main assembly so JS can reach them) ---

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
    public static void OnWheel(double deltaX, double deltaY, int deltaMode)
        => WebInput.OnWheel(deltaX, deltaY, deltaMode);

    /// <summary>
    /// Button tap handler
    /// </summary>
    private static void OnButtonTapped(SkiaControl control)
    {
        _clickCount++;
        if (_label != null)
        {
            _label.Text = $"Clicked {_clickCount} time(s)!";
        }
        Console.WriteLine($"Button clicked! Count: {_clickCount}");
    }

    /// <summary>
    /// Frame update - render and display
    /// </summary>
    private static void OnFrame(object? sender, EventArgs e)
    {
        if (_canvas?.Surface == null)
            return;

        // Get canvas dimensions
        var width = JsInterop.CanvasWidth;
        var height = JsInterop.CanvasHeight;
        var scale = (float)JsInterop.DevicePixelRatio;

        // Update canvas size if needed
        if (_drawnCanvas != null &&
            (Math.Abs(_drawnCanvas.WidthRequest - width) > 0.1 ||
             Math.Abs(_drawnCanvas.HeightRequest - height) > 0.1))
        {
            _drawnCanvas.WidthRequest = width;
            _drawnCanvas.HeightRequest = height;
            _canvas.Resize(width, height);
        }

        // Physical pixel rect (matches the SKSurface backing store)
        var physicalWidth = (int)(width * scale);
        var physicalHeight = (int)(height * scale);

        // PINK TEST: clear the surface with pink to prove the pixel pipeline works.
        // Once we see pink, we'll switch back to DrawnUI rendering.
        var canvas = _canvas.Canvas;
        if (canvas != null)
        {
            canvas.Clear(new SKColor(0xFF, 0x69, 0xB4, 0xFF)); // hot pink
        }

        // Blit pixel buffer to HTML canvas via putImageData
        JsInterop.PutImageData(_canvas.Pixels, physicalWidth, physicalHeight);
    }
}
