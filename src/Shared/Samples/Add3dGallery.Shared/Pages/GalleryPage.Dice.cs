using System.Numerics;

namespace Add3dGallery;

/// <summary>
/// A small game: two dice made in code (a rounded cube wearing its six faces, pips drawn with Skia) that tumble, hop and
/// land on a number. Flick them sideways, tap them or press Roll.
/// </summary>
public partial class GalleryPage
{
    SkiaModel3D _diceView;
    SkiaLabel _diceResult;
    CancellationTokenSource _diceRoll;
    /// <summary>Where each die lies now and how it is turned.</summary>
    readonly Vector3[] _dieAt = { new(-0.8f, 0, 0), new(0.8f, 0, 0) };
    readonly Quaternion[] _dieTurn = { Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.35f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.26f) };
    readonly DiceThrow _throw = new(DieRounding);

    const float DiceHeight = 280;

    /// <summary>A die's faces in one image, one 256 px cell each, in the order RoundedCuboid lays an atlas.</summary>
    static readonly int[] AtlasFaces = { 3, 4, 1, 6, 2, 5 }; // +X, -X, +Y on top; -Y, +Z, -Z below

    static Texture3D DieFaces() => Painted(768, 512, (c, w, h) =>
    {
        c.Clear(new SKColor(250, 246, 236));
        for (var f = 0; f < AtlasFaces.Length; f++)
        {
            c.Save();
            c.Translate(f % 3 * 256, f / 3 * 256);
            Pips(c, AtlasFaces[f]);
            c.Restore();
        }
    });

    /// <summary>One face's pips on a 256 px square: drilled into the face, darker inside, a light lip at the lower edge.</summary>
    static void Pips(SKCanvas c, int value)
    {
        // where the pips go on a 3 x 3 grid, as on a real die
        int[] cells = value switch
        {
            1 => new[] { 4 },
            2 => new[] { 0, 8 },
            3 => new[] { 0, 4, 8 },
            4 => new[] { 0, 2, 6, 8 },
            5 => new[] { 0, 2, 4, 6, 8 },
            _ => new[] { 0, 2, 3, 5, 6, 8 },
        };
        var radius = value == 1 ? 34f : 22f;
        var pip = value == 1 ? new SKColor(196, 30, 58) : new SKColor(28, 28, 38);
        foreach (var cell in cells)
        {
            var x = 128 + (cell % 3 - 1) * 64f;
            var y = 128 + (cell / 3 - 1) * 64f;
            using (var lip = Fill(new SKColor(255, 255, 255, 200)))
                c.DrawCircle(x, y + 2.5f, radius, lip);
            using (var paint = Fill(pip))
                c.DrawCircle(x, y, radius, paint);
            using (var shade = Fill(new SKColor(0, 0, 0, 70)))
                c.DrawCircle(x, y - radius * 0.25f, radius * 0.75f, shade);
        }
    }

    const float DieRounding = 0.14f;
    static readonly Vector2 ShadowShift = new(0.12f, 0.15f);

    /// <summary>
    /// Two dice, each a rounded cube wearing its faces, and a soft shadow on the table under it (it stays on the table
    /// while the die is in the air).
    /// </summary>
    static Model3D TwoDice()
    {
        var model = new Model3D();
        for (var d = 0; d < 2; d++)
        {
            var x = d == 0 ? -0.8f : 0.8f;
            // a little toward the viewer and right: away from the key light, up and to the left
            model.Part($"shadow{d}", Mesh3D.Cuboid(1.45f, 0.001f, 1.45f), Colors.White.WithAlpha(0.99f)).At(x + ShadowShift.X, -0.505f, ShadowShift.Y)
                .Part($"die{d}", Mesh3D.RoundedCuboid(1, 1, 1, DieRounding, atlas: true), Colors.White).At(x, 0, 0);
        }
        return model;
    }

    /// <summary>A soft dark spot that fades to nothing at its edge: a die's shadow on the table.</summary>
    static Texture3D ShadowSpot() => Painted(128, 128, (c, w, h) =>
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(w / 2, h / 2), w / 2,
                new[] { new SKColor(0, 0, 0, 255), new SKColor(0, 0, 0, 215), new SKColor(0, 0, 0, 90), new SKColor(0, 0, 0, 0) },
                new[] { 0f, 0.35f, 0.7f, 1f },
                SKShaderTileMode.Clamp),
        };
        c.DrawRect(0, 0, w, h, paint);
    });

    /// <summary>A die's place and turn; its shadow under it on the table, wider and lighter as the die rises.</summary>
    void PlaceDie(int d, Vector3 at, Quaternion turn)
    {
        _diceView.SetNodeRotation($"die{d}", turn);
        _diceView.SetNodeTranslation($"die{d}", at.X, at.Y, at.Z);
        var lift = MathF.Max(0, at.Y);
        var spread = 1 + lift * 0.6f;
        _diceView.SetNodeTranslation($"shadow{d}", at.X + ShadowShift.X, -0.505f, at.Z + ShadowShift.Y);
        _diceView.SetNodeScale($"shadow{d}", spread, 1, spread);
        _diceView.SetMaterialColor($"shadow{d}", Colors.White.WithAlpha(Math.Clamp(0.99f - lift * 0.9f, 0.3f, 0.99f)));
    }

    /// <summary>The dice's table: green casino felt, lit in its middle and darker toward its sunk edges.</summary>
    static SkiaShape Felt(SkiaControl dice) => new()
    {
        CornerRadius = 14,
        UseCache = SkiaCacheType.None,
        HorizontalOptions = LayoutOptions.Fill,
        BackgroundColor = Color.FromRgb(17, 82, 50),
        FillGradient = new SkiaGradient
        {
            Type = GradientType.Circular,
            StartXRatio = 0.5f, StartYRatio = 0.45f,
            // a lamp over the table: the light falls off softly into the felt's own green, no rim of shade
            Colors = new List<Color> { Color.FromRgb(36, 132, 80), Color.FromRgb(25, 106, 64), Color.FromRgb(17, 82, 50) },
            ColorPositions = new List<double> { 0, 0.6, 1 },
        },
        BevelType = BevelType.Emboss,
        Bevel = Theme.CardBevel(),
        Children = { dice },
    };

    SkiaControl DiceCard()
    {
        var faces = DieFaces();
        var shadow = ShadowSpot();
        return WideCard("Dice roll",
            "Two dice made in code: a rounded cube wearing one image of its six faces, pips drawn in Skia. Flick them sideways, tap them or press Roll.",
            new SkiaStack
            {
                Spacing = 12,
                Children =
                {
                    Felt(new SkiaModel3D(TwoDice())
                        {
                            Orbit = false,
                            Yaw = 0,
                            Pitch = 32,
                            // room for a die turned on its corner and hopping
                            FrameMargin = 0.5f,
                            Antialias = true,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                            AccessibilityRole = Aria.RoleImg,
                            AccessibilityLabel = "Two dice",
                        }
                        .Assign(out _diceView)
                        .Adapt(me =>
                        {
                            for (var d = 0; d < 2; d++)
                            {
                                me.SetMaterialImage($"die{d}", faces);
                                // polished plastic: a soft shine
                                me.SetMaterialRoughness($"die{d}", 0.3f);
                                me.SetMaterialImage($"shadow{d}", shadow);
                            }
                            for (var d = 0; d < 2; d++)
                                PlaceDie(d, _dieAt[d], _dieTurn[d]);
                            // a flick sideways rolls them; the page keeps its vertical drags
                            me.OnGestures = (args, _) =>
                            {
                                if (args.Type == TouchActionResult.Up)
                                {
                                    var moved = args.Event.Distance.Total;
                                    if (MathF.Abs(moved.X) > 40 * me.RenderingScale && MathF.Abs(moved.X) > MathF.Abs(moved.Y))
                                        RollDice(MathF.Sign(moved.X));
                                }
                                return null;
                            };
                        })
                        .OnTapped3D((_, _) => RollDice()))
                        .WithHeight(DiceHeight),
                    new SkiaRow
                    {
                        Spacing = 16,
                        Children =
                        {
                            new Button3D("Roll", Theme.Accent, 120).OnTapped(_ => RollDice()),
                            new Copyable("Roll the dice")
                            {
                                FontSize = 18, FontFamily = Theme.Title, TextColor = Theme.Ink,
                                VerticalOptions = LayoutOptions.Center,
                            }.Assign(out _diceResult),
                        }
                    },
                }
            }, still: true);
    }

    /// <summary>
    /// Throws both dice from where they lie (toward <paramref name="direction"/>: -1 left, 1 right, 0 either). The throw
    /// is physics (<see cref="DiceThrow"/>) stepped along with each frame at its real pace, so nothing is worked out up
    /// front; the faces they end on are the result.
    /// </summary>
    internal void RollDice(int direction = 0)
    {
        _diceRoll?.Cancel();
        _diceRoll = new CancellationTokenSource();
        var cancel = _diceRoll.Token;

        var roll = _throw;
        roll.Throw(_dieAt[0], _dieTurn[0], _dieAt[1], _dieTurn[1], direction, Random.Shared);
        _diceResult.Text = "Rolling...";

        double last = 0;
        // 4 s is the longest a throw runs; the animation stops itself when the dice lie still
        _diceView.AnimateRangeAsync(t =>
        {
            var now = t * 4;
            roll.Run((float)(now - last));
            last = now;
            for (var d = 0; d < 2; d++)
            {
                _dieAt[d] = roll.Position(d);
                _dieTurn[d] = roll.Turn(d);
                PlaceDie(d, _dieAt[d], _dieTurn[d]);
            }
            if (roll.Done && !cancel.IsCancellationRequested)
                Landed(roll.Faces[0], roll.Faces[1]);
        }, 0, 1, 4000, null, 0, cancel);
    }

    void Landed(int a, int b)
    {
        _diceRoll?.Cancel(); // stops the animation: nothing moves any more
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var sum = a + b;
            _diceResult.Text = a == b ? $"Doubles! {a} + {b} = {sum}" : $"{a} + {b} = {sum}";
            _diceView.AccessibilityLabel = $"Two dice showing {a} and {b}";
        });
    }
}
