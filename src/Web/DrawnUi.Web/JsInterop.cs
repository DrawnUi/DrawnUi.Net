using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DrawnUi.Draw;

/// <summary>
/// Central JS interop for DrawnUI.Web - single static partial class for all imports/exports
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class JsInterop
{
    /// <summary>
    /// Current canvas width in CSS pixels
    /// </summary>
    public static int CanvasWidth { get; private set; }

    /// <summary>
    /// Current canvas height in CSS pixels
    /// </summary>
    public static int CanvasHeight { get; private set; }

    /// <summary>
    /// Device pixel ratio for scaling
    /// </summary>
    public static double DevicePixelRatio { get; private set; } = 1.0;

    /// <summary>
    /// Initialize the canvas and get its dimensions
    /// </summary>
    [JSImport("initCanvas", "drawnui-web")]
    public static partial void InitCanvas(int targetWidth, int targetHeight);

    /// <summary>
    /// Get the raw WebGL texture ID from JS (for GPU rendering)
    /// Returns -1 if not available
    /// </summary>
    [JSImport("getGlTextureId", "drawnui-web")]
    public static partial int GetGlTextureId();

    /// <summary>
    /// Get the WebGL context handle (GRDirectContext pointer)
    /// Returns 0 if not available
    /// </summary>
    [JSImport("getGlContext", "drawnui-web")]
    public static partial int GetGlContext();

    /// <summary>
    /// Get canvas width in CSS pixels
    /// </summary>
    [JSImport("getCanvasWidth", "drawnui-web")]
    public static partial int GetCanvasWidth();

    /// <summary>
    /// Get canvas height in CSS pixels
    /// </summary>
    [JSImport("getCanvasHeight", "drawnui-web")]
    public static partial int GetCanvasHeight();

    /// <summary>
    /// Get device pixel ratio
    /// </summary>
    [JSImport("getDevicePixelRatio", "drawnui-web")]
    public static partial double GetDevicePixelRatio();

    /// <summary>
    /// Request a single frame callback to OnBrowserFrame
    /// </summary>
    [JSImport("requestAnimationFrame", "drawnui-web")]
    public static partial void RequestAnimationFrame();

    /// <summary>
    /// Update canvas with PNG image data
    /// </summary>
    [JSImport("updateCanvasWithPng", "drawnui-web")]
    public static partial void UpdateCanvasWithPng(byte[] pngBytes);

    /// <summary>
    /// Blit pixel buffer to HTML canvas via putImageData.
    /// Passes byte array directly (pure WASM runtime doesn't expose HEAPU8 like Emscripten).
    /// </summary>
    [JSImport("putImageData", "drawnui-web")]
    public static partial void PutImageData(byte[] pixels, int width, int height);

    /// <summary>
    /// Called by JS when canvas is resized
    /// </summary>
    [JSExport]
    public static void OnCanvasResize(int width, int height, double pixelRatio)
    {
        CanvasWidth = width;
        CanvasHeight = height;
        DevicePixelRatio = pixelRatio;
    }

    /// <summary>
    /// Update canvas dimensions from JS
    /// </summary>
    public static void UpdateCanvasSize()
    {
        CanvasWidth = GetCanvasWidth();
        CanvasHeight = GetCanvasHeight();
        DevicePixelRatio = GetDevicePixelRatio();
    }
}
