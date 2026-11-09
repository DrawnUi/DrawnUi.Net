namespace Add3dGallery;

/// <summary>
/// A 3D boss in a DrawnUI screen (port of DrawnUI for Rust's model3d): a glTF model with its skeleton animations,
/// drawn with the same GPU context as the UI around it. The app drives the scene: moves played once and back to idle,
/// a tap on the boss that hits it, its skin and eyes recolored by material name. Drag the dragon to turn it.
/// </summary>
public class BossPage : SkiaLayer
{
    const string Idle = "CharacterArmature|Flying_Idle";
    const string Hit = "CharacterArmature|HitReact";

    /// <summary>How a move plays.</summary>
    enum Kind
    {
        /// <summary>Loops until another move.</summary>
        Loop,
        /// <summary>Plays once, then back to the loop.</summary>
        Once,
        /// <summary>Plays once and stays on its last frame.</summary>
        Hold
    }

    /// <summary>The boss's moves: the caption, the glTF animation and how it plays, in the order of a fight.</summary>
    static readonly (string Caption, string Animation, Kind Kind)[] Moves =
    {
        ("Idle", Idle, Kind.Loop),
        ("Fly", "CharacterArmature|Fast_Flying", Kind.Loop),
        ("Punch", "CharacterArmature|Punch", Kind.Once),
        ("Headbutt", "CharacterArmature|Headbutt", Kind.Once),
        ("Yes", "CharacterArmature|Yes", Kind.Once),
        ("No", "CharacterArmature|No", Kind.Once),
        ("Death", "CharacterArmature|Death", Kind.Hold),
    };

    /// <summary>Skin colors for the dragon's main material; null = the file's own.</summary>
    static readonly (string Caption, Color Color)[] Skins =
    {
        ("Ember", null),
        ("Frost", Color.FromRgb(70, 150, 230)),
        ("Venom", Color.FromRgb(90, 190, 80)),
        ("Shadow", Color.FromRgb(70, 60, 90)),
    };

    static readonly Color Ember = Color.FromRgb(190, 70, 30);
    static readonly Color ButtonColor = Color.FromRgb(44, 40, 52);
    static readonly Color MovesAccent = Color.FromRgb(255, 122, 69);
    static readonly Color ToggleColor = Color.FromRgb(60, 54, 72);
    static readonly Color RageColor = Color.FromRgb(200, 30, 40);
    static readonly Color Muted = Color.FromRgb(190, 180, 200);

    readonly UiButton[] _moveButtons = new UiButton[Moves.Length];
    readonly UiButton[] _skinButtons = new UiButton[Skins.Length];
    SkiaModel3D _boss;
    SkiaLabel _news;
    UiButton _rageButton, _turnButton;
    string _looping = "Idle";
    string _skin = "Ember";
    bool _rage, _turning;

    public BossPage()
    {
        VerticalOptions = LayoutOptions.Fill;
        Children = new List<SkiaControl>
        {
            // The arena: a warm light behind the boss.
            new SkiaShape
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                BackgroundColor = Color.FromRgb(18, 14, 22),
                UseCache = SkiaCacheType.Image,
                FillGradient = new SkiaGradient
                {
                    Type = GradientType.Circular,
                    StartXRatio = 0.5f,
                    StartYRatio = 0.42f,
                    Colors = new List<Color> { Color.FromRgb(92, 38, 30), Color.FromRgb(18, 14, 22) },
                },
            },
            new SkiaGrid
                {
                    Padding = new Thickness(20),
                    RowSpacing = 10,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaStack
                        {
                            Spacing = 8,
                            UseCache = SkiaCacheType.Image,
                            Children =
                            {
                                new SkiaLabel("BOSS FIGHT")
                                {
                                    FontSize = 30, FontFamily = Theme.Title, TextColor = Colors.White,
                                    HorizontalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RoleHeading,
                                },
                                new SkiaLabel("A glTF model drawn in 3D inside DrawnUI, driven by the app. Drag it to turn it, tap it to hit it.")
                                {
                                    FontSize = 15, TextColor = Muted, HorizontalOptions = LayoutOptions.Center,
                                    HorizontalTextAlignment = DrawTextAlignment.Center,
                                },
                            }
                        }.SetGrid(0, 0),
                        new SkiaModel3D(Theme.Dragon)
                            {
                                Animation = Idle, Yaw = 30, Pitch = 8, Zoom = 0.75f,
                                HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                            }
                            .Assign(out _boss)
                            .OnTapped3D((me, hit) =>
                            {
                                // A hit plays once and goes back to the loop; a dead dragon does not react.
                                if (_looping != "Death")
                                    me.PlayOnce(Hit);
                                _news.Text = $"Hit on {hit.Material.Replace('_', ' ')}";
                            })
                            .OnAnimationEnded((_, name) => _news.Text = $"{CaptionOf(name)} finished")
                            .SetGrid(0, 1),
                        new SkiaLabel("Tap the dragon to hit it.")
                            {
                                FontSize = 15, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center,
                            }
                            .WithAccessibilityLive()
                            .Assign(out _news)
                            .SetGrid(0, 2),
                        new SkiaStack
                        {
                            Spacing = 10,
                            UseCache = SkiaCacheType.Image,
                            Children =
                            {
                                Caption("MOVES"),
                                new SkiaWrap
                                {
                                    Spacing = 8,
                                    HorizontalOptions = LayoutOptions.Center,
                                    AccessibilityRole = Aria.RoleToolbar,
                                    AccessibilityLabel = "Moves",
                                    Children = Moves.Select((m, i) => (SkiaControl)new UiButton(m.Caption, ButtonColor, 100)
                                        .Assign(out _moveButtons[i])
                                        .OnTapped(_ => Move(m.Caption, m.Animation, m.Kind))).ToList(),
                                },
                                Caption("SKIN AND EYES (materials by name)"),
                                new SkiaWrap
                                {
                                    Spacing = 8,
                                    HorizontalOptions = LayoutOptions.Center,
                                    AccessibilityRole = Aria.RoleToolbar,
                                    AccessibilityLabel = "Skin and eyes",
                                    Children = Skins.Select((s, i) => (SkiaControl)new UiButton(s.Caption, ButtonColor, 100)
                                            {
                                                StrokeColor = s.Color ?? Ember, StrokeWidth = 2,
                                                AccessibilityRole = Aria.RoleRadio,
                                            }
                                            .Assign(out _skinButtons[i])
                                            .OnTapped(_ => Skin(s.Caption, s.Color)))
                                        .Append(new UiButton("Rage: off", ToggleColor, 124).Assign(out _rageButton).OnTapped(_ => ToggleRage()))
                                        .Append(new UiButton("Turn: off", ToggleColor, 124).Assign(out _turnButton).OnTapped(_ => ToggleTurn()))
                                        .ToList(),
                                },
                            }
                        }.SetGrid(0, 3),
                    }
                }
                .WithRowDefinitions("Auto,*,Auto,Auto"),
        };
        UpdateButtons();
    }

    static SkiaLabel Caption(string text) => new(text)
    {
        FontSize = 13, TextColor = Muted, HorizontalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RoleHeading,
    };

    static string CaptionOf(string animation)
    {
        foreach (var m in Moves)
            if (m.Animation == animation)
                return m.Caption;
        return animation == Hit ? "Hit" : "Move";
    }

    void Move(string caption, string animation, Kind kind)
    {
        switch (kind)
        {
            case Kind.Loop:
                _boss.Play(animation);
                _looping = caption;
                break;
            case Kind.Once:
                _boss.PlayOnce(animation);
                break;
            case Kind.Hold:
                _boss.Hold(animation);
                _looping = caption;
                break;
        }
        _news.Text = $"{caption}…";
        UpdateButtons();
    }

    void Skin(string caption, Color color)
    {
        _boss.SetMaterialColor("Dragon_Main", color);
        _skin = caption;
        UpdateButtons();
    }

    void ToggleRage()
    {
        _rage = !_rage;
        _boss.SetMaterialColor("Eye_White", _rage ? Color.FromRgb(255, 40, 30) : null);
        UpdateButtons();
    }

    void ToggleTurn()
    {
        _turning = !_turning;
        _boss.AutoRotate = _turning ? 30 : 0;
        UpdateButtons();
    }

    void UpdateButtons()
    {
        for (var i = 0; i < Moves.Length; i++)
            _moveButtons[i].Fill = _looping == Moves[i].Caption ? MovesAccent : ButtonColor;
        for (var i = 0; i < Skins.Length; i++)
        {
            var on = _skin == Skins[i].Caption;
            _skinButtons[i].Fill = on ? Skins[i].Color ?? Ember : ButtonColor;
            _skinButtons[i].AccessibilityIsPressed = on;
        }
        _rageButton.Text = _rage ? "Rage: on" : "Rage: off";
        _rageButton.Fill = _rage ? RageColor : ToggleColor;
        _rageButton.AccessibilityIsPressed = _rage;
        _turnButton.Text = _turning ? "Turn: on" : "Turn: off";
        _turnButton.AccessibilityIsPressed = _turning;
    }
}
