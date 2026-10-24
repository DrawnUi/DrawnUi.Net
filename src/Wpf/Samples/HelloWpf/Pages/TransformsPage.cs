using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// Render transforms (MAUI VisualElement names), Opacity, hit-testing through transforms and the
/// *ToAsync animations. Ported from the React demo's TransformsPage.tsx.
/// </summary>
public class TransformsPage : SkiaLayer
{
    private SkiaSvg _logo;
    private SkiaButton _tapped;
    private SkiaButton _spinButton;
    private int _taps;
    private CancellationTokenSource _spin;

    private SkiaShape _card3d, _front, _back;
    private SkiaSlider _sliderX, _sliderY, _sliderZ;
    private SkiaLabel _readout, _lastTile;

    /// <summary>Tile gradients of the tilted strip: the root page's card title colors, G0..G5.</summary>
    private static readonly string[][] StripColors =
    {
        new[] { "#6EA8FE", "#0D6EFD" }, new[] { "#D63384", "#FD7E14" }, new[] { "#20C997", "#0DCAF0" },
        new[] { "#FFC107", "#FD7E14" }, new[] { "#A98EFF", "#6610F2" }, new[] { "#0DCAF0", "#6EA8FE" },
    };
    private SkiaButton _spin3dButton;
    private CancellationTokenSource _spin3d;
    private bool _syncing, _turning;

    /// <summary>Builds the page.</summary>
    public TransformsPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new SkiaStack
                {
                    Spacing = 16,
                    Padding = new Thickness(16),
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 720,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("Transforms") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
                        new SkiaLabel("Applied at render around the arranged box, so layout is untouched and caches stay valid. Same names as MAUI: TranslationX/Y, Rotation, RotationX/Y, ScaleX/Y, SkewX/Y, AnchorX/Y, Opacity, plus TranslationZ for 3D depth.")
                        {
                            FontSize = 13, TextColor = Colors.LightGray, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center,
                        },

                        Card(Title("One property each"),
                            new SkiaWrap
                            {
                                Spacing = 4,
                                HorizontalOptions = LayoutOptions.Center,
                                Children = new List<SkiaControl>
                                {
                                    Tile("none", s => { }),
                                    Tile("Rotation=15", s => s.Rotation = 15),
                                    Tile("RotationX=45", s => s.RotationX = 45),
                                    Tile("RotationY=45", s => s.RotationY = 45),
                                    Tile("Scale=1.3", s => { s.ScaleX = 1.3; s.ScaleY = 1.3; }),
                                    Tile("ScaleX=-1", s => s.ScaleX = -1),
                                    Tile("SkewX=20", s => s.SkewX = 20),
                                    Tile("TranslationY=10", s => s.TranslationY = 10),
                                    Tile("Opacity=0.35", s => s.Opacity = 0.35),
                                    Tile("Rotation=15 AnchorX/Y=0", s => { s.Rotation = 15; s.AnchorX = 0; s.AnchorY = 0; }),
                                },
                            }),

                        Card(Title("3D — RotationX, RotationY, TranslationZ, with perspective"),
                            new SkiaLayer
                            {
                                HeightRequest = 250,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle,
                                        CornerRadius = 16,
                                        WidthRequest = 240,
                                        HeightRequest = 150,
                                        HorizontalOptions = LayoutOptions.Center,
                                        VerticalOptions = LayoutOptions.Center,
                                        UseCache = SkiaCacheType.Image,
                                        Children = new List<SkiaControl>
                                        {
                                            Face("#6EA8FE", "#0D6EFD", "DrawnUI", "Tap to flip").Assign(out _front),
                                            Face("#D63384", "#FD7E14", "Back side", "Drawn mirrored, reads right from behind").Assign(out _back),
                                        },
                                    }
                                    .Assign(out _card3d)
                                    .OnTapped(me => _ = Turn(aroundY: true))
                                    .ObserveSelf((me, prop) =>
                                    {
                                        if (prop is nameof(RotationX) or nameof(RotationY) or nameof(TranslationZ))
                                            OnCardTurned();
                                    }),
                                },
                            },
                            new SkiaLabel { FontSize = 13, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center }.Assign(out _readout),
                            SliderRow("RotationX", -180, 180, v => _card3d.RotationX = v, out _sliderX),
                            SliderRow("RotationY", -180, 180, v => _card3d.RotationY = v, out _sliderY),
                            SliderRow("TranslationZ", -300, 150,v => _card3d.TranslationZ = v, out _sliderZ),
                            new SkiaWrap
                            {
                                Spacing = 6,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Flip") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _ = Turn(aroundY: true)),
                                    new SkiaButton("Tumble") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _ = Turn(aroundY: false)),
                                    new SkiaButton("Spin") { BackgroundColor = Color.Parse("#20C997") }.Assign(out _spin3dButton).OnTapped(me => _ = ToggleSpin3d()),
                                    new SkiaButton("Reset") { BackgroundColor = Color.Parse("#495057") }.OnTapped(me => _ = Reset3d()),
                                },
                            },
                            new SkiaLabel("Perspective is built in: RotationX turns around the horizontal axis, RotationY around the vertical one, TranslationZ moves the control toward (+) or away from (−) the viewer. The card keeps its Image cache while it turns; it shows the back face once it faces away.")
                            {
                                FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card(Title("Gestures map through 3D — swipe and tap the tilted strip"),
                            new SkiaLayer
                            {
                                HeightRequest = 220,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle,
                                        CornerRadius = 16,
                                        WidthRequest = 280,
                                        HeightRequest = 150,
                                        HorizontalOptions = LayoutOptions.Center,
                                        VerticalOptions = LayoutOptions.Center,
                                        RotationX = 16,
                                        RotationY = 20,
                                        Rotation = -6,
                                        BackgroundColor = Color.Parse("#111827"),
                                        StrokeColor = Color.Parse("#6EA8FE"),
                                        StrokeWidth = 1.55,
                                        Children = new List<SkiaControl>
                                        {
                                            new SkiaScroll
                                            {
                                                Orientation = ScrollOrientation.Horizontal,
                                                Content = new SkiaRow
                                                {
                                                    UseCache = SkiaCacheType.Image,
                                                    Spacing = 10,
                                                    Padding = new Thickness(14),
                                                    VerticalOptions = LayoutOptions.Center,
                                                    Children = Enumerable.Range(1, 20).Select(i => (SkiaControl)new SkiaShape
                                                    {
                                                        Type = ShapeType.Rectangle,
                                                        CornerRadius = 12,
                                                        WidthRequest = 96,
                                                        HeightRequest = 96,
                                                        FillGradient = new SkiaGradient
                                                        {
                                                            Type = GradientType.Linear,
                                                            EndXRatio = 1,
                                                            EndYRatio = 1,
                                                            Colors = StripColors[(i - 1) % StripColors.Length].Select(c => Color.Parse(c)).ToList(),
                                                        },
                                                        Children = new List<SkiaControl>
                                                        {
                                                            new SkiaLabel($"{i}") { FontSize = 30, FontFamily = "FontTextBold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
                                                        },
                                                    }.OnTapped(me => _lastTile.Text = $"Tapped: tile {i}")).ToList(),
                                                },
                                            }.Fill(),
                                        },
                                    },
                                },
                            },
                            new SkiaLabel("Tapped: none") { FontSize = 13, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center }.Assign(out _lastTile),
                            new SkiaLabel("RotationX 16, RotationY 20, Rotation −6. A sideways swipe scrolls the strip and a tap picks the tile under the finger: input goes through the inverse of the 3D matrix. An up or down swipe still scrolls the page.")
                            {
                                FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card(Title("Gestures map through transforms — tap the rotated, scaled button"),
                            new SkiaStack
                            {
                                Spacing = 8,
                                HeightRequest = 120,
                                HorizontalOptions = LayoutOptions.Fill,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Tapped 0×") { BackgroundColor = Color.Parse("#D63384"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Rotation = -20, ScaleX = 1.4, ScaleY = 1.4, ApplyEffect = SkiaTouchAnimation.Ripple }
                                        .Assign(out _tapped)
                                        .OnTapped(me => { _taps++; _tapped.Text = $"Tapped {_taps}×"; }),
                                },
                            },
                            new SkiaLabel("The hit rect is the drawn one (inverse RenderTransformMatrix), not the layout box; the ripple lands under the finger.")
                            {
                                FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card(Title("Animations — FadeToAsync, ScaleToAsync, TranslateToAsync, RotateToAsync (Tasks)"),
                            new SkiaStack
                            {
                                Spacing = 0,
                                HeightRequest = 140,
                                HorizontalOptions = LayoutOptions.Fill,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaSvg { Source = "images/drawnui.svg", WidthRequest = 100, LockRatio = 1, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }.Assign(out _logo),
                                },
                            },
                            new SkiaWrap
                            {
                                Spacing = 6,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Fade") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _ = Fade()),
                                    new SkiaButton("Scale") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _ = ScaleDemo()),
                                    new SkiaButton("Translate") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _ = Translate()),
                                    new SkiaButton("Rotate") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => { _logo.Rotation = 0; _ = _logo.RotateToAsync(360, 600, Easing.CubicInOut); }),
                                    new SkiaButton("Spin") { BackgroundColor = Color.Parse("#20C997") }.Assign(out _spinButton).OnTapped(me => _ = ToggleSpin()),
                                },
                            },
                            new SkiaLabel("Each *ToAsync cancels its previous run of the same kind (per-property CancellationTokenSource); pass a CancellationTokenSource to cancel from outside.")
                            {
                                FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill,
                            }),
                    },
                },
            }.Fill(),
        };

        OnCardTurned();
    }

    /// <summary>Shows the face that looks at the viewer and syncs the sliders and the readout with the card.</summary>
    private void OnCardTurned()
    {
        var cosX = Math.Cos(_card3d.RotationX * Math.PI / 180);
        var cosY = Math.Cos(_card3d.RotationY * Math.PI / 180);
        var backShows = cosX * cosY < 0;
        _front.Opacity = backShows ? 0 : 1;
        _back.Opacity = backShows ? 1 : 0;
        // seen from behind a face draws mirrored: the back face is mirrored once more to read right
        _back.ScaleX = cosY < 0 ? -1 : 1;
        _back.ScaleY = cosX < 0 ? -1 : 1;

        _syncing = true;
        _sliderX.End = Normalize(_card3d.RotationX);
        _sliderY.End = Normalize(_card3d.RotationY);
        _sliderZ.End = _card3d.TranslationZ;
        _syncing = false;

        _readout.Text = $"RotationX {Normalize(_card3d.RotationX):0}°  ·  RotationY {Normalize(_card3d.RotationY):0}°  ·  TranslationZ {_card3d.TranslationZ:0}";
    }

    /// <summary>Half a turn around the vertical (flip) or the horizontal (tumble) axis.</summary>
    private async Task Turn(bool aroundY)
    {
        if (_turning || _spin3d != null)
            return;

        _turning = true;
        var from = aroundY ? _card3d.RotationY : _card3d.RotationX;
        await _card3d.AnimateRangeAsync(v =>
        {
            if (aroundY)
                _card3d.RotationY = v;
            else
                _card3d.RotationX = v;
        }, from, from + 180, 700, Easing.CubicInOut);

        if (aroundY)
            _card3d.RotationY = Normalize(_card3d.RotationY);
        else
            _card3d.RotationX = Normalize(_card3d.RotationX);
        _turning = false;
    }

    private async Task ToggleSpin3d()
    {
        if (_spin3d != null)
        {
            _spin3d.Cancel();
            _spin3d = null;
            _spin3dButton.Text = "Spin";
            _spin3dButton.BackgroundColor = Color.Parse("#20C997");
            return;
        }

        if (_turning)
            return;

        var cts = new CancellationTokenSource();
        _spin3d = cts;
        _spin3dButton.Text = "Stop spin";
        _spin3dButton.BackgroundColor = Color.Parse("#DC3545");
        while (!cts.IsCancellationRequested)
        {
            var from = Normalize(_card3d.RotationY);
            await _card3d.AnimateRangeAsync(v => _card3d.RotationY = v, from, from + 360, 3000, Easing.Linear, 0, cts.Token);
        }
        _card3d.RotationY = Normalize(_card3d.RotationY);
    }

    private async Task Reset3d()
    {
        if (_spin3d != null)
            await ToggleSpin3d();
        if (_turning)
            return;

        _turning = true;
        double x = Normalize(_card3d.RotationX), y = Normalize(_card3d.RotationY), z = _card3d.TranslationZ;
        await _card3d.AnimateRangeAsync(t =>
        {
            _card3d.RotationX = x * (1 - t);
            _card3d.RotationY = y * (1 - t);
            _card3d.TranslationZ = z * (1 - t);
        }, 0, 1, 400, Easing.CubicOut);
        _turning = false;
    }

    /// <summary>An angle in -180..180.</summary>
    private static double Normalize(double degrees) => ((degrees + 180) % 360 + 360) % 360 - 180;

    /// <summary>One face of the 3D card: a gradient fill with a title and a line of text.</summary>
    private static SkiaShape Face(string from, string to, string title, string text) => new()
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 16,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        FillGradient = new SkiaGradient
        {
            Type = GradientType.Linear,
            EndXRatio = 1,
            EndYRatio = 1,
            Colors = new List<Color> { Color.Parse(from), Color.Parse(to) },
        },
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 4,
                VerticalOptions = LayoutOptions.Center,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel(title) { FontSize = 28, FontFamily = "FontTextBold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
                    new SkiaLabel(text) { FontSize = 13, TextColor = Colors.White, Margin = new Thickness(12, 0), HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
                },
            },
        },
    };

    /// <summary>A caption, a slider for one 3D property and its value; moving the slider sets the property.</summary>
    private SkiaControl SliderRow(string name, double min, double max, Action<double> apply, out SkiaSlider slider) => new SkiaLayer
    {
        HorizontalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>
        {
            new SkiaLabel(name) { FontSize = 13, TextColor = Color.Parse("#ADB5BD"), VerticalOptions = LayoutOptions.Center },
            new SkiaSlider { AccessibilityLabel = name, Min = min, Max = max, Margin = new Thickness(104, 0, 0, 0), HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center }
                .Assign(out slider)
                .ObserveSelf((me, prop) =>
                {
                    if (prop == nameof(SkiaSlider.End) && !_syncing)
                        apply(me.End);
                }),
        },
    };

    private async Task Fade()
    {
        await _logo.FadeToAsync(0.15, 300);
        await _logo.FadeToAsync(1, 300);
    }

    private async Task ScaleDemo()
    {
        await _logo.ScaleToAsync(1.6, 1.6, 250, Easing.CubicOut);
        await _logo.ScaleToAsync(1, 1, 250, Easing.CubicIn);
    }

    private async Task Translate()
    {
        await _logo.TranslateToAsync(140, 0, 300, Easing.CubicInOut);
        await _logo.TranslateToAsync(0, 0, 300, Easing.CubicInOut);
    }

    private async Task ToggleSpin()
    {
        if (_spin != null)
        {
            _spin.Cancel();
            _spin = null;
            _spinButton.Text = "Spin";
            _spinButton.BackgroundColor = Color.Parse("#20C997");
            return;
        }

        var cts = new CancellationTokenSource();
        _spin = cts;
        _spinButton.Text = "Stop spin";
        _spinButton.BackgroundColor = Color.Parse("#DC3545");
        while (!cts.IsCancellationRequested)
        {
            _logo.Rotation = 0;
            await _logo.RotateToAsync(360, 1200, Easing.Linear, cts);
        }
    }

    /// <summary>A 96x64 tile with a caption, used to show one transform each.</summary>
    private static SkiaControl Tile(string text, Action<SkiaShape> transform) => new SkiaStack
    {
        Spacing = 6,
        WidthRequest = 120,
        Padding = new Thickness(0, 12),
        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                Type = ShapeType.Rectangle,
                CornerRadius = 10,
                BackgroundColor = Color.Parse("#0D6EFD"),
                WidthRequest = 96,
                HeightRequest = 56,
                HorizontalOptions = LayoutOptions.Center,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel("DrawnUI") { FontSize = 14, FontFamily = "FontTextBold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
                },
            }.Adapt(transform),
            new SkiaLabel(text) { FontSize = 12, TextColor = Color.Parse("#ADB5BD"), HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
        },
    };

    private static SkiaLabel Title(string text) => new(text)
    {
        FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase, HorizontalOptions = LayoutOptions.Fill,
    };

    private static SkiaControl Card(SkiaLabel title, params SkiaControl[] content) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#2B3035"),
        HorizontalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 10,
                Padding = new Thickness(16, 12),
                HorizontalOptions = LayoutOptions.Fill,
                Children = new SkiaControl[] { title }.Concat(content).ToList(),
            },
        },
    };
}
