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
    /// <summary>Each die's turn now (x, z) and the turn of its holder around the vertical (yaw).</summary>
    readonly (float X, float Z, float Yaw)[] _diePose = { (0, 0, 20), (0, 0, -15) };

    const float DiceHeight = 280;

    /// <summary>The turn (x, z) that puts a face on top: 1 starts on top, 6 under it, 2 in front, 5 behind, 3 right, 4 left.</summary>
    static readonly (float X, float Z)[] FaceUp = { (0, 0), (-90, 0), (0, 90), (0, -90), (90, 0), (180, 0) };

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
    static readonly float[] DieX = { -0.8f, 0.8f };
    static readonly Vector2 ShadowShift = new(0.12f, 0.15f);

    /// <summary>
    /// Two dice, each a holder that turns it around the vertical and lifts it, a rounded cube wearing its faces, and a
    /// soft shadow on the table under it (it stays on the table while the die hops).
    /// </summary>
    static Model3D TwoDice()
    {
        var model = new Model3D();
        for (var d = 0; d < 2; d++)
            // a little toward the viewer and right: away from the key light, up and to the left
            model.Part($"shadow{d}", Mesh3D.Cuboid(1.45f, 0.001f, 1.45f), Colors.White.WithAlpha(0.99f)).At(DieX[d] + ShadowShift.X, -0.505f, ShadowShift.Y)
                .Part($"holder{d}", Mesh3D.Cuboid(0.01f, 0.01f, 0.01f), Colors.White).At(DieX[d], 0, 0)
                .Part($"die{d}", Mesh3D.RoundedCuboid(1, 1, 1, DieRounding, atlas: true), Colors.White).Inside($"holder{d}");
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

    /// <summary>How high a die's center must be for its lowest point to touch the table, turned by (x, z).</summary>
    static float RestingLift(float x, float z)
    {
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitX, x * MathF.PI / 180) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z * MathF.PI / 180);
        var core = 0.5f - DieRounding;
        var lowest = 0f;
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -core : core, (i & 2) == 0 ? -core : core, (i & 4) == 0 ? -core : core);
            lowest = MathF.Min(lowest, Vector3.Transform(corner, turn).Y);
        }
        return -lowest + DieRounding - 0.5f;
    }

    /// <summary>A die's place: turned, lifted to rest on the table plus its hop, its shadow wider and lighter as it rises.</summary>
    void PlaceDie(int d, (float X, float Z, float Yaw) pose, float hop)
    {
        var lift = RestingLift(pose.X, pose.Z) + hop;
        _diceView.SetNodeRotation($"die{d}", pose.X, 0, pose.Z);
        _diceView.SetNodeRotation($"holder{d}", 0, pose.Yaw, 0);
        _diceView.SetNodeTranslation($"holder{d}", DieX[d], lift, 0);
        var spread = 1 + lift * 0.6f;
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
                                me.SetMaterialVisible($"holder{d}", false);
                            }
                            for (var d = 0; d < 2; d++)
                                PlaceDie(d, _diePose[d], 0);
                            // a flick sideways rolls them; the page keeps its vertical drags
                            me.OnGestures = (args, _) =>
                            {
                                if (args.Type == TouchActionResult.Up)
                                {
                                    var moved = args.Event.Distance.Total;
                                    if (MathF.Abs(moved.X) > 40 * me.RenderingScale && MathF.Abs(moved.X) > MathF.Abs(moved.Y))
                                        RollDice();
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
                            new SkiaLabel("Roll the dice")
                            {
                                FontSize = 18, FontFamily = Theme.Title, TextColor = Theme.Ink,
                                VerticalOptions = LayoutOptions.Center,
                            }.Assign(out _diceResult),
                        }
                    },
                }
            }, still: true);
    }

    const double RollMs = 1700;
    /// <summary>The part of a roll spent tumbling; the rest is the die settling on the face it landed on, on a spring.</summary>
    const float Tumbling = 0.8f;

    /// <summary>
    /// A die's angle during a roll: it keeps turning fast, loses speed to friction but never crawls (its last turn is
    /// still at 40% of its average speed), hits its face and settles on it like a damped spring: it rocks back off
    /// the face and in again, each swing smaller, and stops.
    /// </summary>
    static float Tumble(float from, float to, float t)
    {
        var d = to - from;
        if (t < Tumbling)
        {
            var u = t / Tumbling;
            return from + d * u * (1.6f - 0.6f * u);
        }
        // a damped spring started by the impact: x(s) = A e^(-zeta w s) sin(w_d s), back off the face first
        var s = (t - Tumbling) / (1 - Tumbling);
        const float zeta = 0.28f, cycles = 2.2f;
        var wd = 2 * MathF.PI * cycles;
        var w = wd / MathF.Sqrt(1 - zeta * zeta);
        var amplitude = MathF.Min(MathF.Abs(d), 7);
        return to - MathF.Sign(d) * amplitude * MathF.Exp(-zeta * w * s) * MathF.Sin(wd * s);
    }

    /// <summary>Hops off the table: three falls under gravity, each lower and shorter, done before the die lands.</summary>
    static float Bounces(float t)
    {
        ReadOnlySpan<float> heights = stackalloc float[] { 0.32f, 0.12f, 0.04f };
        // time of a hop grows with the square root of its height, as for a falling body
        float total = 0;
        foreach (var h in heights)
            total += MathF.Sqrt(h);
        var at = t / (Tumbling * 0.95f) * total;
        foreach (var h in heights)
        {
            var span = MathF.Sqrt(h);
            if (at < span)
            {
                var u = at / span;
                return 4 * h * u * (1 - u);
            }
            at -= span;
        }
        return 0;
    }

    /// <summary>Picks both results, then tumbles each die into its face-up turn with a few hops, from where it stands.</summary>
    internal void RollDice()
    {
        _diceRoll?.Cancel();
        _diceRoll = new CancellationTokenSource();
        var cancel = _diceRoll.Token;

        var values = new[] { Random.Shared.Next(1, 7), Random.Shared.Next(1, 7) };
        var from = ((float X, float Z, float Yaw)[])_diePose.Clone();
        var to = new (float X, float Z, float Yaw)[2];
        for (var d = 0; d < 2; d++)
        {
            var up = FaceUp[values[d] - 1];
            // two or three whole turns on top of the landing turn, alternately forward and back
            var spins = 360 * Random.Shared.Next(2, 4) * (d == 0 ? 1 : -1);
            to[d] = (up.X + spins, up.Z + 360 * Random.Shared.Next(1, 3), from[d].Yaw + Random.Shared.Next(-60, 61));
        }
        _diceResult.Text = "Rolling...";

        _diceView.AnimateRangeAsync(t =>
        {
            var hop = Bounces((float)t);
            for (var d = 0; d < 2; d++)
            {
                var p = (Tumble(from[d].X, to[d].X, (float)t), Tumble(from[d].Z, to[d].Z, (float)t),
                    Tumble(from[d].Yaw, to[d].Yaw, (float)t));
                _diePose[d] = p;
                PlaceDie(d, p, hop);
            }
        }, 0, 1, RollMs, null, 0, cancel).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (cancel.IsCancellationRequested)
                return;
            for (var d = 0; d < 2; d++)
                _diePose[d] = (to[d].X % 360, to[d].Z % 360, to[d].Yaw % 360);
            var sum = values[0] + values[1];
            _diceResult.Text = values[0] == values[1] ? $"Doubles! {values[0]} + {values[1]} = {sum}" : $"{values[0]} + {values[1]} = {sum}";
            _diceView.AccessibilityLabel = $"Two dice showing {values[0]} and {values[1]}";
        }));
    }
}
