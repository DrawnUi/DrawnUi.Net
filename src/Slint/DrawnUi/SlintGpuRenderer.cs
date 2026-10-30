using SkiaSharp;
using GraphicsApi = global::Slint.GraphicsApi;
using SlintImage = global::Slint.Image;

namespace DrawnUi.Slint;

/// <summary>
/// Draws canvases with Skia on the OpenGL context of a window's Slint renderer, each into a texture that its view shows,
/// without copying pixels. Use it in the window's rendering notifier, where the context is current.
/// </summary>
internal sealed class SlintGpuRenderer : IDisposable
{
    private readonly SlintGl _gl;
    private readonly GRContext _context;

    private SlintGpuRenderer(SlintGl gl, GRContext context)
    {
        _gl = gl;
        _context = context;
    }

    /// <summary>
    /// Null when Skia can't use the context, such as an OpenGL version it doesn't support. Call it in the rendering
    /// notifier, where Slint's context is current: Skia loads its functions from the current context.
    /// </summary>
    public static SlintGpuRenderer? Create(GraphicsApi api)
    {
        var glInterface = GRGlInterface.Create();
        if (glInterface == null)
            return null;
        var context = GRContext.CreateGl(glInterface);
        if (context == null)
        {
            glInterface.Dispose();
            return null;
        }
        return new SlintGpuRenderer(new SlintGl(api), context);
    }

    /// <summary>A texture and the Skia surface that renders into it.</summary>
    public sealed class Target : IDisposable
    {
        private readonly SlintGpuRenderer _renderer;
        private readonly uint _texture;
        private readonly GRBackendTexture _backendTexture;

        internal Target(SlintGpuRenderer renderer, int width, int height)
        {
            _renderer = renderer;
            Width = width;
            Height = height;
            _texture = renderer._gl.CreateTexture(width, height);
            _backendTexture = new GRBackendTexture(width, height, false, new GRGlTextureInfo(SlintGl.Texture2D, _texture, SlintGl.Rgba8));
            Surface = SKSurface.Create(renderer._context, _backendTexture, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888)
                ?? throw new InvalidOperationException("Skia can't render into an OpenGL texture of this context.");
            Image = SlintImage.FromBorrowedOpenGLTexture(_texture, width, height);
        }

        public int Width { get; }
        public int Height { get; }
        public SKSurface Surface { get; }

        /// <summary>The texture as an image for the view, which shows each frame drawn into it.</summary>
        public SlintImage Image { get; }

        public void Dispose()
        {
            Image.Dispose();
            Surface.Dispose();
            _backendTexture.Dispose();
            _renderer._gl.DeleteTexture(_texture);
        }
    }

    public Target CreateTarget(int width, int height) => new(this, width, height);

    /// <summary>Runs <paramref name="draw"/> with Skia's state in sync, and restores Slint's afterwards.</summary>
    public void Draw(Action draw)
    {
        var state = _gl.Save();
        try
        {
            // Slint's renderer changed the context since Skia last drew.
            _context.ResetContext();
            draw();
            _context.Flush();
        }
        finally
        {
            _gl.Restore(state);
        }
    }

    public void Dispose() => _context.Dispose();
}
