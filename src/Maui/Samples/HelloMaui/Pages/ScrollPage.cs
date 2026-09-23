using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;

namespace HelloMaui.Pages;

/// <summary>
/// SkiaScroll features: Header (flow / sticky / behind + parallax), Footer, scroll bars, pull to
/// refresh, SnapToChildren, TrackIndexPosition. Ported from the React demo's ScrollPage.tsx.
/// </summary>
public class ScrollPage : SkiaLayer
{
    private static readonly string[] Palette = { "#0F3460", "#533483", "#1B4332", "#7B2D26", "#495057", "#0D6EFD", "#D63384", "#2D6A4F" };

    private SkiaScroll _refreshScroll;
    private SkiaLabel _refreshTitle;
    private SkiaLabel _snapTitle;
    private SkiaLabel _trackTitle;

    /// <summary>Builds the page.</summary>
    public ScrollPage()
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
                    UseCache = SkiaCacheType.Operations,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("SkiaScroll") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },

                        Card(Title("Header + Footer in the flow · Header / Footer properties scroll with the content"),
                            new SkiaScroll
                            {
                                HeightRequest = 240,
                                BackgroundColor = Color.Parse("#212529"),
                                IgnoreWrongDirection = true,
                                Header = new SkiaLayer
                                {
                                    HeightRequest = 70,
                                    BackgroundColor = Color.Parse("#0F3460"),
                                    Children = { new SkiaLabel("Header (70 pt) · scrolls away") { FontSize = 16, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                                },
                                Content = Rows(14, "Row"),
                                Footer = new SkiaLayer
                                {
                                    HeightRequest = 50,
                                    BackgroundColor = Color.Parse("#533483"),
                                    Children = { new SkiaLabel("Footer (50 pt) · after the content") { FontSize = 14, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                                },
                            }),

                        Card(Title("HeaderSticky · the header stays at the top, drawn over the content"),
                            new SkiaScroll
                            {
                                HeightRequest = 220,
                                BackgroundColor = Color.Parse("#212529"),
                                HeaderSticky = true,
                                IgnoreWrongDirection = true,
                                Header = new SkiaLayer
                                {
                                    HeightRequest = 44,
                                    BackgroundColor = Color.Parse("#0D6EFD"),
                                    Children = { new SkiaLabel("Sticky header") { FontSize = 15, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                                },
                                Content = Rows(14, "Under sticky"),
                            }),

                        Card(Title("HeaderBehind + HeaderParallaxRatio=0.5 · the content covers the header, which moves at half speed"),
                            new SkiaScroll
                            {
                                HeightRequest = 260,
                                BackgroundColor = Color.Parse("#212529"),
                                HeaderBehind = true,
                                HeaderParallaxRatio = 0.5,
                                ContentOffset = -24,
                                IgnoreWrongDirection = true,
                                Header = new SkiaLayer
                                {
                                    HeightRequest = 160,
                                    Children =
                                    {
                                        new SkiaImage { Source = "images/baboon.jpg", Aspect = TransformAspect.AspectCover, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill },
                                        new SkiaLabel("Parallax cover") { FontSize = 22, FontFamily = "FontTextBold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, DropShadowColor = Colors.Black, DropShadowSize = 4 },
                                    },
                                },
                                Content = new SkiaShape
                                {
                                    Type = ShapeType.Rectangle,
                                    CornerRadius = new CornerRadius(24, 24, 0, 0),
                                    BackgroundColor = Color.Parse("#2B3035"),
                                    HorizontalOptions = LayoutOptions.Fill,
                                    Children = { Rows(12, "Content over the cover") },
                                },
                            }),

                        Card(Title("ScrollBarsVisibility=Vertical + ScrollBarThumbColor · default SkiaScrollBar, auto-hides 1 s after scrolling"),
                            new SkiaScroll
                            {
                                HeightRequest = 200,
                                BackgroundColor = Color.Parse("#212529"),
                                ScrollBarsVisibility = ScrollBarVisibility.Vertical,
                                ScrollBarThumbColor = Color.Parse("#6EA8FE"),
                                ScrollBarTrackColor = Color.Parse("#22FFFFFF"),
                                IgnoreWrongDirection = true,
                                Content = Rows(16, "Scrollbar row"),
                            },
                            new SkiaScroll
                            {
                                Orientation = ScrollOrientation.Horizontal,
                                HeightRequest = 70,
                                BackgroundColor = Color.Parse("#212529"),
                                ScrollBarsVisibility = ScrollBarVisibility.Horizontal,
                                ScrollBarThumbColor = Color.Parse("#FFC107"),
                                Content = new SkiaRow
                                {
                                    Spacing = 8,
                                    Padding = new Thickness(8),
                                    Children = Enumerable.Range(0, 14).Select(i => (SkiaControl)new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle, CornerRadius = 6, WidthRequest = 120, HeightRequest = 50, BackgroundColor = Color.Parse(Palette[i % Palette.Length]), UseCache = SkiaCacheType.Operations,
                                        Children = { new SkiaLabel($"H {i + 1}") { FontSize = 13, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                                    }).ToList(),
                                },
                            }),

                        Card(Title(RefreshTitle("pull down past 60 pt")).Assign(out _refreshTitle),
                            new SkiaScroll
                            {
                                HeightRequest = 220,
                                BackgroundColor = Color.Parse("#212529"),
                                RefreshEnabled = true,
                                RefreshDistanceLimit = 60,
                                IgnoreWrongDirection = true,
                                RefreshIndicator = new RefreshIndicator
                                {
                                    HeightRequest = 50,
                                    Children =
                                    {
                                        new SkiaShape
                                        {
                                            Type = ShapeType.Rectangle, CornerRadius = 20, BackgroundColor = Color.Parse("#0D6EFD"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, WidthRequest = 160, HeightRequest = 36,
                                            Children = { new SkiaLabel("refresh") { FontSize = 14, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                                        },
                                    },
                                },
                                Content = Rows(12, "Pull down"),
                            }
                            .Assign(out _refreshScroll)
                            .Adapt(me => me.RefreshCommand = new Command(StartRefresh)),
                            // Nested in a scrolling page the outer scroll wins a downward pan on this engine
                            // (harness-verified: standalone refreshes, nested does not), so the same command is
                            // reachable from a button too.
                            new SkiaButton("IsRefreshing = true") { BackgroundColor = Color.Parse("#0D6EFD"), FontSize = 13 }
                                .OnTapped(me => _refreshScroll.IsRefreshing = true)),

                        Card(Title(SnapTitle(-1)).Assign(out _snapTitle),
                            new SkiaScroll
                            {
                                Orientation = ScrollOrientation.Horizontal,
                                HeightRequest = 120,
                                BackgroundColor = Color.Parse("#212529"),
                                SnapToChildren = SnapToChildrenType.Center,
                                TrackIndexPosition = RelativePositionType.Center,
                                Content = new SkiaRow
                                {
                                    Spacing = 12,
                                    Padding = new Thickness(8),
                                    Children = Enumerable.Range(0, 10).Select(i => (SkiaControl)new SkiaShape
                                    {
                                        Type = ShapeType.Rectangle, CornerRadius = 10, WidthRequest = 200, HeightRequest = 100, BackgroundColor = Color.Parse(Palette[i % Palette.Length]), UseCache = SkiaCacheType.Operations,
                                        Children = { new SkiaLabel($"Snap {i}") { FontSize = 18, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                                    }).ToList(),
                                },
                            }
                            .Adapt(me => me.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(SkiaScroll.CurrentIndex)) _snapTitle.Text = SnapTitle(me.CurrentIndex); })),

                        Card(Title(TrackTitle(-1)).Assign(out _trackTitle),
                            new SkiaScroll
                            {
                                HeightRequest = 180,
                                BackgroundColor = Color.Parse("#212529"),
                                TrackIndexPosition = RelativePositionType.Start,
                                SnapToChildren = SnapToChildrenType.Side,
                                IgnoreWrongDirection = true,
                                Content = Rows(16, "Tracked"),
                            }
                            .Adapt(me => me.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(SkiaScroll.CurrentIndex)) _trackTitle.Text = TrackTitle(me.CurrentIndex); })),
                    },
                },
            }.Fill(),
        };
    }

    private async void StartRefresh()
    {
        _refreshTitle.Text = RefreshTitle("refreshing… (2 s)");
        await Task.Delay(2000);
        _refreshScroll.IsRefreshing = false;
        _refreshTitle.Text = RefreshTitle("done · pull again");
    }

    private static string RefreshTitle(string state) => $"RefreshEnabled + RefreshIndicator · RefreshDistanceLimit 60 · pull down inside the list (page not scrolling) or use the button · {state}";

    private static string SnapTitle(int index) => $"SnapToChildren=Center + TrackIndexPosition=Center (horizontal) · CurrentIndex {index}";

    private static string TrackTitle(int index) => $"TrackIndexPosition=Start (vertical) · CurrentIndex {index} · SnapToChildren=Side";

    private static SkiaStack Rows(int count, string prefix) => new()
    {
        Spacing = 6,
        Padding = new Thickness(8),
        HorizontalOptions = LayoutOptions.Fill,
        Children = Enumerable.Range(0, count).Select(i => (SkiaControl)new SkiaShape
        {
            Type = ShapeType.Rectangle,
            CornerRadius = 6,
            BackgroundColor = Color.Parse(Palette[i % Palette.Length]),
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 40,
            UseCache = SkiaCacheType.Operations,
            Children = { new SkiaLabel($"{prefix} {i + 1}") { FontSize = 13, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(12, 0) } },
        }).ToList(),
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
