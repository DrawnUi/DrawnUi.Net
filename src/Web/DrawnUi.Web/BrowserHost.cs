using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using SkiaSharp;

namespace DrawnUi.Draw;

/// <summary>
/// Hosts a DrawnUI <see cref="Canvas"/> on an HTML canvas element for pure WebAssembly.
/// Owns all browser glue: JS module import, the SkiaSharp renderer (<see cref="WebSkiaView"/>),
/// input routing, the requestAnimationFrame loop and resize. Apps never touch this directly —
/// they call <see cref="DrawnUiBuilderWebExtensions.RunAsync"/>.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class BrowserHost
{
    private static WebSkiaView? _view;
    private static Canvas? _canvas;
    private static readonly Stopwatch _frameTimer = Stopwatch.StartNew();

    internal static async Task RunAsync(DrawnUiBuilder builder, string elementId, Func<Canvas> content)
    {
        // Register the JS module for C# [JSImport] bindings, then init core + fonts/assets.
        await JSHost.ImportAsync("drawnui-web", "/_content/DrawnUi.Web/drawnui-web.js");
        Super.Init();
        await builder.BuildAsync();

        // Fonts registered via ConfigureFonts can't load from the Mono VFS (WasmFilesToBundle is a
        // no-op in the .NET WASM SDK), so fetch them over HTTP from static web assets, like Blazor.
        await SkiaFontManager.Instance.InitializeWebAsync(JsInterop.GetBaseUrl());

        // Wire DOM input listeners + read the element's CSS size / device pixel ratio.
        JsInterop.InitCanvas(0, 0);
        JsInterop.UpdateCanvasSize();
        WebInput.RenderingScale = (float)JsInterop.DevicePixelRatio;

        // Build the user's Canvas; size from explicit requests, else the element size.
        var canvas = content();
        if (canvas.WidthRequest <= 0)
            canvas.WidthRequest = JsInterop.CanvasWidth;
        if (canvas.HeightRequest <= 0)
            canvas.HeightRequest = JsInterop.CanvasHeight;
        _canvas = canvas;
        WebInput.TargetCanvas = canvas;

        // Mirror Blazor Canvas GestureStyle at lib level: route Gestures mode to canvas/page CSS
        // (+ iOS swipe-away guard for Lock). Disabled = leave page defaults untouched.
        if (canvas.Gestures != GesturesMode.Disabled)
            JsInterop.ApplyGestureStyle(elementId, canvas.Gestures == GesturesMode.Lock);

        // The renderer/handler is created from the Canvas: RenderingMode picks GPU vs raster.
        var view = new WebSkiaView(elementId, OnRenderFrame);
        view.SetDpi(JsInterop.DevicePixelRatio);
        view.SetSize(new SKSize(JsInterop.CanvasWidth, JsInterop.CanvasHeight));
        view.OnDraw = (surface, rect) =>
            canvas.RenderExternalSurface(surface, rect, Nanos());

        var useGpu = view.Init(preferGPU: canvas.RenderingMode == RenderingModeType.Accelerated);

        // RenderingMode must be final BEFORE attach: changing it after attach disposes the
        // view and kills the loop. Downgrade to raster here if GPU was unavailable.
        canvas.RenderingMode = useGpu && view.IsHardwareAccelerated
            ? RenderingModeType.Accelerated
            : RenderingModeType.Default;

        canvas.AttachCanvasView(view);
        _view = view;
        view.SetEnableRenderLoop(true);
    }

    private static long Nanos() => _frameTimer.ElapsedTicks * (1_000_000_000L / Stopwatch.Frequency);

    private static void OnRenderFrame()
    {
        var nanos = Nanos();
        Super.OnBrowserFrame(nanos);   // tick DrawnUI animators
        _view?.SignalFrame(nanos);
        _view?.OnRenderFrame();
    }

    /// <summary>
    /// Called by JS when the canvas element resizes (wired from main.js to this assembly).
    /// </summary>
    [JSExport]
    public static void OnCanvasResize(int width, int height, double pixelRatio)
    {
        JsInterop.OnCanvasResize(width, height, pixelRatio);
        WebInput.RenderingScale = (float)pixelRatio;
        _view?.SetDpi(pixelRatio);
        _view?.SetSize(new SKSize(width, height));
        if (_canvas != null)
        {
            _canvas.WidthRequest = width;
            _canvas.HeightRequest = height;
        }
    }
}

/// <summary>
/// Builder entry points for hosting DrawnUI in the browser.
/// </summary>
[SupportedOSPlatform("browser")]
public static class DrawnUiBuilderWebExtensions
{
    /// <summary>
    /// Builds DrawnUI and runs the given <see cref="Canvas"/> on the HTML canvas element
    /// with the given id, wiring rendering, input and the frame loop automatically.
    /// </summary>
    /// <param name="builder">The DrawnUI builder.</param>
    /// <param name="elementId">Id of the HTML &lt;canvas&gt; element to render into.</param>
    /// <param name="content">Factory that creates the root <see cref="Canvas"/>.</param>
    public static Task RunAsync(this DrawnUiBuilder builder, string elementId, Func<Canvas> content)
        => BrowserHost.RunAsync(builder, elementId, content);
}
