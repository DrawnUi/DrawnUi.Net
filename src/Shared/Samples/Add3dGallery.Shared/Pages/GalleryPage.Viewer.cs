using System.Numerics;

namespace Add3dGallery;

/// <summary>
/// The product viewer: a walnut sofa (Poly Haven, CC0) shipped in the app or loaded from the site by its address (a
/// placeholder while it loads, an error card when it cannot), turned by dragging, zoomed by pinch or wheel, reset by a
/// double tap; dots anchored to points of the sofa open a note on that part; the upholstery swatches put another
/// fabric image on the sofa (SetMaterialImage) with its own shine (SetMaterialRoughness) and its own price. The same
/// files, points and strings as DrawnUI for Rust's gallery3d.
/// </summary>
public partial class GalleryPage
{
    const float ViewerWidth = 620, ViewerHeight = 420, PinSize = 26, NoteWidth = 210;

    /// <summary>Where the sofa comes from.</summary>
    enum Source
    {
        /// <summary>The .glb shipped inside the app.</summary>
        App,
        /// <summary>The same file at its address on the app's site.</summary>
        Link,
        /// <summary>An address with nothing behind it: the error card.</summary>
        Broken,
    }

    static readonly (Source Source, string Caption)[] Sources =
    {
        (Source.App, "In the app"),
        (Source.Link, "From a link"),
        (Source.Broken, "Broken link"),
    };

    const string SofaUrl = "assets/sofa.glb", BrokenUrl = "assets/no-such-sofa.glb";

    /// <summary>Parts of the sofa worth a note: name, note and the point in the model's own units, a hair above its surface.</summary>
    static readonly (string Name, string Note, Vector3 At)[] Hotspots =
    {
        ("Seat", "Feather-wrapped cushions", new Vector3(0.3f, 0.386f, 0.05f)),
        ("Back", "Hand-sewn, deep buttoned", new Vector3(0, 0.65f, -0.252f)),
        ("Frame", "Carved walnut", new Vector3(0.76f, 0.5f, 0.183f)),
    };

    /// <summary>
    /// The upholstery, cheapest first: name, its fabric image (Poly Haven CC0), how matte it is (leather shines a
    /// little), the swatch's color and the sofa's price in it.
    /// </summary>
    static readonly (string Name, byte[] Image, float Roughness, Color Swatch, int Price)[] Fabrics =
    {
        ("Linen", Theme.Linen, 1.0f, Color.FromRgb(140, 172, 206), 1190),
        ("Wool", Theme.Wool, 0.95f, Color.FromRgb(122, 120, 114), 1290),
        ("Velvet", Theme.Velvet, 0.7f, Color.FromRgb(180, 40, 62), 1490),
        ("Leather", Theme.Leather, 0.42f, Color.FromRgb(92, 52, 26), 1890),
    };

    /// <summary>The fabric images, decoded once.</summary>
    static Texture3D[] _fabricImages;

    SkiaLayer _viewerHost, _loading, _failed;
    SkiaModel3D _viewer;
    SkiaLabel _failedText, _price;
    SkiaShape _note;
    SkiaLabel _noteTitle, _noteText;
    readonly SkiaShape[] _pins = new SkiaShape[Hotspots.Length];
    readonly UiButton[] _sourceButtons = new UiButton[Sources.Length];
    readonly SkiaShape[] _swatches = new SkiaShape[Fabrics.Length];
    int _fabric = -1, _openSpot = -1;
    Source _source;

    static string Dollars(int amount) => "$" + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    SkiaControl ViewerCard() => WideCard(new SkiaRow
    {
        Spacing = 24,
        Children =
        {
            new SkiaShape
            {
                CornerRadius = 12,
                // At rest the sofa shows its last render and the dots stand still: one GPU blit. Turning, zooming or a
                // note draws it again.
                UseCache = SkiaCacheType.GPU,
                BackgroundColor = Theme.Stage,
                WidthRequest = ViewerWidth,
                HeightRequest = ViewerHeight,
                Children =
                {
                    new SkiaLayer { VerticalOptions = LayoutOptions.Fill }.Assign(out _viewerHost),
                    // The dots and the note follow their points of the model.
                    new SkiaLayer
                    {
                        VerticalOptions = LayoutOptions.Fill,
                        Children = Hotspots.Select((spot, i) => (SkiaControl)Pin(i, spot.Name)).Append(Note()).ToList(),
                    },
                    new SkiaLabel("Drag to turn  ·  pinch or scroll to zoom  ·  double-tap to reset")
                    {
                        FontSize = 12, TextColor = Theme.Muted, Margin = new Thickness(0, 0, 0, 12),
                        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End, InputTransparent = true,
                    },
                    Loading().Assign(out _loading),
                    Failed().Assign(out _failed),
                }
            },
            new SkiaStack
            {
                Spacing = 14,
                WidthRequest = WideWidth - ViewerWidth - 24,
                Children =
                {
                    // Uncached: the price counts. Its box is the stack's width, so a new price never lays the page out again.
                    new SkiaStack
                    {
                        Spacing = 2,
                        IgnoreChildrenInvalidations = true,
                        Children =
                        {
                            new SkiaLabel("Walnut sofa") { FontSize = 24, FontFamily = Theme.Title, TextColor = Theme.Ink, AccessibilityRole = Aria.RoleHeading },
                            new SkiaLabel("Three-seater, carved walnut frame") { FontSize = 13, TextColor = Theme.Muted },
                            new SkiaLabel(Dollars(Fabrics[0].Price))
                                {
                                    FontSize = 18, FontFamily = Theme.Title, TextColor = Theme.Gold, Margin = new Thickness(0, 6, 0, 0),
                                    HorizontalOptions = LayoutOptions.Fill, AccessibilityRole = Aria.RoleText,
                                }
                                .WithAccessibilityLive()
                                .Assign(out _price),
                        }
                    },
                    new SkiaStack
                    {
                        Spacing = 14,
                        // Still beside a stage that draws every frame: blitted, drawn again only when a choice changes.
                        UseCache = SkiaCacheType.Image,
                        Children =
                        {
                            PanelLabel("FABRIC"),
                            new SkiaRow
                            {
                                Spacing = 10,
                                AccessibilityRole = Aria.RoleRadioGroup,
                                AccessibilityLabel = "Upholstery",
                                Children = Fabrics.Select((f, i) => (SkiaControl)new SkiaStack
                                {
                                    Spacing = 4,
                                    WidthRequest = 60,
                                    Children =
                                    {
                                        Swatch($"{f.Name}, {Dollars(f.Price)}", f.Swatch).Assign(out _swatches[i]).OnTapped(_ => SetFabric(i)),
                                        new SkiaLabel(f.Name) { FontSize = 12, TextColor = Theme.Muted, HorizontalOptions = LayoutOptions.Center },
                                    }
                                }).ToList(),
                            },
                            PanelLabel("MODEL"),
                            new SkiaWrap
                            {
                                Spacing = 8,
                                AccessibilityRole = Aria.RoleRadioGroup,
                                AccessibilityLabel = "Load the model",
                                Children = Sources.Select((s, i) => (SkiaControl)new UiButton(s.Caption, Theme.Quiet, 108) { AccessibilityRole = Aria.RoleRadio }
                                    .Assign(out _sourceButtons[i])
                                    .OnTapped(_ => LoadViewer(s.Source))).ToList(),
                            },
                            new UiButton("Reset view", Theme.Quiet, 140) { AccessibilityHint = "Turns the sofa back to the front" }
                                .OnTapped(_ => _viewer?.ResetView()),
                        }
                    },
                }
            },
        }
    }).Initialize(_ =>
    {
        SetFabric(0);
        LoadViewer(Source.App);
    });

    static SkiaLabel PanelLabel(string text) => new(text)
    {
        FontSize = 12, FontFamily = Theme.Title, TextColor = Theme.Muted, Margin = new Thickness(0, 8, 0, -6),
        CharacterSpacing = 1.5,
    };

    /// <summary>A dot on a part of the model: tap it for the part's note.</summary>
    SkiaShape Pin(int index, string name) => new SkiaShape
        {
            Type = ShapeType.Circle,
            WidthRequest = PinSize,
            HeightRequest = PinSize,
            BackgroundColor = Colors.White,
            StrokeColor = Theme.Accent,
            StrokeWidth = 3,
            // Shown while its point faces the eye: hidden, it is out of taps and screen readers too.
            IsVisible = false,
            AccessibilityRole = Aria.RoleButton,
            AccessibilityLabel = $"{name}, hotspot",
            AccessibilityHint = "Shows a note about this part",
            Shadows = new List<SkiaShadow> { new() { Blur = 4, Y = 2, Opacity = 0.35, Color = Colors.Black } },
            Children =
            {
                new SkiaLabel($"{index + 1}")
                {
                    FontSize = 12, FontFamily = Theme.Title, TextColor = Theme.Accent,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
                },
            }
        }
        .Assign(out _pins[index])
        .OnHovered((me, on) => me.BackgroundColor = on ? Theme.Lighter(Theme.Accent, 0.75f) : Colors.White)
        .OnTapped(_ => ToggleNote(index));

    /// <summary>The note a dot opens, above its point.</summary>
    SkiaShape Note() => new SkiaShape
    {
        CornerRadius = 10,
        BackgroundColor = Theme.Header,
        StrokeColor = Theme.Line,
        StrokeWidth = 1,
        Padding = new Thickness(12, 9),
        WidthRequest = NoteWidth,
        IsVisible = false,
        InputTransparent = true,
        Shadows = new List<SkiaShadow> { new() { Blur = 10, Y = 4, Opacity = 0.4, Color = Colors.Black } },
        Children =
        {
            new SkiaStack
            {
                Spacing = 2,
                Children =
                {
                    new SkiaLabel { FontSize = 14, FontFamily = Theme.Title, TextColor = Theme.Ink }.Assign(out _noteTitle),
                    new SkiaLabel { FontSize = 13, TextColor = Theme.Muted }.Assign(out _noteText),
                }
            }
        }
    }.Assign(out _note);

    /// <summary>The placeholder while the model loads: a spinning ring and a line.</summary>
    static SkiaLayer Loading() => new()
    {
        VerticalOptions = LayoutOptions.Fill,
        BackgroundColor = Theme.Stage,
        IsVisible = false,
        Children =
        {
            new SkiaStack
            {
                Spacing = 14,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new SkiaShape
                        {
                            Type = ShapeType.Arc, WidthRequest = 44, HeightRequest = 44, StrokeWidth = 4,
                            StrokeColor = Theme.Accent, Value1 = 0, Value2 = 270, StrokeCap = SKStrokeCap.Round,
                            HorizontalOptions = LayoutOptions.Center,
                        }
                        .AnimateRotation(0, 360, seconds: 0.9, repeat: -1),
                    new SkiaLabel("Loading the sofa…") { FontSize = 14, TextColor = Theme.Muted, HorizontalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RoleText }
                        .WithAccessibilityLive(),
                }
            }
        }
    };

    /// <summary>The card shown when the model could not be loaded, with a way back.</summary>
    SkiaLayer Failed() => new()
    {
        VerticalOptions = LayoutOptions.Fill,
        BackgroundColor = Theme.Stage,
        IsVisible = false,
        Children =
        {
            new SkiaStack
            {
                Spacing = 10,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 340,
                Children =
                {
                    new SkiaShape
                    {
                        Type = ShapeType.Circle, WidthRequest = 48, HeightRequest = 48, BackgroundColor = Theme.Bad.WithAlpha(0.18f),
                        HorizontalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new SkiaLabel("!") { FontSize = 24, FontFamily = Theme.Title, TextColor = Theme.Bad, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
                        }
                    },
                    new SkiaLabel("The model could not be loaded")
                    {
                        FontSize = 16, FontFamily = Theme.Title, TextColor = Theme.Ink, HorizontalOptions = LayoutOptions.Center,
                        AccessibilityRole = Aria.RoleText,
                    }.WithAccessibilityLive(),
                    new SkiaLabel { FontSize = 13, TextColor = Theme.Muted, HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = DrawTextAlignment.Center }
                        .Assign(out _failedText),
                    new UiButton("Use the app's copy", Theme.Accent, 180) { Margin = new Thickness(0, 6, 0, 0) }
                        .WithHorizontalOptions(LayoutOptions.Center)
                        .OnTapped(_ => LoadViewer(Source.App)),
                }
            }
        }
    };

    /// <summary>A round swatch: a ring shows the chosen one.</summary>
    static SkiaShape Swatch(string name, Color color) => new SkiaShape
        {
            Type = ShapeType.Circle,
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeWidth = 3,
            StrokeColor = Colors.Transparent,
            Padding = new Thickness(5),
            HorizontalOptions = LayoutOptions.Center,
            AccessibilityRole = Aria.RoleRadio,
            AccessibilityLabel = name,
            Children =
            {
                new SkiaShape { Type = ShapeType.Circle, BackgroundColor = color, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill },
            }
        }
        .OnHovered((me, on) => me.BackgroundColor = on ? Theme.Lighter(Theme.Card, 0.12f) : Colors.Transparent);

    void SetFabric(int index)
    {
        _fabric = index;
        for (var i = 0; i < _swatches.Length; i++)
        {
            _swatches[i].StrokeColor = i == index ? Theme.Ink : Colors.Transparent;
            _swatches[i].AccessibilityIsPressed = i == index;
        }
        Dress(_viewer);
    }

    /// <summary>The price on the label, counting to the chosen fabric's.</summary>
    float _priceShown = Fabrics[0].Price;

    /// <summary>Each frame: the price counts to the chosen fabric's at 2,800 dollars a second (Linen to Leather in a quarter second).</summary>
    void PriceFrame(float delta)
    {
        var target = Fabrics[Math.Max(_fabric, 0)].Price;
        if (_priceShown == target)
            return;
        var step = MathF.Sign(target - _priceShown) * MathF.Max(700 * 4 * delta, 1);
        _priceShown = MathF.Abs(target - _priceShown) <= MathF.Abs(step) ? target : _priceShown + step;
        _price.Text = Dollars((int)MathF.Round(_priceShown));
    }

    /// <summary>The upholstery's image and shine for the chosen fabric.</summary>
    void Dress(SkiaModel3D sofa)
    {
        if (sofa == null || _fabric < 0)
            return;
        _fabricImages ??= Fabrics.Select(f => Texture3D.Decode(f.Image)).ToArray();
        sofa.SetMaterialImage("upholstery", _fabricImages[_fabric]);
        sofa.SetMaterialColor("upholstery", Colors.White);
        sofa.SetMaterialRoughness("upholstery", Fabrics[_fabric].Roughness);
    }

    void ToggleNote(int index)
    {
        _openSpot = _openSpot == index ? -1 : index;
        for (var i = 0; i < _pins.Length; i++)
            _pins[i].AccessibilityIsPressed = i == _openSpot;
        if (_openSpot >= 0)
        {
            _noteTitle.Text = Hotspots[index].Name;
            _noteText.Text = Hotspots[index].Note;
        }
        PlaceSpots();
    }

    /// <summary>Shows the sofa from where the buttons say: from the app at once, from an address through its loading states.</summary>
    void LoadViewer(Source source)
    {
        _source = source;
        for (var i = 0; i < Sources.Length; i++)
        {
            var on = Sources[i].Source == source;
            _sourceButtons[i].Fill = on ? Theme.Accent : Theme.Quiet;
            _sourceButtons[i].AccessibilityIsPressed = on;
        }
        var sofa = source == Source.App ? new SkiaModel3D(Theme.Sofa) : new SkiaModel3D();
        sofa.Yaw = 28;
        sofa.Pitch = 14;
        sofa.Zoom = 0.95f;
        sofa.PinchZoom = true;
        sofa.MinZoom = 0.55f;
        sofa.MaxZoom = 1.8f;
        sofa.ResetOnDoubleTap = true;
        sofa.Antialias = true;
        sofa.HorizontalOptions = LayoutOptions.Fill;
        sofa.VerticalOptions = LayoutOptions.Fill;
        sofa.ViewChanged += (_, _) => PlaceSpots();
        sofa.LoadStateChanged += (me, state) =>
        {
            if (!ReferenceEquals(me, _viewer))
                return;
            ShowLoadState(state, ((SkiaModel3D)me).Error);
            if (state == LoadState3D.Ready)
                Dress((SkiaModel3D)me);
        };
        Dress(sofa);
        ShowViewer(sofa);
        ShowLoadState(sofa.LoadState, sofa.Error);
        if (source != Source.App)
            sofa.Source = source == Source.Link ? SofaUrl : BrokenUrl;
    }

    void ShowLoadState(LoadState3D state, string error)
    {
        _loading.IsVisible = state == LoadState3D.Loading;
        _failed.IsVisible = state == LoadState3D.Failed;
        if (state == LoadState3D.Failed)
            _failedText.Text = $"{(_source == Source.Broken ? BrokenUrl : SofaUrl)}: {error}.\nCheck the link, or show the copy shipped with the app.";
        PlaceSpots();
    }

    void ShowViewer(SkiaModel3D model)
    {
        if (_viewer != null)
        {
            _viewerHost.RemoveSubView(_viewer);
            // Its GPU frame is freed with the frame's other disposals, on the drawing thread.
            _viewerHost.DisposeObject(_viewer);
        }
        _viewer = model;
        _viewerHost.AddSubView(model);
        _openSpot = -1;
        foreach (var pin in _pins)
            pin.AccessibilityIsPressed = false;
        PlaceSpots();
    }

    /// <summary>The dots and the open note go where their points of the sofa show (when its view moves, and on changes).</summary>
    void PlaceSpots()
    {
        var ready = _viewer is { LoadState: LoadState3D.Ready };
        for (var i = 0; i < Hotspots.Length; i++)
        {
            var pin = _pins[i];
            var at = ready ? _viewer.ScreenPosition(Hotspots[i].At) : null;
            var shown = at is { Occluded: false };
            if (pin.IsVisible != shown)
                pin.IsVisible = shown;
            if (shown)
                Move(pin, at.Value.X - PinSize / 2, at.Value.Y - PinSize / 2);
            if (i != _openSpot)
                continue;
            if (_note.IsVisible != shown)
                _note.IsVisible = shown;
            if (!shown)
                continue;
            var height = (float)_note.MeasuredSize.Units.Height;
            Move(_note, Math.Clamp(at.Value.X - NoteWidth / 2, 8, ViewerWidth - NoteWidth - 8),
                MathF.Max(8, at.Value.Y - PinSize / 2 - height - 8));
        }
        if (_openSpot < 0 && _note.IsVisible)
            _note.IsVisible = false;
    }

    /// <summary>Moves a control drawn over another to a point, skipping changes under a quarter point.</summary>
    static void Move(SkiaControl control, float x, float y)
    {
        if (MathF.Abs((float)control.TranslationX - x) > 0.25f || MathF.Abs((float)control.TranslationY - y) > 0.25f)
        {
            control.TranslationX = x;
            control.TranslationY = y;
        }
    }
}
