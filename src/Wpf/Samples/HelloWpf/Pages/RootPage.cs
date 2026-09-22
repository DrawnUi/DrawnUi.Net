using System.Diagnostics;
using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// Root menu, ported from the React demo's RootPage.tsx: dark body, logo, bold title, then the
/// sample cards flowing in a wrap — two columns when there is room, one otherwise.
/// </summary>
public class RootPage : SkiaLayer
{
    private const double MaxWidth = 820;
    private const double PagePadding = 24;
    private const double Gap = 16;
    private const double TwoColumnThreshold = 640;

    /// <summary>Accent pairs the card titles cycle through, same as the React demo.</summary>
    private static readonly string[][] TitleGradients =
    {
        new[] { "#6EA8FE", "#0D6EFD" },
        new[] { "#D63384", "#FD7E14" },
        new[] { "#20C997", "#0DCAF0" },
        new[] { "#FFC107", "#FD7E14" },
        new[] { "#A98EFF", "#6610F2" },
        new[] { "#0DCAF0", "#6EA8FE" },
    };

    private readonly List<SkiaShape> _cards = new();
    private SkiaStack _content;
    private double _cardWidth;
    private double _contentWidth;

    /// <summary>Raised when a sample card is tapped. Navigation arrives with the next module.</summary>
    public Action<SampleEntry> SampleSelected;

    /// <summary>Builds the page.</summary>
    public RootPage()
    {
        Children = new List<SkiaControl>
        {
            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new SkiaStack
                {
                    Spacing = 24,
                    Padding = new Thickness(PagePadding, 24, PagePadding, 40),
                    HorizontalOptions = LayoutOptions.Center,
                    UseCache = SkiaCacheType.Image,
                    Children = BuildContent(),
                }.Assign(out _content),
            }.Fill(),
        };
    }

    private List<SkiaControl> BuildContent()
    {
        var content = new List<SkiaControl>
        {
            new SkiaSvg
            {
                Source = "images/drawnui.svg",
                WidthRequest = 120,
                LockRatio = 1,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 16, 0, 0),
            },
            new SkiaLabel("DrawnUI for WPF")
            {
                FontSize = 48,
                FontFamily = "FontTextBold",
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
            },
            new SkiaLabel("A UI rendering engine on top of SkiaSharp: layouts, controls, gestures, effects and animations")
            {
                FontSize = 16,
                TextColor = Color.Parse("#ADB5BD"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = DrawTextAlignment.Center,
                Margin = new Thickness(0, -12, 0, 0),
            },
            new SkiaWrap
            {
                Spacing = Gap,
                HorizontalOptions = LayoutOptions.Fill,
                Children = Catalog.Samples.Select(BuildCard).ToList(),
            },
            BuildFooter(),
        };

        return content;
    }

    private SkiaControl BuildCard(SampleEntry sample, int index)
    {
        var gradient = TitleGradients[index % TitleGradients.Length];

        return new SkiaShape
        {
            Type = ShapeType.Rectangle,
            CornerRadius = 12,
            BackgroundColor = Color.Parse("#2B3035"),
            StrokeColor = Color.Parse("#373B3E"),
            StrokeWidth = 1,
            AnimationTapped = SkiaTouchAnimation.Ripple,
            Children = new List<SkiaControl>
            {
                new SkiaStack
                {
                    Spacing = 6,
                    Padding = new Thickness(24, 20, 48, 20),
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel(sample.Title)
                        {
                            FontSize = 22,
                            FontFamily = "FontTextBold",
                            TextColor = Colors.White,
                            FillGradient = new SkiaGradient
                            {
                                Type = GradientType.Linear,
                                StartXRatio = 0, StartYRatio = 0,
                                EndXRatio = 1, EndYRatio = 0,
                                Colors = new List<Color> { Color.Parse(gradient[0]), Color.Parse(gradient[1]) },
                            },
                        },
                        new SkiaLabel(sample.Text)
                        {
                            FontSize = 13,
                            TextColor = Color.Parse("#ADB5BD"),
                        },
                    },
                },
                new SkiaLabel("›")
                {
                    FontSize = 28,
                    TextColor = Color.Parse("#6EA8FE"),
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 0, 20, 0),
                },
            },
        }
        .OnTapped(me => SampleSelected?.Invoke(sample))
        .Adapt(me => _cards.Add(me));
    }

    private SkiaControl BuildFooter()
    {
        return new SkiaLabel
        {
            FontSize = 12,
            TextColor = Color.Parse("#6C757D"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 16, 0, 0),
            Spans =
            {
                new TextSpan { Text = "helloreact.drawnui.net · " },
                BuildRepositorySpan(),
                new TextSpan { Text = " · MIT" },
            },
        };
    }

    /// <summary>The repository fragment: a coloured, tappable span inside the footer label.</summary>
    private static TextSpan BuildRepositorySpan()
    {
        // A span exposes Tapped as an event, so it is wired here rather than in an initializer.
        var span = new TextSpan
        {
            Text = "github.com/DrawnUi/DrawnUi.React",
            TextColor = Color.Parse("#6EA8FE"),
        };

        span.Tapped += (_, _) => OpenRepository();
        return span;
    }

    private static void OpenRepository()
    {
        Process.Start(new ProcessStartInfo("https://github.com/DrawnUi/DrawnUi.React") { UseShellExecute = true });
    }

    /// <summary>
    /// Recomputes the card width from the page's own arranged width — never from a screen metric,
    /// which is the window, not the container. Two columns once the content box has room for them.
    /// </summary>
    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        var scale = RenderingScale <= 0 ? 1 : RenderingScale;
        var widthPoints = DrawingRect.Width / scale;
        if (widthPoints <= 0)
            return;

        // The content box gets an explicit width rather than being auto-sized by Center +
        // MaximumWidthRequest. An auto-sized stack takes the width of its content, so as soon as the
        // cards below are narrowed it collapses onto them and the wrap never has room for a second
        // column — the list silently stays single-column at any window size.
        var contentWidth = Math.Min(MaxWidth, widthPoints);
        var inner = contentWidth - PagePadding * 2;
        if (inner <= 0)
            return;

        if (Math.Abs(contentWidth - _contentWidth) >= 0.5)
        {
            _contentWidth = contentWidth;
            _content.WidthRequest = contentWidth;
        }

        var cardWidth = inner >= TwoColumnThreshold ? (inner - Gap) / 2 : inner;
        if (Math.Abs(cardWidth - _cardWidth) < 0.5)
            return;

        _cardWidth = cardWidth;
        foreach (var card in _cards)
            card.WidthRequest = cardWidth;
    }
}
