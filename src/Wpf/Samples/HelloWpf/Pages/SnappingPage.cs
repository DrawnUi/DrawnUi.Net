using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>Recycled carousel slide: visuals built once, <see cref="SetContent"/> runs on every rebind.</summary>
public class SlideCell : SkiaDynamicDrawnCell
{
    private SkiaShape _shape;
    private SkiaLabel _label;
    private SkiaLabel _sub;

    /// <summary>Builds the slide.</summary>
    public SlideCell()
    {
        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                Type = ShapeType.Rectangle,
                CornerRadius = 12,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel { FontSize = 22, FontFamily = "FontTextBold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }.Assign(out _label),
                    new SkiaLabel { FontSize = 12, TextColor = Color.Parse("#AAFFFFFF"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 0, 10) }.Assign(out _sub),
                },
            }.Assign(out _shape),
        };
    }

    /// <inheritdoc/>
    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);
        if (ctx is SnappingPage.Slide s)
        {
            _shape.BackgroundColor = Color.Parse(s.Color);
            _label.Text = s.Title;
            _sub.Text = $"recycled cell · index {ContextIndex}";
            if (s.Height > 0)
                HeightRequest = s.Height;
        }
    }
}

/// <summary>
/// SkiaCarousel + SkiaDrawer: SnappingLayout descendants — swipe / drag, snap by velocity,
/// programmatic state. Ported from the React demo's SnappingPage.tsx.
/// </summary>
public class SnappingPage : SkiaLayer
{
    /// <summary>A carousel slide.</summary>
    public record Slide(string Title, string Color, double Height = 0);

    private static readonly (string Title, string Color, Color Text)[] Slides =
    {
        ("1", "#E94560", Colors.White), ("2", "#0F3460", Colors.White), ("3", "#533483", Colors.White), ("4", "#A8DF8E", Color.Parse("#1A1A2E")),
    };

    private static readonly (string Title, string Color)[] Peek =
    {
        ("Slide 1", "#0D6EFD"), ("Slide 2", "#6610F2"), ("Slide 3", "#D63384"), ("Slide 4", "#20C997"),
    };

    private static readonly string[] LoopColors = { "#0D6EFD", "#6610F2", "#D63384", "#20C997", "#FD7E14", "#0DCAF0" };

    private static readonly List<Slide> LoopItems = Enumerable.Range(0, 12).Select(i => new Slide($"Item {i + 1}", LoopColors[i % 6])).ToList();

    private static readonly List<Slide> DynItems = new() { new("80 pt", "#0D6EFD", 80), new("160 pt", "#6610F2", 160), new("110 pt", "#20C997", 110) };

    private static readonly Color Muted = Color.Parse("#ADB5BD");

    private SkiaCarousel _carousel;
    private SkiaCarousel _loop;
    private SkiaDrawer _drawer;
    private SkiaLabel _status;
    private SkiaLabel _speedLabel;
    private SkiaLabel _peekTitle;
    private SkiaLabel _loopTitle;
    private SkiaLabel _dynTitle;
    private SkiaLabel _openLabel;
    private SkiaButton _openButton;
    private SkiaButton _sidesButton;
    private readonly List<SkiaShape> _dots = new();
    private readonly List<SkiaButton> _speedButtons = new();
    private readonly Dictionary<string, SkiaButton> _toggles = new();

    private bool _inTransition;
    private string _appeared = "";
    private double _speed = 1;

    /// <summary>Builds the page.</summary>
    public SnappingPage()
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
                    Padding = new Thickness(16, 16, 16, 260),
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 720,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("Carousel & Drawer") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },

                        Card("SkiaCarousel playground (Sandbox MainPageCarousels)",
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    Toggle("IsLooped", false, on => _carousel.IsLooped = on),
                                    Toggle("Bounces", false, on => _carousel.Bounces = on),
                                    Toggle("PreloadNeighboors", true, on => _carousel.PreloadNeighboors = on),
                                    Toggle("IsVertical", false, on => _carousel.IsVertical = on),
                                    new SkiaButton("SidesOffset: 40") { BackgroundColor = Color.Parse("#495057"), FontSize = 13 }
                                        .Assign(out _sidesButton)
                                        .OnTapped(me =>
                                        {
                                            var next = _carousel.SidesOffset == 40 ? 0 : _carousel.SidesOffset == 0 ? 20 : 40;
                                            _carousel.SidesOffset = next;
                                            me.Text = $"SidesOffset: {next}";
                                        }),
                                },
                            },
                            new SkiaWrap
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("← Prev") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _carousel.GoPrev()),
                                    new SkiaButton("Next →") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _carousel.GoNext()),
                                    new SkiaButton("SelectedIndex = 2") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _carousel.SelectedIndex = 2),
                                    new SkiaButton("Index 0, no anim") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => { _carousel.SelectedIndex = 0; _carousel.ApplyIndex(true); }),
                                    new SkiaButton("Set index 3") { BackgroundColor = Color.Parse("#0F3460"), FontSize = 13 }.OnTapped(me => _carousel.SelectedIndex = 3),
                                },
                            },
                            new SkiaCarousel
                            {
                                HeightRequest = 250,
                                HorizontalOptions = LayoutOptions.Fill, // a SkiaCarousel auto-sizes to its slides otherwise
                                BackgroundColor = Color.Parse("#16213E"),
                                IsLooped = false,
                                Bounces = false,
                                SwipeSpeed = 1,
                                SidesOffset = 40,
                                Spacing = 20,
                                PreloadNeighboors = true,
                                IsVertical = false,
                                SelectedIndex = 0,
                                // Slides are plain children here: on the C# engine a SkiaShape auto-sizes to
                                // its label unless it fills the slot the carousel offers.
                                Children = Slides.Select(s => (SkiaControl)new SkiaShape
                                {
                                    Type = ShapeType.Rectangle, BackgroundColor = Color.Parse(s.Color), UseCache = SkiaCacheType.Operations,
                                    HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                    Children = { new SkiaLabel(s.Title) { FontSize = 60, FontFamily = "FontTextBold", TextColor = s.Text, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                                }).ToList(),
                            }
                            .Assign(out _carousel)
                            .Adapt(me =>
                            {
                                me.SelectedIndexChanged += (_, i) => UpdateStatus();
                                me.TransitionChanged += (_, t) => { _inTransition = t; UpdateStatus(); };
                                me.ItemAppearing += (_, i) => { _appeared = $"ItemAppearing {i}"; UpdateStatus(); };
                                me.ItemDisappearing += (_, i) => { _appeared = $"ItemDisappearing {i}"; UpdateStatus(); };
                            }),
                            // indicators: the selected dot stretches to 24 like the DataTrigger in the sandbox
                            new SkiaRow
                            {
                                Spacing = 8,
                                HorizontalOptions = LayoutOptions.Center,
                                Children = Slides.Select((s, i) => (SkiaControl)new SkiaShape
                                {
                                    Type = ShapeType.Rectangle, CornerRadius = 4, WidthRequest = i == 0 ? 24 : 8, HeightRequest = 8, BackgroundColor = Color.Parse(s.Color),
                                }.Adapt(d => _dots.Add(d))).ToList(),
                            },
                            new SkiaLabel { FontSize = 13, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill }.Assign(out _status),
                            new SkiaRow
                            {
                                Spacing = 8,
                                VerticalOptions = LayoutOptions.Center,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaLabel("Swipe Speed") { FontSize = 14, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center },
                                }
                                .Concat(new[] { 0.5, 1, 2 }.Select(v => (SkiaControl)new SkiaButton($"{v:0.0}x") { FontSize = 13 }
                                    .Adapt(b => _speedButtons.Add(b))
                                    .OnTapped(me => SetSpeed(v))))
                                .Append(new SkiaLabel("Current: 1.0x") { FontSize = 12, TextColor = Muted, VerticalOptions = LayoutOptions.Center }.Assign(out _speedLabel))
                                .ToList(),
                            }),

                        Card(null,
                            new SkiaLabel("SidesOffset=40 Spacing=12 — neighbours peek in · SelectedIndex=1") { FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase }.Assign(out _peekTitle),
                            new SkiaCarousel
                            {
                                HeightRequest = 140, HorizontalOptions = LayoutOptions.Fill, SidesOffset = 40, Spacing = 12, SelectedIndex = 1,
                                Children = Peek.Select(s => (SkiaControl)new SkiaShape
                                {
                                    Type = ShapeType.Rectangle, CornerRadius = 12, BackgroundColor = Color.Parse(s.Color),
                                    HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
                                    Children = { new SkiaLabel(s.Title) { FontSize = 20, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } },
                                }).ToList(),
                            }.Adapt(me => me.SelectedIndexChanged += (_, i) => _peekTitle.Text = $"SidesOffset=40 Spacing=12 — neighbours peek in · SelectedIndex={i}")),

                        Card(null,
                            new SkiaLabel("IsLooped + ItemsSource/ItemTemplate (12 recycled cells) · SelectedIndex=0") { FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase }.Assign(out _loopTitle),
                            new SkiaCarousel
                            {
                                HeightRequest = 130, IsLooped = true, SidesOffset = 30, Spacing = 10, LinearSpeedMs = 350,
                                ItemsSource = LoopItems, ItemTemplate = new DataTemplate(() => new SlideCell()), SelectedIndex = 0,
                            }
                            .Assign(out _loop)
                            .Adapt(me => me.SelectedIndexChanged += (_, i) => _loopTitle.Text = $"IsLooped + ItemsSource/ItemTemplate (12 recycled cells) · SelectedIndex={i}"),
                            new SkiaRow
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Prev") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _loop.GoPrev()),
                                    new SkiaButton("Next") { BackgroundColor = Color.Parse("#0D6EFD") }.OnTapped(me => _loop.GoNext()),
                                    new SkiaLabel("Wraps last to first both ways (virtual anchors); LinearSpeedMs=350 = one slide per 350 ms without Bounces; cells are recycled through ItemTemplate.")
                                    {
                                        FontSize = 12, TextColor = Muted, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill,
                                    },
                                },
                            }),

                        Card(null,
                            new SkiaLabel("DynamicSize — auto height follows the selected slide · SelectedIndex=0") { FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase }.Assign(out _dynTitle),
                            new SkiaCarousel
                            {
                                DynamicSize = true, VerticalOptions = LayoutOptions.Start, Bounces = true,
                                ItemsSource = DynItems, ItemTemplate = new DataTemplate(() => new SlideCell()), SelectedIndex = 0,
                            }.Adapt(me => me.SelectedIndexChanged += (_, i) => _dynTitle.Text = $"DynamicSize — auto height follows the selected slide · SelectedIndex={i}"),
                            new SkiaLabel("No HeightRequest: the carousel measures the selected cell (80 / 160 / 110 pt) and re-measures on every index change.")
                            {
                                FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card("SkiaDrawer — drag the header below, or:",
                            new SkiaRow
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaButton("Open drawer") { BackgroundColor = Color.Parse("#6610F2") }.Assign(out _openButton).OnTapped(me => _drawer.IsOpen = !_drawer.IsOpen),
                                    new SkiaLabel("IsOpen: False") { FontSize = 14, TextColor = Color.Parse("#DEE2E6"), VerticalOptions = LayoutOptions.Center }.Assign(out _openLabel),
                                },
                            },
                            new SkiaLabel("Direction=FromBottom HeaderSize=56, sits in a SkiaLayer with VerticalOptions=End; snaps by velocity, Bounces enabled.")
                            {
                                FontSize = 12, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                            }),
                    },
                },
            }.Fill(),

            // the drawer lives in its own full-size layer over the page, anchored to the bottom edge
            new SkiaLayer
            {
                VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaDrawer
                    {
                        Direction = DrawerDirection.FromBottom,
                        HeaderSize = 56,
                        HeightRequest = 320,
                        VerticalOptions = LayoutOptions.End,
                        HorizontalOptions = LayoutOptions.Fill,
                        Bounces = true,
                        Children = new List<SkiaControl>
                        {
                            new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = new CornerRadius(20, 20, 0, 0),
                                BackgroundColor = Color.Parse("#F5F5F5"),
                                HorizontalOptions = LayoutOptions.Fill,
                                VerticalOptions = LayoutOptions.Fill,
                                Shadows = { new SkiaShadow { X = 0, Y = -2, Blur = 8, Opacity = 0.4, Color = Colors.Black } },
                                Children = new List<SkiaControl>
                                {
                                    new SkiaStack
                                    {
                                        Spacing = 0,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        Children = new List<SkiaControl>
                                        {
                                            // top corners only, like a MAUI CornerRadius="20,20,0,0"
                                            new SkiaShape
                                            {
                                                Type = ShapeType.Rectangle, CornerRadius = new CornerRadius(20, 20, 0, 0), BackgroundColor = Color.Parse("#0D6EFD"), HeightRequest = 56, HorizontalOptions = LayoutOptions.Fill,
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaShape { Type = ShapeType.Rectangle, CornerRadius = 3, BackgroundColor = Colors.White, WidthRequest = 44, HeightRequest = 5, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 8, 0, 0) },
                                                    new SkiaLabel("Drag me") { FontSize = 16, FontFamily = "FontTextBold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 10, 0, 0) },
                                                },
                                            },
                                            new SkiaStack
                                            {
                                                Spacing = 12,
                                                Padding = new Thickness(20),
                                                Children = new List<SkiaControl>
                                                {
                                                    new SkiaLabel("Drawer content") { FontSize = 20, FontFamily = "FontTextBold", TextColor = Color.Parse("#111827") },
                                                    new SkiaLabel("Everything inside is a normal drawn tree: buttons keep working, the drawer only takes vertical drags. Release with a flick to snap open or closed.")
                                                    {
                                                        FontSize = 14, TextColor = Color.Parse("#374151"), HorizontalOptions = LayoutOptions.Fill,
                                                    },
                                                    new SkiaButton("Close") { ControlStyle = PrebuiltControlStyle.Material }.OnTapped(me => _drawer.Close()),
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    }
                    .Assign(out _drawer)
                    .Adapt(me => me.IsOpenChanged += (_, open) =>
                    {
                        _openLabel.Text = $"IsOpen: {open}";
                        _openButton.Text = open ? "Close drawer" : "Open drawer";
                    }),
                },
            },
        };

        UpdateStatus();
        SetSpeed(1);
    }

    private void UpdateStatus()
    {
        var index = _carousel.SelectedIndex;
        for (var i = 0; i < _dots.Count; i++)
            _dots[i].WidthRequest = i == index ? 24 : 8;

        _status.Text = $"Selected Index: {index}   ·   InTransition: {_inTransition}   ·   {(_carousel.IsLooped ? "Looping enabled - infinite scroll" : "Looping disabled - bounded scroll")}   ·   {_appeared}";
    }

    private void SetSpeed(double speed)
    {
        _speed = speed;
        _carousel.SwipeSpeed = speed;
        var values = new[] { 0.5, 1, 2 };
        for (var i = 0; i < _speedButtons.Count; i++)
            _speedButtons[i].BackgroundColor = Color.Parse(values[i] == speed ? "#533483" : "#495057");
        _speedLabel.Text = $"Current: {speed:0.0}x";
    }

    /// <summary>An on/off button that applies its state to the playground carousel.</summary>
    private SkiaControl Toggle(string text, bool initial, Action<bool> apply)
    {
        var on = initial;
        void Paint(SkiaButton b)
        {
            b.Text = $"{text}: {(on ? "On" : "Off")}";
            b.BackgroundColor = Color.Parse(on ? "#20C997" : "#495057");
            b.TextColor = on ? Color.Parse("#1A1A2E") : Colors.White;
        }

        return new SkiaButton { FontSize = 13 }
            .Adapt(Paint)
            .OnTapped(me =>
            {
                on = !on;
                apply(on);
                Paint(me);
                UpdateStatus();
            });
    }

    /// <summary>A titled card, the React page's Card component. Null title = caller supplies its own.</summary>
    private static SkiaControl Card(string title, params SkiaControl[] content) => new SkiaShape
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
                Children = (title == null
                    ? Enumerable.Empty<SkiaControl>()
                    : new SkiaControl[]
                    {
                        new SkiaLabel(title) { FontSize = 12, TextColor = Color.Parse("#6EA8FE"), FontAttributes = FontAttributes.Bold, TextTransform = TextTransform.Uppercase },
                    })
                    .Concat(content).ToList(),
            },
        },
    };
}
