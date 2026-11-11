namespace Add3dGallery;

/// <summary>
/// Card flip: a loyalty card in 3D, its front and back drawn in code with Skia. It leans toward the pointer (or the
/// finger) and turns over with a smooth half turn on a tap or the Flip button.
/// </summary>
public partial class GalleryPage
{
    /// <summary>The card in model units: a bank card's proportions (85.6 x 54 mm).</summary>
    const float CardW = 8.56f, CardH = 5.4f, CardDepth = 0.06f;

    SkiaModel3D _card;
    UiButton _flipButton;
    bool _flipped;
    /// <summary>The half turn: where it is and where it goes (0 = front, 180 = back), degrees.</summary>
    float _cardTurn, _cardTurnTo;
    /// <summary>The lean toward the pointer, degrees, now and wanted.</summary>
    float _leanX, _leanY, _leanToX, _leanToY;

    static SKPath CardOutline()
    {
        var path = new SKPath();
        path.AddRoundRect(new SKRoundRect(SKRect.Create(CardW, CardH), 0.38f));
        return path;
    }

    /// <summary>
    /// The card: a front half and a back half, each a rounded slab with its own image. The back half is turned round,
    /// so its image reads right from behind.
    /// </summary>
    static Model3D CardModel()
    {
        var half = Mesh3D.Extrude(CardOutline(), CardDepth / 2);
        return new Model3D()
            .Part("front", half, Colors.White).At(0, 0, CardDepth / 4)
            .Part("back", half, Colors.White).At(0, 0, -CardDepth / 4).Turned(0, 180, 0);
    }

    static Texture3D CardFront() => Painted(1024, 646, (c, w, h) =>
    {
        using (var gradient = new SKPaint
               {
                   Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(w, h),
                       new[] { new SKColor(38, 30, 92), new SKColor(124, 108, 242), new SKColor(255, 99, 132) }, new[] { 0f, 0.62f, 1f },
                       SKShaderTileMode.Clamp)
               })
            c.DrawRect(0, 0, w, h, gradient);
        // A soft light across the card.
        using (var sheen = new SKPaint { Color = new SKColor(255, 255, 255, 26), IsAntialias = true })
            c.DrawCircle(w * 0.85f, -h * 0.2f, h * 0.9f, sheen);
        // The brand: two rings and a name.
        using (var ring = new SKPaint { Color = new SKColor(255, 255, 255, 230), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 10 })
        {
            c.DrawCircle(96, 92, 34, ring);
            c.DrawCircle(138, 92, 34, ring);
        }
        Text(c, "DRAWN REWARDS", 196, 106, 40, SKColors.White);
        Text(c, "GOLD", w - 64, 106, 34, new SKColor(255, 214, 120), SKTextAlign.Right);
        // The chip.
        using (var chip = new SKPaint
               {
                   IsAntialias = true,
                   Shader = SKShader.CreateLinearGradient(new SKPoint(80, 220), new SKPoint(210, 320),
                       new[] { new SKColor(255, 222, 140), new SKColor(214, 166, 64) }, SKShaderTileMode.Clamp)
               })
            c.DrawRoundRect(80, 220, 130, 100, 16, 16, chip);
        using (var lines = new SKPaint { Color = new SKColor(150, 110, 40, 160), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 })
        {
            c.DrawLine(80, 270, 210, 270, lines);
            c.DrawLine(145, 220, 145, 320, lines);
        }
        Text(c, "4821  0730  5512  9046", 80, 430, 58, SKColors.White);
        Text(c, "MEMBER", 80, 520, 22, new SKColor(255, 255, 255, 170));
        Text(c, "ALEX MORGAN", 80, 562, 38, SKColors.White);
        Text(c, "VALID THRU", w - 80, 520, 22, new SKColor(255, 255, 255, 170), SKTextAlign.Right);
        Text(c, "08/29", w - 80, 562, 38, SKColors.White, SKTextAlign.Right);
    });

    static Texture3D CardBack() => Painted(1024, 646, (c, w, h) =>
    {
        using (var paint = new SKPaint { Color = new SKColor(30, 26, 62) })
            c.DrawRect(0, 0, w, h, paint);
        using (var stripe = new SKPaint { Color = new SKColor(12, 12, 18) })
            c.DrawRect(0, 70, w, 120, stripe);
        using (var strip = new SKPaint { Color = new SKColor(238, 236, 246), IsAntialias = true })
            c.DrawRoundRect(64, 250, 640, 90, 8, 8, strip);
        Text(c, "Alex Morgan", 96, 312, 44, new SKColor(52, 56, 120), SKTextAlign.Left, true);
        using (var cvv = new SKPaint { Color = SKColors.White, IsAntialias = true })
            c.DrawRoundRect(730, 250, 150, 90, 8, 8, cvv);
        Text(c, "123", 805, 312, 40, new SKColor(30, 26, 62), SKTextAlign.Center);
        Text(c, "12,480 points", 64, 450, 52, SKColors.White);
        Text(c, "Show this card at checkout to collect points.", 64, 506, 28, new SKColor(255, 255, 255, 170));
        Text(c, "drawnui.net", 64, 590, 28, new SKColor(255, 214, 120));
    });

    static void Text(SKCanvas canvas, string text, float x, float y, float size, SKColor color, SKTextAlign align = SKTextAlign.Left, bool italic = false)
    {
        using var font = new SKFont(SkiaFontManager.Instance.GetFont(Theme.Title) ?? SKTypeface.Default, size) { SkewX = italic ? -0.25f : 0 };
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawText(text, x, y, align, font, paint);
    }

    SkiaControl FlipCard() => WideCard(new SkiaRow
    {
        Spacing = 24,
        Children =
        {
            Stage(new SkiaModel3D(CardModel())
                    {
                        Orbit = false,
                        Yaw = 0,
                        Pitch = 0,
                        Zoom = 0.82f,
                        Antialias = true,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                    }
                    .MaterialImage("front", CardFront())
                    .MaterialImage("back", CardBack())
                    .Assign(out _card)
                    .OnTapped3D((_, _) => Flip()))
                .WithWidth(ViewerWidth)
                .WithHeight(340)
                // Still between moves (the card shows its last render): one GPU blit.
                .WithCache(SkiaCacheType.GPU)
                // The pointer's place over the stage leans the card toward it; leaving lets it settle.
                .WithGestures((me, args, apply) =>
                {
                    if (args.Type is TouchActionResult.Pointer or TouchActionResult.Panning)
                    {
                        var at = me.GetOffsetInsideControlInPoints(new System.Drawing.PointF(apply.MappedLocation.X, apply.MappedLocation.Y), apply.ChildOffset);
                        var w = me.DrawingRect.Width / me.RenderingScale;
                        var h = me.DrawingRect.Height / me.RenderingScale;
                        if (w > 0 && h > 0)
                        {
                            _leanToX = (float)Math.Clamp((at.X / w - 0.5) * 2, -1, 1) * 16;
                            _leanToY = (float)Math.Clamp((at.Y / h - 0.5) * 2, -1, 1) * 12;
                        }
                    }
                    else if (args.Type == TouchActionResult.Up)
                        _leanToX = _leanToY = 0;
                    return null;
                })
                .OnHovered((_, on) =>
                {
                    if (!on)
                        _leanToX = _leanToY = 0;
                }),
            new SkiaStack
            {
                Spacing = 14,
                // Still beside a stage that draws every frame: blitted, drawn again only when a choice changes.
                UseCache = SkiaCacheType.Image,
                WidthRequest = WideWidth - ViewerWidth - 24,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new SkiaStack
                    {
                        Spacing = 4,
                        UseCache = SkiaCacheType.Image,
                        Children =
                        {
                            new SkiaLabel("Drawn Rewards Gold") { FontSize = 22, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                            new SkiaLabel("12,480 points · member since 2021") { FontSize = 14, TextColor = Theme.Muted },
                            new SkiaLabel("Both faces are images drawn in code with Skia and put on the card's two halves.")
                                { FontSize = 13, TextColor = Theme.Muted, Margin = new Thickness(0, 10, 0, 0) },
                        }
                    },
                    new UiButton("Show the back", Theme.Accent, 160) { AccessibilityHint = "Turns the card over" }
                        .Assign(out _flipButton)
                        .OnTapped(_ => Flip()),
                }
            },
        }
    });

    void Flip()
    {
        _flipped = !_flipped;
        _cardTurnTo = _flipped ? 180 : 0;
        _flipButton.Text = _flipped ? "Show the front" : "Show the back";
    }

    /// <summary>Each frame: the half turn eases toward its side, the lean follows the pointer.</summary>
    void CardFrame(float delta)
    {
        if (!_card.WasInLastFrame())
            return;
        var k = MathF.Min(1, delta * 10);
        _leanX += (_leanToX - _leanX) * k;
        _leanY += (_leanToY - _leanY) * k;
        // About half a second for the half turn, slowing into its side.
        _cardTurn += (_cardTurnTo - _cardTurn) * MathF.Min(1, delta * 7);
        if (MathF.Abs(_cardTurnTo - _cardTurn) < 0.05f)
            _cardTurn = _cardTurnTo;
        // Leaning toward the pointer: the card's near edge comes to it. Seen from the back, the sideways lean turns over.
        var yaw = _cardTurn + _leanX;
        var pitch = -_leanY;
        if (MathF.Abs(_card.Yaw - yaw) > 0.01f)
            _card.Yaw = yaw;
        if (MathF.Abs(_card.Pitch - pitch) > 0.01f)
            _card.Pitch = pitch;
    }
}
