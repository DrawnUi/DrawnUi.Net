using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DrawnUi.Draw;

/// <summary>
/// SkiaSharp HTML canvas interop for pure WebAssembly (no Blazor).
/// Ported from SkiaSharp.Views.Blazor.Internal.SKHtmlCanvasInterop.
///
/// Two rendering paths:
///   GL (GPU)    — initGL returns {fboId, stencil, sample, depth} for GRBackendRenderTarget.
///   Raster (CPU)— initRaster + putImageData with pinned byte[] buffer.
///
/// Export-friendly: uses [JSImport]/[JSExport] (no IJSRuntime, no ElementReference).
/// Could be contributed back as SkiaSharp.Views.Web.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class SkiaHtmlCanvasInterop
{
    private const string ModuleName = "drawnui-web";

    // --- GL (GPU) path ---

    /// <summary>
    /// GL info returned by initGL (mirrors SKHtmlCanvasInterop.GLInfo).
    /// </summary>
    public record GLInfo(uint FboId, int Stencils, int Samples, int Depth);

    // Emscripten JS-library function (linked via --js-library SkiaSharpInterop.js).
    // Runs inside the module closure and stashes GL/Module on globalThis so the
    // pure-WASM JS bridge can reach the Emscripten GL object (it is not exported
    // via EXPORTED_RUNTIME_METHODS, so it is otherwise unreachable from JS).
    [DllImport("libSkiaSharp", EntryPoint = "InterceptBrowserObjects", CallingConvention = CallingConvention.Cdecl)]
    private static extern void InterceptBrowserObjects();

    private static bool _intercepted;

    /// <summary>
    /// Expose the Emscripten GL/Module objects on globalThis (idempotent).
    /// Must run before <see cref="InitGL"/> so the GPU path can find WebGL.
    /// </summary>
    public static void EnsureBrowserObjectsIntercepted()
    {
        if (_intercepted)
            return;
        try
        {
            InterceptBrowserObjects();
            _intercepted = true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"SkiaHtmlCanvasInterop: InterceptBrowserObjects failed: {e.Message}");
        }
    }

    /// <summary>
    /// Initialize a GPU (WebGL) canvas view. Returns GL info or null on failure.
    /// Mirrors SKHtmlCanvas.initGL. The callback is a C# [JSExport] function
    /// that JS calls each frame via requestAnimationFrame.
    /// </summary>
    public static GLInfo? InitGL(string elementId, Action callback)
    {
        EnsureBrowserObjectsIntercepted();

        var obj = InitGLJs(elementId, callback);
        if (obj == null)
            return null;

        return new GLInfo(
            (uint)obj.GetPropertyAsInt32("fboId"),
            obj.GetPropertyAsInt32("stencil"),
            obj.GetPropertyAsInt32("sample"),
            obj.GetPropertyAsInt32("depth"));
    }

    [JSImport("initGL", ModuleName)]
    private static partial JSObject? InitGLJs(string elementId, [JSMarshalAs<JSType.Function>] Action callback);

    // --- Raster (CPU) path ---

    /// <summary>
    /// Initialize a raster (CPU) canvas view. Returns true on success.
    /// Mirrors SKHtmlCanvas.initRaster.
    /// </summary>
    public static bool InitRaster(string elementId, Action callback)
    {
        return InitRasterJs(elementId, callback);
    }

    [JSImport("initRaster", ModuleName)]
    private static partial bool InitRasterJs(string elementId, [JSMarshalAs<JSType.Function>] Action callback);

    // --- Common ---

    /// <summary>
    /// Deinitialize a canvas view.
    /// </summary>
    [JSImport("deinit", ModuleName)]
    public static partial void Deinit(string elementId);

    /// <summary>
    /// Request a frame render. Optionally set render loop + resize.
    /// Mirrors SKHtmlCanvas.requestAnimationFrame.
    /// </summary>
    [JSImport("requestAnimationFrame", ModuleName)]
    public static partial void RequestAnimationFrame(string elementId, bool renderLoop, int width, int height);

    /// <summary>
    /// Enable/disable continuous render loop.
    /// </summary>
    [JSImport("setEnableRenderLoop", ModuleName)]
    public static partial void SetEnableRenderLoop(string elementId, bool enable);

    /// <summary>
    /// Raster path: blit pixel buffer to canvas via putImageData.
    /// Passes byte array directly (pure WASM runtime doesn't expose HEAPU8 like Emscripten).
    /// </summary>
    [JSImport("putImageData", ModuleName)]
    public static partial void PutImageData(string elementId, byte[] pixels, int width, int height);
}
