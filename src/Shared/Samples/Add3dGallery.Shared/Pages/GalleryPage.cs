using DrawnUi.Gaming;

namespace Add3dGallery;

/// <summary>
/// Where 3D belongs in an app, inside a DrawnUI screen (the Add3d add-on), the same gallery as DrawnUI for Rust's
/// gallery3d. The reasons an app adds 3D come first, the fun after, games last:
/// - a product viewer (a model from the app or from a link, turn, zoom, hotspots, colors);
/// - 3D thumbnails in a scrolling list;
/// - a card that flips;
/// - a globe with pins;
/// - a 3D mascot beside a plain sign-up form;
/// - micro-interactions (like, celebration, achievement), an image on a 3D box, models with their own animations,
///   and a field of walking mobs drawn by one 3D layer.
/// </summary>
public partial class GalleryPage : DrawnGame
{
    /// <summary>The confetti tile and the mobs' field, in points.</summary>
    const float RainWidth = 268, RainHeight = 190, FieldWidth = 1000, FieldHeight = 280;

    /// <summary>The inner width of the wide cards (the first five sections and the mobs).</summary>
    const float WideWidth = 1000;

    static readonly int[] MobCounts = { 10, 40, 100 };

    SkiaScroll _scroll;
    SkiaModel3D _chest;
    SkiaScene3D _rain, _field;
    Scenery _scenery;
    Button3D _chestButton;
    readonly Button3D[] _mobButtons = new Button3D[MobCounts.Length];
    Switch3D _drawnSwitch;
    SkiaControl _keyboardSpacer;

    /// <summary>
    /// The on-screen keyboard's height in points (0 = hidden) and the focused control: the page scrolls that control
    /// above the keyboard (SkiaScroll.AdaptToKeyboardFor / AdaptToKeyboardSize), the spacer lets the last fields go
    /// that high. Called by the MAUI head from DrawnUiBasePage.KeyboardSize and when the focus moves.
    /// </summary>
    public void OnKeyboard(double height, SkiaControl focused)
    {
        _keyboardSpacer.HeightRequest = height;
        _keyboardSpacer.IsVisible = height > 0;
        _scroll.AdaptToKeyboardFor = height > 0 ? focused : null;
        _scroll.AdaptToKeyboardSize = height;
    }
    bool _drawnMobs;

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
                // an overlay bar, as on phones and the Mac: it shows while the page moves and fades out after; it can be
                // dragged; not held on hover (the mouse is always over the page, it would never hide)
                ScrollBar = new SkiaScrollBar { IsDraggable = true, Thickness = 6 },
                ShowScrollBarsOnHover = false,
                // a focused field ends 16 pt clear of the on-screen keyboard, as in DrawnUI for React and Rust
                AdaptToKeyboardGap = 16,
                ScrollBarThumbColor = Theme.Muted,
                ScrollBarTrackColor = Color.FromRgba(255, 255, 255, 18),
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
                            // As the page's own spacing in DrawnUI for Rust's gallery, where the caption is the title's sibling.
                            Spacing = 20,
                            UseCache = SkiaCacheType.Image,
                            Children =
                            {
                                new Copyable("3D, made in the app")
                                {
                                    FontSize = 30, FontFamily = Theme.Title, TextColor = Theme.Ink,
                                    HorizontalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RoleHeading,
                                },
                                new Copyable("Where 3D earns its place in an app: products, lists, cards, maps and forms first, then the fun.")
                                {
                                    FontSize = 15, TextColor = Theme.Muted, HorizontalOptions = LayoutOptions.Center,
                                    HorizontalTextAlignment = DrawTextAlignment.Center,
                                },
                                new SkiaRow
                                {
                                    Spacing = 20,
                                    HorizontalOptions = LayoutOptions.Center,
                                    Children =
                                    {
                                        Theme.Link("Read the 3D article", Theme.ArticleUrl),
                                        Theme.Link("Source on GitHub", Theme.RepoUrl),
                                    }
                                },
                            }
                        },

                        GlobeCard().Assign(out _globeCard),

                        FlipCard(),

                        ViewerCard(),

                        ListCard(),

                        Theme.Section("Small 3D touches", "Little 3D moments that answer a tap."),
                        new SkiaWrap
                        {
                            Spacing = 16,
                            HorizontalOptions = LayoutOptions.Center,
                            Children =
                            {
                                Tile("Celebration", "Confetti and stars falling: one 3D layer, many pieces.",
                                    new SkiaScene3D
                                        {
                                            Tilt = 20,
                                            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                        }
                                        .Model("paper", Paper())
                                        .Model("star", ConfettiStar())
                                        .Assign(out _rain),
                                    new Button3D("Hooray", Theme.Accent).OnTapped(_ => Hooray())),
                                ProductBoxTile(),
                                Tile("Reward", "A chest that opens and closes.",
                                    new SkiaModel3D(Theme.Chest)
                                        {
                                            Animation = "Chest_Open", IsPlaying = false, Yaw = 30, Pitch = 20, Orbit = false,
                                            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                        }
                                        .Assign(out _chest),
                                    new Button3D("Open", Theme.Accent).Assign(out _chestButton).OnTapped(_ => ToggleChest())),
                            }
                        },

                        FormCard(),

                        Theme.Section("Games", "Many 3D characters on one field, moved like sprites, and dice made in code."),
                        MobsCard(),
                        DiceCard(),

                        Footer(),

                        // As tall as the on-screen keyboard while it shows (phones): the last fields can scroll above it.
                        new SkiaControl { HorizontalOptions = LayoutOptions.Fill, IsVisible = false }.Assign(out _keyboardSpacer),
                    }
                }
            }.Assign(out _scroll)
        };
        _mobs = 40;
        UpdateMobButtons();
    }

    /// <summary>A card: title, caption, the 3D piece on its stage, the button that drives it.</summary>
    /// <summary>The page's end: where to read more, the source, and the same demo in the other DrawnUI engines.</summary>
    static SkiaControl Footer() => new SkiaStack
    {
        Spacing = 14,
        Margin = new Thickness(0, 24, 0, 8),
        HorizontalOptions = LayoutOptions.Fill,
        UseCache = SkiaCacheType.Image,
        Children =
        {
            new SkiaShape { HeightRequest = 1, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Theme.Line },
            new SkiaWrap
            {
                Spacing = 24,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    Theme.Link("3D article", Theme.ArticleUrl),
                    Theme.Link("DrawnUI docs", Theme.DocsUrl),
                    Theme.Link("Source on GitHub", Theme.RepoUrl),
                    Theme.Link("Same demo in Rust", Theme.RustDemoUrl),
                    Theme.Link("Same demo in React", Theme.ReactDemoUrl),
                }
            },
            new Copyable("Drawn with DrawnUI: every pixel of this page, 3D and context menus included.")
            {
                FontSize = 12, TextColor = Theme.Muted, HorizontalOptions = LayoutOptions.Center,
            },
        }
    };

    static SkiaShape Tile(string title, string caption, SkiaControl piece, SkiaControl button) => new()
    {
        WidthRequest = 300,
        // Holds a 3D piece that draws every frame: a recorded cache would be made again each frame.
        UseCache = SkiaCacheType.None,
        CornerRadius = 16,
        BackgroundColor = Theme.Card,
        // raised from the page: lit top edge, shaded bottom edge
        BevelType = BevelType.Bevel,
        Bevel = Theme.CardBevel(),
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
                            new Copyable(title) { FontSize = 18, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                            new Copyable(caption) { FontSize = 13, TextColor = Theme.Muted, HeightRequest = 36 },
                        }
                    },
                    Stage(piece).WithHeight(RainHeight),
                    button.WithHorizontalOptions(LayoutOptions.Center),
                }
            }
        }
    };

    /// <summary>The rounded panel a 3D piece stands on.</summary>
    static SkiaShape Stage(SkiaControl piece) => new()
    {
        CornerRadius = 14,
        UseCache = SkiaCacheType.None,
        BackgroundColor = Theme.Stage,
        // sunk into the card
        BevelType = BevelType.Emboss,
        Bevel = Theme.CardBevel(),
        HorizontalOptions = LayoutOptions.Fill,
        Children = { piece },
    };

    /// <summary>Two-column cards stay side by side while their content is at least this wide; narrower, the 3D view goes on top.</summary>
    const float TwoColumnsMin = 720;
    /// <summary>A list row keeps its button beside the text while the card's content is at least this wide.</summary>
    const float RowBesideMin = 420;
    /// <summary>The mascot's view height when the form and the mascot stack.</summary>
    const float StackedView = 320;

    /// <summary>Layout changes that follow the page's width (the same thresholds as DrawnUI for Rust's gallery3d).</summary>
    readonly List<Action<float>> _adapt = new();
    float _width = -1;

    /// <summary>The width a card's content has on a page <paramref name="width"/> points wide, at most the desktop card.</summary>
    static float CardInner(float width) => MathF.Min(width - 2 * 24 - 2 * 21, WideWidth);

    static bool TwoColumns(float width) => width <= 0 || CardInner(width) >= TwoColumnsMin;

    /// <inheritdoc />
    protected override void OnMeasured()
    {
        base.OnMeasured();
        var width = (float)MeasuredSize.Units.Width;
        if (width <= 0 || float.IsInfinity(width) || width == _width)
            return;
        _width = width;
        foreach (var adapt in _adapt)
            adapt(width);
    }

    /// <summary>
    /// A two-column card body: the 3D view (<paramref name="viewWidth"/> x <paramref name="viewHeight"/>) beside its panel;
    /// when the card is narrow, stacked, the view on top at full width and its desktop proportions.
    /// </summary>
    SkiaLayout TwoColumnCard(SkiaLayout row, float viewWidth, float viewHeight)
    {
        _adapt.Add(width =>
        {
            var wide = TwoColumns(width);
            row.Type = wide ? LayoutType.Row : LayoutType.Column;
            row.HorizontalOptions = LayoutOptions.Fill;
            var view = row.Children[0];
            var side = row.Children[1];
            view.WidthRequest = wide ? viewWidth : -1;
            view.HorizontalOptions = wide ? LayoutOptions.Start : LayoutOptions.Fill;
            view.HeightRequest = wide ? viewHeight : MathF.Round(CardInner(width) * viewHeight / viewWidth);
            side.WidthRequest = wide ? CardInner(width) - viewWidth - 24 : -1;
            side.HorizontalOptions = wide ? LayoutOptions.Start : LayoutOptions.Fill;
        });
        return row;
    }

    /// <summary>
    /// A wide card for one demo: its title, a caption, and what it shows. A <paramref name="still"/> card (its 3D at
    /// rest until touched: the viewer, the card) is one GPU blit while the page scrolls or another card
    /// moves; a card whose 3D moves all the time (globe, mascot, mobs) is drawn live, a cache would be made again
    /// each frame. The shop scrolls inside its card, so it stays live too.
    /// </summary>
    SkiaShape WideCard(string title, string caption, SkiaControl content, bool still = false) => new SkiaShape
    {
        UseCache = still ? SkiaCacheType.GPU : SkiaCacheType.None,
        CornerRadius = 16,
        BackgroundColor = Theme.Card,
        // raised from the page: lit top edge, shaded bottom edge
        BevelType = BevelType.Bevel,
        Bevel = Theme.CardBevel(),
        Padding = new Thickness(20),
        HorizontalOptions = LayoutOptions.Center,
        WidthRequest = WideWidth + 42,
        Children =
        {
            new SkiaStack
            {
                Spacing = 10,
                Children =
                {
                    new SkiaStack
                    {
                        Spacing = 10,
                        // Static text over a card that may redraw every frame: one blit (the still card's GPU cache holds it anyway).
                        UseCache = still ? SkiaCacheType.None : SkiaCacheType.Image,
                        Children =
                        {
                            new Copyable(title) { FontSize = 20, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                            new Copyable(caption) { FontSize = 14, TextColor = Theme.Muted },
                        }
                    },
                    content.WithMargin(0, 6, 0, 0),
                }
            },
        },
    }
    // The page's width up to the desktop card.
    .Adapt(card => _adapt.Add(width => card.WidthRequest = MathF.Min(width - 2 * 24, WideWidth + 42)));

    /// <summary>The medal's resting turn: it spins from here and lands here.</summary>

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

    SkiaControl MobsCard() => WideCard("3D mobs on a 2D field",
        "Many 3D monsters from one model, moved like sprites and drawn by one layer in one pass. Tap one to hit it.", new SkiaStack
    {
        Spacing = 10,
        Children =
        {
            // The whole field scaled down to the card when the card is narrower than it.
            new SkiaShape
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    MaximumWidthRequest = FieldWidth,
                    HeightRequest = FieldHeight,
                    UseCache = SkiaCacheType.None,
                    CornerRadius = 10,
                    Children =
                    {
                        new RescalingLayout
                        {
                            LogicalWidth = FieldWidth,
                            LogicalHeight = FieldHeight,
                            Children =
                            {
                                // A side-scroller scene behind the mobs, its layers moving at their own pace.
                                new Scenery { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }.Assign(out _scenery),
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
                    }
                }
                .Adapt(me => _adapt.Add(width => me.HeightRequest = MathF.Round(FieldHeight * MathF.Min(CardInner(width), FieldWidth) / FieldWidth))),
            new SkiaWrap
            {
                Spacing = 8,
                Children =
                {
                    // how many: one radio group, its own Tab stop walked with the arrows; on a narrow phone its buttons
                    // wrap instead of squeezing
                    new SkiaWrap
                    {
                        Spacing = 8,
                        HorizontalOptions = LayoutOptions.Start,
                        AccessibilityRole = Aria.RoleRadioGroup,
                        AccessibilityLabel = "Mobs on the field",
                        Children = MobCounts.Select((n, i) => (SkiaControl)new Button3D($"{n} mobs", Theme.Quiet, 110) { AccessibilityRole = Aria.RoleRadio }
                            .Assign(out _mobButtons[i])
                            .OnTapped(_ => SetMobs(n))).ToList(),
                    },
                    // How they look, switched in place: lit 3D, or drawn as a cartoon. A Tab stop of its own.
                    new SkiaRow
                    {
                        Spacing = 10,
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new SkiaLabel("Drawn") { FontSize = 14, FontFamily = Theme.Title, TextColor = Theme.Ink, VerticalOptions = LayoutOptions.Center },
                            new Switch3D { VerticalOptions = LayoutOptions.Center, AccessibilityLabel = "Drawn look" }
                                .Assign(out _drawnSwitch)
                                .ObserveSelf((me, prop) =>
                                {
                                    if (prop == nameof(SkiaToggle.IsToggled))
                                        SetDrawnMobs(me.IsToggled);
                                }),
                        }
                    },
                }
            },
        }
    });

    SkiaControl _globeCard;

    /// <summary>For <see cref="PerfProbe"/> (globetaps): a tap on a city of the list, as the button does it.</summary>
    internal void ProbeTapCity(int n) => GoToCity(n % Cities.Length);

    /// <summary>For <see cref="PerfProbe"/> (globetaps): what could pile up per tap.</summary>
    internal string ProbeGlobeCounts()
    {
        var viewChanged = typeof(SkiaModel3D).GetField("ViewChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(_globe) as Delegate;
        return $"viewChanged handlers {viewChanged?.GetInvocationList().Length ?? 0}, globe animating {_globe.IsAnimating}, spin {_globe.AutoRotate}";
    }

    /// <summary>For <see cref="PerfProbe"/> (mode globechipraw): the name chips drawn live, for the comparison.</summary>
    internal static bool ProbeChipsUncached;

    /// <summary>For <see cref="PerfProbe"/>: every half second for <paramref name="times"/> times, a name chip stays open on a
    /// pin facing the eye (the turning globe carries the open one away).</summary>
    void KeepChipOpen(int times)
    {
        if (times <= 0)
            return;
        Tasks.StartDelayed(TimeSpan.FromMilliseconds(500), () =>
        {
            if (_openCity < 0 || !_cityLabels[_openCity].IsVisible)
                for (var i = 0; i < Cities.Length; i++)
                    if (_cityPins[i].IsVisible)
                    {
                        _openCity = -1;
                        ToggleCity(i);
                        break;
                    }
            KeepChipOpen(times - 1);
        });
    }

    /// <summary>For <see cref="PerfProbe"/>: puts the globe's card at the top of the page; with a chip, opens the name of the
    /// first pin facing the eye while the globe keeps turning (as a tap on that pin).</summary>
    internal void ProbeShowGlobe(bool chip = false)
    {
        if (chip)
            KeepChipOpen(20);
        if (ProbeChipsUncached)
            foreach (var label in _cityLabels)
                label.UseCache = SkiaCacheType.None;
        if (_scroll.Content is not SkiaLayout page)
            return;
        foreach (var cell in page.GetStackStructure().GetChildren())
            if (ReferenceEquals(cell.View, _globeCard))
                _scroll.ScrollTo(0, -(cell.Destination.Top / page.RenderingScale), 0, true);
    }

    /// <summary>
    /// For <see cref="PerfProbe"/>: puts the page (or, with <paramref name="list"/>, the 3D product list) at
    /// <paramref name="travel"/> points of steady scrolling, going down and back up between the ends; -1 = the end.
    /// </summary>
    /// <summary>For <see cref="PerfProbe"/> (tour): shows step <paramref name="step"/> of the page, one screen per step, and
    /// says where it is; "end" after the last.</summary>
    internal string ProbeTourStep(int step)
    {
        var range = (float)(_scroll.ContentSize.Units.Height - _scroll.Viewport.Units.Height);
        var screen = (float)_scroll.Viewport.Units.Height * 0.9f;
        var y = step * screen;
        if (y > range + screen)
            return "end";
        y = MathF.Min(y, range);
        _scroll.ScrollTo(0, -y, 0, true);
        return $"y {y:0} of {range:0}";
    }

    internal void ProbeScroll(bool list, float travel)
    {
        var scroll = list ? _listScroll : _scroll;
        var range = MathF.Max(1, (float)(scroll.ContentSize.Units.Height - scroll.Viewport.Units.Height));
        var at = travel < 0 ? range : travel % (2 * range);
        scroll.ScrollTo(0, -(at < range ? at : 2 * range - at), 0, true);
    }

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();
        _built = true;
        // The pins' first placement makes the globe's ray tree (hidden pins are found by a ray): done now, while the
        // page opens, and not in the frame the globe scrolls into view.
        PlacePins();
        // every drawn frame (a scroll, a tap) starts the loop again when something it moves comes into view
        if (!_wakeHooked && Superview is DrawnView canvas)
        {
            _wakeHooked = true;
            canvas.WasDrawn += (_, _) => WakeLoop();
        }
        _looping = true;
        StartLoop();
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
        _drawnSwitch.IsToggled = _drawnMobs;
    }

    /// <summary>The drawn look of the mobs: toon shading and the gallery's ink outline (same in DrawnUI for Rust).</summary>
    const float DrawnOutline = 2;
    static readonly Color DrawnInk = Color.FromRgb(43, 37, 64); // #2B2540

    /// <summary>The mobs as lit 3D models, or drawn as a cartoon.</summary>
    internal void SetDrawnMobs(bool drawn)
    {
        _drawnMobs = drawn;
        _field.Look = drawn ? Look.Toon : Look.Lit;
        _field.OutlineWidth = drawn ? DrawnOutline : 0;
        _field.OutlineColor = DrawnInk;
        UpdateMobButtons();
    }
}
