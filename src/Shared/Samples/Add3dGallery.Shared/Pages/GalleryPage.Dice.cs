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

    /// <summary>Two dice, each a holder that turns it around the vertical and hops, and a rounded cube wearing its faces.</summary>
    static Model3D TwoDice()
    {
        var model = new Model3D();
        for (var d = 0; d < 2; d++)
            model.Part($"holder{d}", Mesh3D.Cuboid(0.01f, 0.01f, 0.01f), Colors.White).At(d == 0 ? -0.8f : 0.8f, 0, 0)
                .Part($"die{d}", Mesh3D.RoundedCuboid(1, 1, 1, 0.14f, atlas: true), Colors.White).Inside($"holder{d}");
        return model;
    }

    SkiaControl DiceCard()
    {
        var faces = DieFaces();
        return WideCard("Dice roll",
            "Two dice made in code: a rounded cube wearing one image of its six faces, pips drawn in Skia. Flick them sideways, tap them or press Roll.",
            new SkiaStack
            {
                Spacing = 12,
                Children =
                {
                    Stage(new SkiaModel3D(TwoDice())
                        {
                            Orbit = false,
                            Yaw = 0,
                            Pitch = 32,
                            Zoom = 0.85f,
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
                                me.SetMaterialVisible($"holder{d}", false);
                                me.SetNodeRotation($"holder{d}", 0, _diePose[d].Yaw, 0);
                            }
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
            var ease = 1 - MathF.Pow(1 - (float)t, 3);
            // hops that die down as it settles
            var hop = 0.45f * MathF.Abs(MathF.Sin((float)t * MathF.PI * 3)) * MathF.Pow(1 - (float)t, 1.5f);
            for (var d = 0; d < 2; d++)
            {
                var p = (from[d].X + (to[d].X - from[d].X) * ease, from[d].Z + (to[d].Z - from[d].Z) * ease,
                    from[d].Yaw + (to[d].Yaw - from[d].Yaw) * ease);
                _diePose[d] = p;
                _diceView.SetNodeRotation($"die{d}", p.Item1, 0, p.Item2);
                _diceView.SetNodeRotation($"holder{d}", 0, p.Item3, 0);
                _diceView.SetNodeTranslation($"holder{d}", d == 0 ? -0.8f : 0.8f, hop, 0);
            }
        }, 0, 1, 1400, null, 0, cancel).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
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
