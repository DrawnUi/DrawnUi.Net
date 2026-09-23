using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace HelloMaui.Pages;

/// <summary>
/// SkiaLottie (Skottie) and SkiaGif: AnimatedFramesRenderer descendants. Ported from the React
/// demo's AnimationsPage.tsx.
/// </summary>
public class AnimationsPage : SkiaLayer
{
    private static readonly Color Muted = Color.Parse("#ADB5BD");

    private SkiaLottie _lottie;
    private SkiaLottie _toggled;
    private SkiaGif _gif;
    private SkiaLabel _lottieTitle;
    private SkiaLabel _toggleTitle;
    private SkiaLabel _gifTitle;
    private SkiaButton _toggleButton;
    private readonly List<SkiaButton> _speedButtons = new();
    private bool _isOn;

    /// <summary>Builds the page.</summary>
    public AnimationsPage()
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
                        new SkiaLabel("Lottie & GIF") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },

                        Card(Title("SkiaLottie — Source=\"lottie/shield.json\" Repeat=-1 · loading…").Assign(out _lottieTitle),
                            new SkiaRow
                            {
                                Spacing = 16,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaLottie { Source = "lottie/shield.json", WidthRequest = 160, HeightRequest = 160, Repeat = -1, SpeedRatio = 1 }
                                        .Assign(out _lottie)
                                        .Adapt(me =>
                                        {
                                            // No Success/Error events on SkiaLottie (SkiaGif has them): Started fires once
                                            // the file is parsed and playback begins, which is the load signal here.
                                            me.Started += (_, _) => LottieStatus($"loaded, {Frames(me)} frames, playing");
                                            me.Finished += (_, _) => LottieStatus("Finished");
                                        }),
                                    new SkiaStack
                                    {
                                        Spacing = 8,
                                        VerticalOptions = LayoutOptions.Center,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        Children = new List<SkiaControl>
                                        {
                                            new SkiaWrap
                                            {
                                                Spacing = 8,
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaButton("Start") { BackgroundColor = Color.Parse("#0D6EFD"), FontSize = 13 }.OnTapped(me => _lottie.Start()),
                                                    new SkiaButton("Stop") { BackgroundColor = Color.Parse("#6C757D"), FontSize = 13 }.OnTapped(me => _lottie.Stop()),
                                                    new SkiaButton("Seek(30)") { BackgroundColor = Color.Parse("#6C757D"), FontSize = 13 }.OnTapped(me => { _lottie.Stop(); _lottie.Seek(30); }),
                                                    new SkiaButton("GoToEnd") { BackgroundColor = Color.Parse("#6C757D"), FontSize = 13 }.OnTapped(me => { _lottie.Stop(); _lottie.GoToEnd(); }),
                                                },
                                            },
                                            new SkiaRow
                                            {
                                                Spacing = 8,
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaLabel("SpeedRatio") { FontSize = 13, TextColor = Muted, VerticalOptions = LayoutOptions.Center },
                                                }
                                                .Concat(new[] { 0.5, 1, 2 }.Select(v => (SkiaControl)new SkiaButton($"{v}x") { FontSize = 13 }
                                                    .Adapt(b => _speedButtons.Add(b))
                                                    .OnTapped(me => SetSpeed(v))))
                                                .ToList(),
                                            },
                                            new SkiaLabel("Skottie renders the vector animation every frame into an ImageDoubleBuffered cache; the animator is the RangeAnimator over InPoint..OutPoint.")
                                            {
                                                FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                                            },
                                        },
                                    },
                                },
                            }),

                        Card(Title("ColorTint / Colors — colors replaced in the JSON before parsing (ApplyTint)"),
                            new SkiaRow
                            {
                                Spacing = 12,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaLottie { Source = "lottie/ok.json", WidthRequest = 90, HeightRequest = 90, Repeat = -1 },
                                    new SkiaLottie { Source = "lottie/ok.json", WidthRequest = 90, HeightRequest = 90, Repeat = -1, ColorTint = Color.Parse("#20C997") },
                                    new SkiaLottie { Source = "lottie/ok.json", WidthRequest = 90, HeightRequest = 90, Repeat = -1, Colors = { Color.Parse("#D63384"), Color.Parse("#FFC107") } },
                                    new SkiaLottie { Source = "lottie/shield.json", WidthRequest = 90, HeightRequest = 90, Repeat = -1, ColorTint = Color.Parse("#0DCAF0"), SpeedRatio = 0.5 },
                                },
                            }),

                        Card(Title("IsOn toggle — AutoPlay=false, DefaultFrame=0 / DefaultFrameWhenOn=-1 · IsOn=False").Assign(out _toggleTitle),
                            new SkiaRow
                            {
                                Spacing = 16,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaLottie { Source = "lottie/ok.json", WidthRequest = 90, HeightRequest = 90, AutoPlay = false, DefaultFrame = 0, DefaultFrameWhenOn = -1, IsOn = false }.Assign(out _toggled),
                                    new SkiaButton("IsOn = true") { BackgroundColor = Color.Parse("#6610F2"), VerticalOptions = LayoutOptions.Center }
                                        .Assign(out _toggleButton)
                                        .OnTapped(me =>
                                        {
                                            _isOn = !_isOn;
                                            _toggled.IsOn = _isOn;
                                            _toggleButton.Text = _isOn ? "IsOn = false" : "IsOn = true";
                                            _toggleTitle.Text = $"IsOn toggle — AutoPlay=false, DefaultFrame=0 / DefaultFrameWhenOn=-1 · IsOn={_isOn}";
                                        }),
                                    new SkiaLabel("Stopped animations show DefaultFrame, or DefaultFrameWhenOn (-1 = last frame) when IsOn: the recipe for animated checkboxes.")
                                    {
                                        FontSize = 12, TextColor = Muted, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill,
                                    },
                                },
                            }),

                        Card(Title("SkiaGif — Source=\"images/banana.gif\" Aspect=AspectFitFill · loading…").Assign(out _gifTitle),
                            new SkiaRow
                            {
                                Spacing = 16,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaGif { Source = "images/banana.gif", WidthRequest = 140, HeightRequest = 140, Repeat = -1, BackgroundColor = Color.Parse("#212529") }
                                        .Assign(out _gif)
                                        .Adapt(me =>
                                        {
                                            me.Started += (_, _) => GifStatus($"{me.Animation?.TotalFrames ?? 0} frames, {me.Animation?.DurationMs ?? 0} ms, playing");
                                            me.Finished += (_, _) => GifStatus("Finished");
                                            me.Error += (_, e) => GifStatus($"error: {e.Message}");
                                        }),
                                    new SkiaGif { Source = "images/banana.gif", WidthRequest = 70, HeightRequest = 140, Repeat = -1, SpeedRatio = 2, Aspect = TransformAspect.AspectCover, BackgroundColor = Color.Parse("#212529") },
                                    new SkiaStack
                                    {
                                        Spacing = 8,
                                        VerticalOptions = LayoutOptions.Center,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        Children = new List<SkiaControl>
                                        {
                                            new SkiaWrap
                                            {
                                                Spacing = 8,
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaButton("Start") { BackgroundColor = Color.Parse("#0D6EFD"), FontSize = 13 }.OnTapped(me => _gif.Start()),
                                                    new SkiaButton("Stop") { BackgroundColor = Color.Parse("#6C757D"), FontSize = 13 }.OnTapped(me => _gif.Stop()),
                                                    new SkiaButton("Seek(-1)") { BackgroundColor = Color.Parse("#6C757D"), FontSize = 13 }.OnTapped(me => { _gif.Stop(); _gif.Seek(-1); }),
                                                },
                                            },
                                            new SkiaLabel("Every frame is decoded once; the animator runs over 0..DurationMs and picks the frame by time (GifAnimation).")
                                            {
                                                FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                                            },
                                        },
                                    },
                                },
                            }),
                    },
                },
            }.Fill(),
        };

        SetSpeed(1);
    }

    private static int Frames(SkiaLottie lottie) =>
        lottie.Animation == null ? 0 : (int)Math.Round(lottie.Animation.OutPoint - lottie.Animation.InPoint);

    private void LottieStatus(string status) => _lottieTitle.Text = $"SkiaLottie — Source=\"lottie/shield.json\" Repeat=-1 · {status}";

    private void GifStatus(string status) => _gifTitle.Text = $"SkiaGif — Source=\"images/banana.gif\" Aspect=AspectFitFill · {status}";

    private void SetSpeed(double speed)
    {
        _lottie.SpeedRatio = speed;
        var values = new[] { 0.5, 1, 2 };
        for (var i = 0; i < _speedButtons.Count; i++)
            _speedButtons[i].BackgroundColor = Color.Parse(values[i] == speed ? "#533483" : "#495057");
    }

    private static SkiaLabel Title(string text) => new(text)
    {
        FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase,
    };

    /// <summary>A card whose first child is the (live) title label.</summary>
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
                Children = new SkiaControl[] { title }.Concat(content).ToList(),
            },
        },
    };
}
