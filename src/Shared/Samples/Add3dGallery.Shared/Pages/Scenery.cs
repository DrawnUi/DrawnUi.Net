namespace Add3dGallery;

/// <summary>
/// The mobs' field: a side-scroller scene drawn in code with gradients and smooth outlines, the same as DrawnUI for
/// Rust's gallery3d: a still sky and sun, then clouds, far hills, near hills and the grass with its dirt path, each
/// sliding at its own share of the pan (parallax). Every moving layer repeats every 1000 points. The shapes are made
/// once per size and painted once into a GPU image per layer (the sky and sun into one more); a frame draws each layer
/// as that image repeated sideways, at its exact slide (<see cref="Pan"/>, in points), fractions of a pixel included.
/// </summary>
public class Scenery : SkiaControl
{
    /// <summary>One tile of the scene, in points: every moving layer repeats over it.</summary>
    public const float TileWidth = 1000;

    /// <summary>Where the mobs walk: the top and bottom of the ground band, in points from the field's top.</summary>
    public const float GroundTop = 196, GroundBottom = 262;

    /// <summary>How fast the scene pans, points per second, and each layer's share of it (far ones move less).</summary>
    public const float PanSpeed = 33;

    static readonly float[] Parallax = { 0.08f, 0.2f, 0.45f, 1.0f };

    /// <summary>A layer: its shapes and their fills, in device pixels.</summary>
    sealed class Layer : IDisposable
    {
        public readonly List<(SKPath Path, SKShader Shader, SKColor Color)> Parts = new();

        public void Dispose()
        {
            foreach (var (path, shader, _) in Parts)
            {
                path.Dispose();
                shader?.Dispose();
            }
        }
    }

    float _pan;
    float _builtH, _builtScale = -1;
    Layer[] _layers = Array.Empty<Layer>();
    SKShader _sky;
    readonly SKPaint _paint = new() { IsAntialias = true };
    readonly SKPaint _glow = new() { IsAntialias = true, Color = new SKColor(255, 230, 150, 230), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 13) };

    /// <summary>How far the scene has panned, in points (the ground's pace; farther layers move less).</summary>
    public float Pan
    {
        get => _pan;
        set
        {
            if (_pan == value)
                return;
            _pan = value;
            Update();
        }
    }

    static SKShader Vertical(float top, float bottom, SKColor from, SKColor to, float s) =>
        SKShader.CreateLinearGradient(new SKPoint(0, top * s), new SKPoint(0, bottom * s), new[] { from, to }, SKShaderTileMode.Clamp);

    /// <summary>A ridge line across one tile down to <paramref name="bottom"/>, filled top to bottom with a gradient.</summary>
    static (SKPath, SKShader, SKColor) Ridge(float baseY, (float Amp, float Period, float Phase)[] waves, float bottom, SKColor from, SKColor to, float s)
    {
        // One device pixel past each end: the twin copies overlap there, so no antialiased edge shows between them.
        var path = new SKPath();
        path.MoveTo(-1, bottom * s);
        var top = bottom;
        for (var i = 0; i <= 100; i++)
        {
            var x = i * 10f;
            var y = baseY;
            foreach (var (amp, period, phase) in waves)
                y -= amp * MathF.Sin(x / period * MathF.Tau + phase);
            top = MathF.Min(top, y);
            path.LineTo(i == 0 ? -1 : i == 100 ? TileWidth * s + 1 : x * s, y * s);
        }
        path.LineTo(TileWidth * s + 1, bottom * s);
        path.Close();
        return (path, Vertical(top, bottom, from, to, s), from);
    }

    /// <summary>The still sky and sun, then one tile of each moving layer, as GPU images of the canvas's context; each
    /// tile also as a shader that repeats it sideways, so a layer is one draw at any fraction of a pixel, without a seam.</summary>
    SKImage _backdrop;
    readonly SKImage[] _images = new SKImage[4];
    readonly SKShader[] _tiles = new SKShader[4];
    GRContext _imagesContext;

    void PaintImages(GRContext gr, int w, int h)
    {
        DisposeImages();
        _imagesContext = gr;
        var tile = (int)MathF.Round(TileWidth * _builtScale);
        using (var surface = SKSurface.Create(gr, false, new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul)))
        {
            if (surface == null)
                return;
            PaintBackdrop(surface.Canvas, w, h, _builtScale);
            _backdrop = surface.Snapshot();
        }
        for (var i = 0; i < _layers.Length; i++)
        {
            using var surface = SKSurface.Create(gr, false, new SKImageInfo(tile, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (surface == null)
                return;
            surface.Canvas.Clear(SKColors.Transparent);
            PaintLayer(surface.Canvas, i);
            _images[i] = surface.Snapshot();
            _tiles[i] = _images[i].ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Decal, new SKSamplingOptions(SKFilterMode.Linear));
        }
    }

    void PaintBackdrop(SKCanvas canvas, float w, float h, float s)
    {
        _paint.Shader = _sky;
        canvas.DrawRect(0, 0, w, h, _paint);
        _paint.Shader = null;
        // The sun: a soft glow, then its disc.
        canvas.DrawCircle(795 * s, 69 * s, 38 * s, _glow);
        _paint.Color = new SKColor(255, 236, 170);
        canvas.DrawCircle(795 * s, 69 * s, 35 * s, _paint);
    }

    void PaintLayer(SKCanvas canvas, int i)
    {
        foreach (var (path, shader, color) in _layers[i].Parts)
        {
            _paint.Shader = shader;
            _paint.Color = color;
            canvas.DrawPath(path, _paint);
        }
        _paint.Shader = null;
    }

    void DisposeImages()
    {
        _backdrop?.Dispose();
        _backdrop = null;
        for (var i = 0; i < _images.Length; i++)
        {
            _tiles[i]?.Dispose();
            _tiles[i] = null;
            _images[i]?.Dispose();
            _images[i] = null;
        }
        _imagesContext = null;
    }

    void Build(float h, float s)
    {
        DisposeShapes();
        DisposeImages();
        _builtH = h;
        _builtScale = s;
        _sky = Vertical(0, h, new SKColor(92, 164, 232), new SKColor(214, 238, 250), s);

        var clouds = new Layer();
        foreach (var (x, y, k) in new[] { (60f, 26f, 1f), (340f, 52f, 0.7f), (610f, 18f, 1.2f), (860f, 60f, 0.8f) })
        {
            var ellipse = new SKPath();
            ellipse.AddOval(SKRect.Create(x * s, (y + 14 * k) * s, 90 * k * s, 30 * k * s));
            clouds.Parts.Add((ellipse, null, new SKColor(255, 255, 255, 220)));
            var puffs = new SKPath();
            puffs.AddOval(SKRect.Create((x + 18 * k) * s, y * s, 44 * k * s, 44 * k * s));
            puffs.AddOval(SKRect.Create((x + 46 * k) * s, (y + 8 * k) * s, 32 * k * s, 32 * k * s));
            clouds.Parts.Add((puffs, null, new SKColor(255, 255, 255, 230)));
        }
        var far = new Layer();
        far.Parts.Add(Ridge(150, new[] { (22f, 1000f, 0.3f), (12f, 500f, 1.7f), (5f, 250f, 0.4f) }, h, new SKColor(150, 190, 196), new SKColor(176, 210, 210), s));
        var near = new Layer();
        near.Parts.Add(Ridge(190, new[] { (26f, 1000f, 2.1f), (14f, 333.33f, 0.9f), (6f, 200f, 2.5f) }, h, new SKColor(98, 160, 104), new SKColor(70, 128, 80), s));
        var ground = new Layer();
        // Grass, then the dirt path the mobs walk on.
        ground.Parts.Add(Ridge(GroundTop - 6, new[] { (3f, 500f, 0f), (2f, 125f, 1.3f) }, h, new SKColor(112, 178, 72), new SKColor(64, 120, 48), s));
        ground.Parts.Add(Ridge(GroundTop + 22, new[] { (4f, 1000f, 0.8f), (3f, 250f, 2.2f) }, GroundBottom + 4, new SKColor(214, 182, 128), new SKColor(190, 152, 98), s));
        _layers = new[] { clouds, far, near, ground };
    }

    /// <inheritdoc />
    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);
        var dest = ctx.Destination;
        var s = ctx.Scale;
        var h = dest.Height / s;
        if (dest.Width <= 0 || h <= 0)
            return;
        if (h != _builtH || s != _builtScale)
            Build(h, s);
        var canvas = ctx.Context.Canvas;
        var gr = Superview?.GetGRContext();
        int w = (int)MathF.Round(dest.Width), hp = (int)MathF.Round(dest.Height);
        if (gr != null && (_imagesContext != gr || _backdrop == null || _backdrop.Width != w || _backdrop.Height != hp))
            PaintImages(gr, w, hp);
        var images = gr != null && _backdrop != null && _images[^1] != null;
        canvas.Save();
        canvas.ClipRect(dest);
        // The field's corner on a whole device pixel: the backdrop image maps one to one.
        canvas.Translate(MathF.Round(dest.Left), MathF.Round(dest.Top));

        if (images)
            canvas.DrawImage(_backdrop, 0, 0);
        else
            PaintBackdrop(canvas, dest.Width, dest.Height, s);

        // Every slide keeps its fraction of a pixel: a layer snapped to whole pixels steps 0 or 1 pixel on alternate
        // frames at these speeds (the ground moves under one pixel a frame), which reads as a stutter.
        var tile = MathF.Round(TileWidth * s);
        for (var i = 0; i < _layers.Length; i++)
        {
            var shift = (_pan * Parallax[i] * s) % tile;
            if (images)
            {
                // One draw of the repeating tile shader: no edge between copies to keep on a pixel.
                _paint.Shader = _tiles[i];
                _paint.Color = SKColors.White; // a shader takes the paint's alpha: keep it opaque
                canvas.Save();
                canvas.Translate(-shift, 0);
                canvas.DrawRect(shift, 0, dest.Width, dest.Height, _paint);
                canvas.Restore();
                _paint.Shader = null;
                continue;
            }
            // No GPU context: the shapes twice, side by side; they overlap by a pixel at each end, so no gap opens.
            for (var copy = 0; copy < 2; copy++)
            {
                canvas.Save();
                canvas.Translate(copy * tile - shift, 0);
                PaintLayer(canvas, i);
                canvas.Restore();
            }
        }
        canvas.Restore();
    }

    void DisposeShapes()
    {
        foreach (var layer in _layers)
            layer.Dispose();
        _layers = Array.Empty<Layer>();
        _sky?.Dispose();
        _sky = null;
    }

    /// <inheritdoc />
    public override void OnDisposing()
    {
        DisposeImages();
        DisposeShapes();
        _paint.Dispose();
        _glow.Dispose();
        base.OnDisposing();
    }
}
