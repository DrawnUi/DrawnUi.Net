namespace Add3dGallery;

/// <summary>Images on 3D: a product box with its label and a ball with three skins, every image drawn in code with Skia.</summary>
public partial class GalleryPage
{
    SkiaModel3D _product, _ball;
    Texture3D[] _labels, _skins;
    int _label, _skin;

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

    /// <summary>Skins for a sphere: the image wraps once around (left to right) from the top pole to the bottom one.</summary>
    static Texture3D[] Skins() => new[]
    {
        // A beach ball.
        Painted(1024, 512, (c, w, h) =>
        {
            var colors = new[] { new SKColor(255, 77, 109), SKColors.White, new SKColor(255, 196, 61), SKColors.White, new SKColor(92, 140, 255), SKColors.White };
            for (var i = 0; i < colors.Length; i++)
                using (var paint = Fill(colors[i]))
                    c.DrawRect(i * w / 6, 0, w / 6, h, paint);
            using var white = Fill(SKColors.White);
            c.DrawRect(0, 0, w, 46, white);
            c.DrawRect(0, h - 46, w, 46, white);
        }),
        // A planet's bands and its storm.
        Painted(1024, 512, (c, w, h) =>
        {
            var tones = new[] { new SKColor(232, 196, 150), new SKColor(196, 140, 92), new SKColor(244, 222, 186), new SKColor(170, 112, 74) };
            var y = 0f;
            for (var i = 0; y < h; i++)
            {
                var band = 18 + 22 * (MathF.Sin(i * 1.7f) * 0.5f + 0.5f);
                using (var paint = Fill(tones[i % tones.Length]))
                    c.DrawRect(0, y, w, band + 1, paint);
                y += band;
            }
            using (var paint = Fill(new SKColor(200, 84, 60)))
                c.DrawOval(SKRect.Create(620, 300, 150, 70), paint);
            using (var paint = Fill(new SKColor(226, 120, 84)))
                c.DrawOval(SKRect.Create(640, 312, 110, 46), paint);
        }),
        // Dots.
        Painted(1024, 512, (c, w, h) =>
        {
            using (var paint = Fill(new SKColor(30, 41, 90)))
                c.DrawRect(0, 0, w, h, paint);
            using var dot = Fill(new SKColor(255, 196, 61));
            for (var row = 0; row < 8; row++)
            for (var col = 0; col < 16; col++)
                c.DrawCircle((col + (row % 2 == 0 ? 0.25f : 0.75f)) * w / 16, (row + 0.5f) * h / 8, 13, dot);
        }),
    };

    /// <summary>A white box and a white ball: the images show as they are.</summary>
    static Model3D ProductBox() => new Model3D().Part("box", Mesh3D.Cuboid(1, 1, 1), Colors.White);

    static Model3D Ball() => new Model3D().Part("ball", Mesh3D.Sphere(1), Colors.White);

    /// <summary>The next image of a list on a material.</summary>
    static void NextImage(SkiaModel3D model, string material, Texture3D[] images, ref int index)
    {
        index = (index + 1) % images.Length;
        model.SetMaterialImage(material, images[index]);
    }

    /// <summary>The section's two tiles.</summary>
    SkiaControl ImagesSection()
    {
        _labels = Labels();
        _skins = Skins();
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
                    new UiButton("Next design", Theme.Accent).OnTapped(_ => NextImage(_product, "box", _labels, ref _label))),
                Tile("Ball", "One sphere, three skins: a beach ball, a planet, dots.",
                    Shaded(Ball(), 0, 16)
                        .Adapt(me => me.AutoRotate = 60)
                        .MaterialImage("ball", _skins[0])
                        .Assign(out _ball)
                        .OnTapped3D((me, _) => NextImage(me, "ball", _skins, ref _skin)),
                    new UiButton("Next skin", Theme.Accent).OnTapped(_ => NextImage(_ball, "ball", _skins, ref _skin))),
            }
        };
    }
}
