namespace Add3dGallery;

/// <summary>
/// The mobs' field: a side-scroller scene drawn in code, back to front: a sky, a sun, drifting clouds, far hills, near
/// hills and a ground strip with grass and a dirt path, each layer scrolling slower the farther it is (parallax).
/// The shapes are made once per size; a frame only moves them (<see cref="Scroll"/>, in points).
/// </summary>
public class Scenery : SkiaControl
{
    float _scroll;
    float _builtW, _builtH, _builtScale;
    SKPath _clouds, _far, _near, _tufts;
    SKShader _sky, _ground, _sun;
    readonly SKPaint _paint = new() { IsAntialias = true };

    /// <summary>The top of the ground strip and the dirt path, from the field's top, in points.</summary>
    public const float GroundTop = 196, PathTop = 226, PathBottom = 250;

    /// <summary>How far the scene has moved, in points (the ground's pace; farther layers move less).</summary>
    public float Scroll
    {
        get => _scroll;
        set
        {
            if (_scroll == value)
                return;
            _scroll = value;
            Update();
        }
    }

    static float Wave(float x, float w, int k1, float p1, float a1, int k2, float p2, float a2) =>
        a1 * MathF.Sin(MathF.Tau * k1 * x / w + p1) + a2 * MathF.Sin(MathF.Tau * k2 * x / w + p2);

    /// <summary>A ridge of hills one tile wide (it repeats exactly), closed down to <paramref name="bottom"/>.</summary>
    static SKPath Ridge(float w, float bottom, float baseY, int k1, float a1, int k2, float a2, float scale)
    {
        var path = new SKPath();
        path.MoveTo(0, bottom * scale);
        for (var x = 0f; x <= w; x += 8)
            path.LineTo(x * scale, (baseY + Wave(x, w, k1, 0.6f, a1, k2, 2.1f, a2)) * scale);
        path.LineTo(w * scale, bottom * scale);
        path.Close();
        return path;
    }

    void Build(float w, float h, float s)
    {
        DisposeShapes();
        _builtW = w;
        _builtH = h;
        _builtScale = s;
        _sky = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, GroundTop * s),
            new[] { new SKColor(110, 190, 245), new SKColor(196, 232, 250) }, SKShaderTileMode.Clamp);
        _sun = SKShader.CreateRadialGradient(new SKPoint(0, 0), 70 * s,
            new[] { new SKColor(255, 246, 200), new SKColor(255, 236, 160, 160), new SKColor(255, 236, 160, 0) }, new[] { 0f, 0.45f, 1f },
            SKShaderTileMode.Clamp);
        _ground = SKShader.CreateLinearGradient(new SKPoint(0, GroundTop * s), new SKPoint(0, h * s),
            new[] { new SKColor(104, 170, 84), new SKColor(70, 128, 62) }, SKShaderTileMode.Clamp);
        _far = Ridge(w, GroundTop + 4, 150, 2, 18, 5, 7, s);
        _near = Ridge(w, GroundTop + 4, 178, 3, 12, 7, 5, s);
        _clouds = new SKPath();
        foreach (var (cx, cy, r) in new[] { (120f, 52f, 18f), (420f, 34f, 14f), (690f, 64f, 20f), (900f, 40f, 12f) })
        {
            _clouds.AddOval(SKRect.Create((cx - r * 1.9f) * s, (cy - r * 0.55f) * s, r * 3.8f * s, r * 1.3f * s));
            _clouds.AddCircle((cx - r * 0.6f) * s, (cy - r * 0.3f) * s, r * 0.85f * s);
            _clouds.AddCircle((cx + r * 0.4f) * s, (cy - r * 0.55f) * s, r * s);
        }
        _tufts = new SKPath();
        for (var x = 6f; x < w; x += 22)
        {
            var y = GroundTop + 6 + (x * 7 % 9);
            _tufts.MoveTo((x - 3) * s, y * s);
            _tufts.LineTo(x * s, (y - 7) * s);
            _tufts.LineTo((x + 3) * s, y * s);
            _tufts.Close();
            if (((int)x / 22) % 3 == 0)
            {
                var y2 = PathBottom + 10 + (x * 5 % 11);
                _tufts.MoveTo((x + 8) * s, y2 * s);
                _tufts.LineTo((x + 10) * s, (y2 - 6) * s);
                _tufts.LineTo((x + 12) * s, y2 * s);
                _tufts.Close();
            }
        }
    }

    /// <summary>Draws a tile-wide path twice, moved by <paramref name="offset"/> points, so it wraps seamlessly.</summary>
    void Tiled(SKCanvas canvas, SKRect dest, SKPath path, float offset, float w, float s)
    {
        var x = -(offset % w);
        for (var i = 0; i < 2; i++)
        {
            canvas.Save();
            canvas.Translate(dest.Left + (x + i * w) * s, dest.Top);
            canvas.DrawPath(path, _paint);
            canvas.Restore();
        }
    }

    /// <inheritdoc />
    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx);
        var dest = ctx.Destination;
        var s = ctx.Scale;
        float w = dest.Width / s, h = dest.Height / s;
        if (w <= 0 || h <= 0)
            return;
        if (w != _builtW || h != _builtH || s != _builtScale)
            Build(w, h, s);
        var canvas = ctx.Context.Canvas;
        canvas.Save();
        canvas.ClipRect(dest);

        _paint.Shader = _sky;
        canvas.Save();
        canvas.Translate(dest.Left, dest.Top);
        canvas.DrawRect(0, 0, dest.Width, GroundTop * s, _paint);
        _paint.Shader = _sun;
        canvas.Translate(w * 0.82f * s, 62 * s);
        canvas.DrawCircle(0, 0, 70 * s, _paint);
        canvas.Restore();
        _paint.Shader = null;

        _paint.Color = new SKColor(255, 255, 255, 225);
        Tiled(canvas, dest, _clouds, _scroll * 0.08f, w, s);
        _paint.Color = new SKColor(150, 196, 178);
        Tiled(canvas, dest, _far, _scroll * 0.2f, w, s);
        _paint.Color = new SKColor(102, 168, 112);
        Tiled(canvas, dest, _near, _scroll * 0.45f, w, s);

        _paint.Shader = _ground;
        canvas.DrawRect(dest.Left, dest.Top + GroundTop * s, dest.Width, dest.Height - GroundTop * s, _paint);
        _paint.Shader = null;
        // The dirt path with a darker edge.
        _paint.Color = new SKColor(214, 178, 120);
        canvas.DrawRect(dest.Left, dest.Top + PathTop * s, dest.Width, (PathBottom - PathTop) * s, _paint);
        _paint.Color = new SKColor(176, 140, 88);
        canvas.DrawRect(dest.Left, dest.Top + (PathBottom - 2) * s, dest.Width, 2 * s, _paint);
        _paint.Color = new SKColor(70, 132, 58);
        Tiled(canvas, dest, _tufts, _scroll, w, s);
        canvas.Restore();
    }

    void DisposeShapes()
    {
        _clouds?.Dispose();
        _far?.Dispose();
        _near?.Dispose();
        _tufts?.Dispose();
        _sky?.Dispose();
        _sun?.Dispose();
        _ground?.Dispose();
    }

    /// <inheritdoc />
    public override void OnDisposing()
    {
        DisposeShapes();
        _paint.Dispose();
        base.OnDisposing();
    }
}
