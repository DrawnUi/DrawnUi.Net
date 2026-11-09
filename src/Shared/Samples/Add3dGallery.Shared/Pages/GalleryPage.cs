using DrawnUi.Gaming;

namespace Add3dGallery;

/// <summary>
/// A gallery of tiny 3D animations inside a DrawnUI screen (the Add3d add-on), a port of DrawnUI for
/// Rust's gallery3d:
/// - made in code, flat: a coin and bouncing dots for loading, a medal for an achievement, a check for success, a heart
///   for a like, falling confetti; smooth shapes in flat colors with outlines, animated by the app;
/// - images on 3D: a product box with its label and a ball with three skins, drawn in code and switched live;
/// - from model files: an avatar, a reward chest, and a field of walking mobs drawn by one 3D layer.
/// </summary>
public partial class GalleryPage : DrawnGame
{
    /// <summary>The confetti tile and the mobs' field, in points.</summary>
    const float RainWidth = 268, RainHeight = 190, FieldWidth = 1000, FieldHeight = 280;

    static readonly int[] MobCounts = { 10, 40, 100 };

    SkiaModel3D _coin, _dots, _medal, _badge, _heart, _avatar, _chest;
    SkiaScene3D _rain, _field;
    UiButton _coinButton, _dotsButton, _medalButton, _likeButton, _chestButton;
    readonly UiButton[] _mobButtons = new UiButton[MobCounts.Length];

    public GalleryPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                // The 3D pieces draw every frame: nothing to keep in a cache.
                AutoCache = false,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = new SkiaStack
                {
                    Padding = new Thickness(24),
                    Spacing = 20,
                    Children =
                    {
                        new SkiaStack
                        {
                            Spacing = 6,
                            UseCache = SkiaCacheType.Image,
                            Children =
                            {
                                new SkiaLabel("3D, made in the app")
                                {
                                    FontSize = 30, FontFamily = Theme.Title, TextColor = Theme.Ink,
                                    HorizontalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RoleHeading,
                                },
                                new SkiaLabel("3D pieces and characters built and animated inside a DrawnUI screen.")
                                {
                                    FontSize = 15, TextColor = Theme.Muted, HorizontalOptions = LayoutOptions.Center,
                                    HorizontalTextAlignment = DrawTextAlignment.Center,
                                },
                            }
                        },
                        Theme.Section("Made in code", "Smooth shapes in real 3D, shaded by a light: a few lines of code each."),
                        new SkiaWrap
                        {
                            Spacing = 16,
                            HorizontalOptions = LayoutOptions.Center,
                            Children =
                            {
                                Tile("Loading", "A coin turning while something loads.",
                                    Shaded(Coin(), 0, 12).Assign(out _coin).Adapt(me => me.AutoRotate = 200),
                                    new UiButton("Pause", Theme.Accent).Assign(out _coinButton).OnTapped(_ => ToggleCoin())),
                                Tile("Loading dots", "Three dots that bounce one after another.",
                                    Shaded(Dots(), 0, 28).Assign(out _dots),
                                    new UiButton("Pause", Theme.Accent).Assign(out _dotsButton).OnTapped(_ => ToggleDots())),
                                Tile("Achievement", "A medal that lights up and spins when unlocked.",
                                    Shaded(Medal(), MedalYaw, 12).Assign(out _medal).OnTapped3D((_, _) => SpinMedal()),
                                    new UiButton("Lock", Theme.Accent).Assign(out _medalButton).OnTapped(_ => ToggleMedal())),
                                Tile("Success", "A badge that pops in, then its check grows.",
                                    Shaded(Badge(), 22, 14).Assign(out _badge),
                                    new UiButton("Done", Theme.Accent).OnTapped(_ => Done())),
                                Tile("Like", "A heart that beats, and jumps when liked.",
                                    Shaded(HeartModel(), -24, 12).Assign(out _heart).OnTapped3D((_, _) => ToggleLike()),
                                    new UiButton("Unlike", Theme.Accent).Assign(out _likeButton).OnTapped(_ => ToggleLike())),
                                Tile("Celebration", "Confetti and stars falling: one 3D layer, many pieces.",
                                    new SkiaScene3D
                                        {
                                            Tilt = 20,
                                            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                        }
                                        .Model("paper", Paper())
                                        .Model("star", ConfettiStar())
                                        .Assign(out _rain),
                                    new UiButton("Hooray", Theme.Accent).OnTapped(_ => Hooray())),
                            }
                        },
                        Theme.Section("Images on 3D", "Any image on a 3D surface, switched live: drawn in code here, or a photo or a file."),
                        ImagesSection(),
                        Theme.Section("From model files", "Free 3D models with their own animations, driven by the app."),
                        new SkiaWrap
                        {
                            Spacing = 16,
                            HorizontalOptions = LayoutOptions.Center,
                            Children =
                            {
                                Tile("Avatar", "A character that idles, and waves when tapped.",
                                    new SkiaModel3D(Theme.Avatar)
                                        {
                                            Animation = "CharacterArmature|Idle", Yaw = 25, Pitch = 10,
                                            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                        }
                                        .Assign(out _avatar)
                                        .OnTapped3D((me, _) => me.PlayOnce("CharacterArmature|Wave")),
                                    new UiButton("Wave", Theme.Accent).OnTapped(_ => _avatar.PlayOnce("CharacterArmature|Wave"))),
                                Tile("Reward", "A chest that opens and closes.",
                                    new SkiaModel3D(Theme.Chest)
                                        {
                                            Animation = "Chest_Open", IsPlaying = false, Yaw = 30, Pitch = 20, Orbit = false,
                                            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                        }
                                        .Assign(out _chest),
                                    new UiButton("Open", Theme.Accent).Assign(out _chestButton).OnTapped(_ => ToggleChest())),
                            }
                        },
                        MobsCard(),
                    }
                }
            }
        };
        _mobs = 40;
        UpdateMobButtons();
    }

    /// <summary>A card: title, caption, the 3D piece on its stage, the button that drives it.</summary>
    static SkiaShape Tile(string title, string caption, SkiaControl piece, SkiaControl button) => new()
    {
        WidthRequest = 300,
        // Holds a 3D piece that draws every frame: a recorded cache would be made again each frame.
        UseCache = SkiaCacheType.None,
        CornerRadius = 16,
        BackgroundColor = Theme.Card,
        StrokeColor = Theme.Line,
        StrokeWidth = 1,
        Padding = new Thickness(16),
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                Children =
                {
                    new SkiaStack
                    {
                        Spacing = 4,
                        UseCache = SkiaCacheType.Image,
                        Children =
                        {
                            new SkiaLabel(title) { FontSize = 18, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                            new SkiaLabel(caption) { FontSize = 13, TextColor = Theme.Muted, HeightRequest = 36 },
                        }
                    },
                    new SkiaShape
                    {
                        CornerRadius = 12,
                        UseCache = SkiaCacheType.None,
                        BackgroundColor = Theme.Stage,
                        HorizontalOptions = LayoutOptions.Fill,
                        HeightRequest = RainHeight,
                        Children = { piece },
                    },
                    button.WithHorizontalOptions(LayoutOptions.Center),
                }
            }
        }
    };

    /// <summary>The medal's resting turn: it spins from here and lands here.</summary>
    const float MedalYaw = 20;

    /// <summary>
    /// A piece made in code in real 3D (the default Lit look: shaded by a light with a sky ambient, in perspective),
    /// turned so its depth shows; still unless the app moves it.
    /// </summary>
    static SkiaModel3D Shaded(Model3D model, float yaw, float pitch) => new(model)
    {
        Orbit = false,
        Yaw = yaw,
        Pitch = pitch,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
    };

    SkiaControl MobsCard() => new SkiaShape
    {
        UseCache = SkiaCacheType.None,
        CornerRadius = 16,
        BackgroundColor = Theme.Card,
        StrokeColor = Theme.Line,
        StrokeWidth = 1,
        Padding = new Thickness(16),
        HorizontalOptions = LayoutOptions.Center,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                HorizontalOptions = LayoutOptions.Start,
                WidthRequest = FieldWidth,
                Children =
                {
                    new SkiaStack
                    {
                        Spacing = 4,
                        UseCache = SkiaCacheType.Image,
                        Children =
                        {
                            new SkiaLabel("Mobs on a 2D field") { FontSize = 18, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                            new SkiaLabel("Many 3D monsters from one model, moved like sprites and drawn by one layer in one pass. Tap one to hit it.")
                                { FontSize = 13, TextColor = Theme.Muted },
                        }
                    },
                    new SkiaShape
                    {
                        WidthRequest = FieldWidth,
                        HeightRequest = FieldHeight,
                        UseCache = SkiaCacheType.None,
                        CornerRadius = 10,
                        BackgroundColor = Color.FromRgb(58, 98, 74),
                        Children =
                        {
                            new SkiaScene3D
                                {
                                    Tilt = 30,
                                    HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                }
                                .Model("slime", Theme.Mob)
                                .Assign(out _field)
                                .OnTapped3D((me, hit) => me.PlayOnce(hit.Object, "CharacterArmature|HitRecieve")),
                        }
                    },
                    new SkiaRow
                    {
                        Spacing = 8,
                        AccessibilityRole = Aria.RoleRadioGroup,
                        AccessibilityLabel = "Mobs on the field",
                        Children = MobCounts.Select((n, i) => (SkiaControl)new UiButton($"{n} mobs", Theme.Quiet, 110) { AccessibilityRole = Aria.RoleRadio }
                            .Assign(out _mobButtons[i])
                            .OnTapped(_ => SetMobs(n))).ToList(),
                    },
                }
            }
        }
    };

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();
        StartLoop();
    }

    void ToggleCoin()
    {
        _coinPaused = !_coinPaused;
        _coin.AutoRotate = _coinPaused ? 0 : 200;
        _coinButton.Text = _coinPaused ? "Spin" : "Pause";
    }

    void ToggleDots()
    {
        _resting = !_resting;
        _dotsButton.Text = _resting ? "Bounce" : "Pause";
    }

    void SpinMedal() => _spin = 0;

    void MedalColors()
    {
        _medal.SetMaterialColor("disc", _unlocked ? null : Grey);
        _medal.SetMaterialColor("inner", _unlocked ? null : Color.FromRgb(222, 219, 230));
        _medal.SetMaterialColor("star", _unlocked ? null : Color.FromRgb(244, 242, 248));
    }

    void ToggleMedal()
    {
        _unlocked = !_unlocked;
        _medalButton.Text = _unlocked ? "Lock" : "Unlock";
        MedalColors();
        if (_unlocked)
        {
            _spin = 0;
            _medal.Scale = 0.6;
            _ = _medal.ScaleToAsync(1, 1, 500, Easing.SpringOut);
        }
    }

    void Done()
    {
        _grow = 0;
        _badge.SetNodeScale("check", 0, 0, 0);
        _badge.Scale = 0.3;
        _ = _badge.ScaleToAsync(1, 1, 450, Easing.SpringOut);
    }

    void ToggleLike()
    {
        _liked = !_liked;
        _pop = _liked ? 1 : 0;
        _heart.SetMaterialColor("heart", _liked ? null : Grey);
        _likeButton.Text = _liked ? "Unlike" : "Like";
    }

    void Hooray()
    {
        // A fresh shower: every piece starts again above the tile.
        for (var i = 0; i < _pieces.Length; i++)
            _pieces[i].Y = -12 - _dice.Next() * RainHeight;
    }

    void ToggleChest()
    {
        _open = !_open;
        _chest.Hold(_open ? "Chest_Open" : "Chest_Close");
        _chestButton.Text = _open ? "Close" : "Open";
    }

    internal void SetMobs(int n)
    {
        _mobs = n;
        UpdateMobButtons();
    }

    void UpdateMobButtons()
    {
        for (var i = 0; i < MobCounts.Length; i++)
        {
            var on = MobCounts[i] == _mobs;
            _mobButtons[i].Fill = on ? Theme.Accent : Theme.Quiet;
            _mobButtons[i].AccessibilityIsPressed = on;
        }
    }
}
