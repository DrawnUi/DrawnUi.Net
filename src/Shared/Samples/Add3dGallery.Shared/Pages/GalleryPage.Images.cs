namespace Add3dGallery;

/// <summary>Images on 3D: a product box with its label, every image drawn in code with Skia.</summary>
public partial class GalleryPage
{
    SkiaModel3D _product;
    Texture3D[] _labels;
    int _label;

    /// <summary>An image drawn with Skia, ready to put on a 3D surface.</summary>
    static Texture3D Painted(int width, int height, Action<SKCanvas, float, float> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        draw(surface.Canvas, width, height);
        using var image = surface.Snapshot();
        return Texture3D.FromImage(image);
    }

    static SKPaint Fill(SKColor color) => new() { IsAntialias = true, Color = color };

    /// <summary>Text centered on <paramref name="x"/>, its baseline at <paramref name="y"/>, in the app's title font.</summary>
    static void Centered(SKCanvas canvas, string text, float x, float y, float size, SKColor color)
    {
        using var font = new SKFont(SkiaFontManager.Instance.GetFont(Theme.Title) ?? SKTypeface.Default, size);
        using var paint = Fill(color);
        canvas.DrawText(text, x, y, SKTextAlign.Center, font, paint);
    }

    /// <summary>A product box's label: a fruit on a gradient, a frame and the flavor's name.</summary>
    static Texture3D Label(string name, SKColor top, SKColor bottom, SKColor fruit) => Painted(512, 512, (c, w, h) =>
    {
        using (var gradient = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, h), new[] { top, bottom }, SKShaderTileMode.Clamp) })
            c.DrawRect(0, 0, w, h, gradient);
        using (var frame = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 10 })
            c.DrawRoundRect(26, 26, w - 52, h - 52, 36, 36, frame);
        using (var paint = Fill(fruit))
            c.DrawCircle(w / 2, 214, 112, paint);
        using (var paint = Fill(new SKColor(255, 255, 255, 90)))
            c.DrawCircle(w / 2 - 38, 176, 30, paint);
        using (var paint = Fill(new SKColor(76, 201, 140)))
            c.DrawOval(SKRect.Create(w / 2 + 6, 70, 70, 34), paint);
        Centered(c, name, w / 2, 410, 76, SKColors.White);
        Centered(c, "made in code", w / 2, 456, 26, new SKColor(255, 255, 255, 220));
    });

    static Texture3D[] Labels() => new[]
    {
        Label("BERRY", new SKColor(255, 99, 132), new SKColor(124, 58, 237), new SKColor(190, 24, 93)),
        Label("MINT", new SKColor(52, 211, 153), new SKColor(13, 110, 253), new SKColor(16, 185, 129)),
        Label("SUN", new SKColor(255, 214, 10), new SKColor(253, 126, 20), new SKColor(255, 159, 28)),
    };

    /// <summary>A white box: the label shows as it is.</summary>
    static Model3D ProductBox() => new Model3D().Part("box", Mesh3D.Cuboid(1, 1, 1), Colors.White);

    /// <summary>The next image of a list on a material.</summary>
    static void NextImage(SkiaModel3D model, string material, Texture3D[] images, ref int index)
    {
        index = (index + 1) % images.Length;
        model.SetMaterialImage(material, images[index]);
    }

    /// <summary>The section's tile.</summary>
    SkiaControl ImagesSection()
    {
        _labels ??= Labels();
        return new SkiaWrap
        {
            Spacing = 16,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                Tile("Product box", "A box with its label: tap it or switch the design.",
                    Shaded(ProductBox(), 28, 18)
                        .Adapt(me => me.AutoRotate = 40)
                        .MaterialImage("box", _labels[0])
                        .Assign(out _product)
                        .OnTapped3D((me, _) => NextImage(me, "box", _labels, ref _label)),
                    new SkiaRow
                    {
                        Spacing = 8,
                        Children =
                        {
                            new UiButton("Next design", Theme.Accent, 124).OnTapped(_ => NextImage(_product, "box", _labels, ref _label)),
                            // The box's edges: hard (the default) or smooth (4x multisampling).
                            new UiButton("Edges: hard", Theme.Quiet, 124)
                                .OnTapped(me =>
                                {
                                    _product.Antialias = !_product.Antialias;
                                    me.Text = _product.Antialias ? "Edges: smooth" : "Edges: hard";
                                    me.Fill = _product.Antialias ? Theme.Accent : Theme.Quiet;
                                    me.AccessibilityIsPressed = _product.Antialias;
                                }),
                        }
                    }),
            }
        };
    }
}
