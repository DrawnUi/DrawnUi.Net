namespace Add3dGallery;

/// <summary>
/// Card flip: a loyalty card in 3D, its front and back drawn in code with Skia. It leans toward the pointer (or the
/// finger) and turns over with a smooth half turn on a tap or the Flip button.
/// </summary>
public partial class GalleryPage
{
    /// <summary>The card in model units: a bank card's proportions (85.6 x 54 mm), its face 1024 x 646 path units.</summary>
    const float CardScale = 8.56f / 1024;

    SkiaModel3D _card;
    UiButton _flipButton;
    bool _flipped;
    /// <summary>The half turn: where it is and where it goes (0 = front, 180 = back), degrees.</summary>
    float _cardTurn, _cardTurnTo;

    static SKPath CardOutline()
    {
        var path = new SKPath();
        path.AddRoundRect(new SKRoundRect(SKRect.Create(1024, 646), 46));
        return path;
    }

    /// <summary>
    /// The card: a front half and a back half, each a rounded slab with its own image. The back half is turned round,
    /// so its image reads right from behind.
    /// </summary>
    static Model3D CardModel()
    {
        var half = Mesh3D.Extrude(CardOutline(), 4);
        return new Model3D()
            .Part("front", half, Colors.White).Scaled(CardScale, CardScale, CardScale).At(0, 0, 2 * CardScale)
            .Part("back", half, Colors.White).Scaled(CardScale, CardScale, CardScale).Turned(0, 180, 0).At(0, 0, -2 * CardScale);
    }

    static SKPaint Linear(float x0, float y0, float x1, float y1, SKColor a, SKColor b) => new()
    {
        IsAntialias = true,
        Shader = SKShader.CreateLinearGradient(new SKPoint(x0, y0), new SKPoint(x1, y1), new[] { a, b }, SKShaderTileMode.Clamp),
    };

    static Texture3D CardFront() => Painted(1024, 646, (c, w, h) =>
    {
        using (var gradient = new SKPaint
               {
                   Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(w, h),
                       new[] { new SKColor(0x26, 0x1E, 0x5C), new SKColor(0x7C, 0x6C, 0xF2), new SKColor(0xFF, 0x63, 0x84) }, SKShaderTileMode.Clamp)
               })
            c.DrawRect(0, 0, w, h, gradient);
        // A soft white glow at the top right.
        using (var glow = new SKPaint
               {
                   Shader = SKShader.CreateRadialGradient(new SKPoint(w * 0.84f, h * 0.1f), 330,
                       new[] { new SKColor(255, 255, 255, 90), new SKColor(255, 255, 255, 0) }, SKShaderTileMode.Clamp)
               })
            c.DrawRect(0, 0, w, h, glow);
        // Two white rings and the name, top left; the tier, top right.
        using (var ring = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 5 })
        {
            c.DrawCircle(84, 92, 24, ring);
            c.DrawCircle(114, 92, 24, ring);
        }
        Text(c, "DRAWN REWARDS", 158, 106, 38, SKColors.White);
        Text(c, "GOLD", w - 64, 108, 44, new SKColor(0xFF, 0xD6, 0x78), SKTextAlign.Right);
        // The chip.
        using (var chip = Linear(64, 196, 200, 300, new SKColor(255, 222, 122), new SKColor(255, 196, 61)))
            c.DrawRoundRect(64, 196, 136, 104, 18, 18, chip);
        using (var lines = new SKPaint { Color = new SKColor(120, 80, 0, 130), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 })
        {
            c.DrawRoundRect(98, 220, 68, 56, 10, 10, lines);
            c.DrawLine(64, 248, 98, 248, lines);
            c.DrawLine(166, 248, 200, 248, lines);
        }
        Text(c, "4821  0730  5512  9046", 64, 420, 60, SKColors.White);
        var dim = new SKColor(255, 255, 255, 190);
        Text(c, "MEMBER", 64, 520, 24, dim);
        Text(c, "ALEX MORGAN", 64, 568, 40, SKColors.White);
        Text(c, "VALID THRU", 700, 520, 24, dim);
        Text(c, "08/29", 700, 568, 40, SKColors.White);
    });

    static Texture3D CardBack() => Painted(1024, 646, (c, w, h) =>
    {
        using (var paint = Fill(new SKColor(0x1E, 0x1A, 0x3E)))
            c.DrawRect(0, 0, w, h, paint);
        // The magnetic stripe.
        using (var stripe = Fill(new SKColor(10, 10, 14)))
            c.DrawRect(0, 60, w, 112, stripe);
        // The signature strip and the CVV.
        using (var strip = Fill(new SKColor(232, 228, 240)))
            c.DrawRoundRect(64, 226, 620, 84, 8, 8, strip);
        Text(c, "Alex Morgan", 92, 282, 40, new SKColor(52, 48, 84), SKTextAlign.Left, true);
        using (var cvv = Fill(SKColors.White))
            c.DrawRoundRect(708, 226, 140, 84, 8, 8, cvv);
        Text(c, "123", 778, 282, 40, new SKColor(30, 28, 52), SKTextAlign.Center);
        Text(c, "12,480 points", 64, 420, 52, SKColors.White);
        Text(c, "Show this card at checkout to collect points.", 64, 478, 30, new SKColor(255, 255, 255, 200));
        Text(c, "drawnui.net", 64, h - 64, 34, new SKColor(0xFF, 0xD6, 0x78));
    });

    /// <summary>Text on a card face, its baseline at <paramref name="y"/>: subpixel-positioned and antialiased, so it stays even on the turning card.</summary>
    static void Text(SKCanvas canvas, string text, float x, float y, float size, SKColor color, SKTextAlign align = SKTextAlign.Left, bool italic = false)
    {
        using var font = new SKFont(SkiaFontManager.Instance.GetFont("FontText") ?? SKTypeface.Default, size)
        {
            SkewX = italic ? -0.22f : 0, Subpixel = true, Edging = SKFontEdging.Antialias,
        };
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawText(text, x, y, align, font, paint);
    }

    SkiaControl FlipCard() => WideCard("Card flip", "A loyalty card in 3D: it leans toward the pointer and turns over to show its back.", new SkiaRow
    {
        Spacing = 24,
        Children =
        {
            Stage(new SkiaModel3D(CardModel())
                    {
                        Orbit = false,
                        Yaw = 0,
                        Pitch = 0,
                        Zoom = 0.74f,
                        // Leans toward the mouse or the finger, up to 14 degrees at the stage's edge.
                        TiltToPointer = 14,
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
                .WithCache(SkiaCacheType.GPU),
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
                            new SkiaLabel("Drawn Rewards Gold") { FontSize = 24, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
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

    /// <summary>Each frame: the half turn eases toward its side (the view's home yaw: the lean toward the pointer stays on top).</summary>
    void CardFrame(float delta)
    {
        if (_cardTurn == _cardTurnTo || !_card.WasInLastFrame())
            return;
        // About half a second for the half turn, slowing into its side.
        _cardTurn += (_cardTurnTo - _cardTurn) * MathF.Min(1, delta * 7);
        if (MathF.Abs(_cardTurnTo - _cardTurn) < 0.05f)
            _cardTurn = _cardTurnTo;
        _card.Yaw = _cardTurn;
    }
}
