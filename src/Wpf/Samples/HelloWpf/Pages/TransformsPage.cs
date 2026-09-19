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
                        new SkiaLabel("Applied at render around the arranged box, so layout is untouched and caches stay valid. Same names as MAUI: TranslationX/Y, Rotation, ScaleX/Y, SkewX/Y, AnchorX/Y, Opacity.")
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
                                    Tile("Scale=1.3", s => { s.ScaleX = 1.3; s.ScaleY = 1.3; }),
                                    Tile("ScaleX=-1", s => s.ScaleX = -1),
                                    Tile("SkewX=20", s => s.SkewX = 20),
                                    Tile("TranslationY=10", s => s.TranslationY = 10),
                                    Tile("Opacity=0.35", s => s.Opacity = 0.35),
                                    Tile("Rotation=15 AnchorX/Y=0", s => { s.Rotation = 15; s.AnchorX = 0; s.AnchorY = 0; }),
                                },
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
                                    new SkiaButton("Scale") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _ = Scale()),
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
    }

    private async Task Fade()
    {
        await _logo.FadeToAsync(0.15, 300);
        await _logo.FadeToAsync(1, 300);
    }

    private async Task Scale()
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
