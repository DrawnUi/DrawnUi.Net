using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The allocations of a frame of a static 2D card page (the Add3dGallery gallery page with its 3D pieces hidden: a
/// header, a scroll of cards with titles, stages and buttons, rows of buttons, an FPS label, an animator keeping the
/// frames coming), redrawn every frame as on the web heads. Before the per-frame allocation fixes such a frame made
/// about 22 KB (render tree records and lists, copied drawing parameters, boxed structs, radii arrays, path builders
/// for clips, children snapshots); now about 1.5 KB.
/// </summary>
public class CardPageFrameAllocationTests
{
    private readonly ITestOutputHelper _out;
    public CardPageFrameAllocationTests(ITestOutputHelper o) { _out = o; }

    [Fact]
    public void A_redrawn_static_card_page_allocates_little_per_frame()
    {
        using var host = new HeadlessCanvasHost(1280, 2000, 1f, CardTheme.Page);
        host.Canvas.Content = CardPage.Build();
        host.AdvanceFrames(120, 16.7);

        const int frames = 120;
        var bytes = new long[frames];
        for (var i = 0; i < frames; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            host.RenderFrame(16.7);
            bytes[i] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Array.Sort(bytes);
        var median = bytes[frames / 2];
        _out.WriteLine($"allocated per frame: median {median} B, max {bytes[^1]} B");
        Assert.True(median < 4096, $"a redrawn static page allocated {median} B per frame");
    }
}

static class CardTheme
{
    public static readonly Color Page = Color.FromRgb(14, 16, 24);
    public static readonly Color Header = Color.FromRgb(19, 21, 32);
    public static readonly Color Card = Color.FromRgb(24, 27, 40);
    public static readonly Color Line = Color.FromRgb(40, 44, 62);
    public static readonly Color Stage = Color.FromRgb(42, 47, 70);
    public static readonly Color Ink = Color.FromRgb(242, 241, 248);
    public static readonly Color Muted = Color.FromRgb(154, 152, 176);
    public static readonly Color Accent = Color.FromRgb(124, 108, 242);
    public static readonly Color Quiet = Color.FromRgb(52, 56, 78);
    public const string Title = "FontTextTitle";

    public static Color Lighter(Color c, float amount) =>
        Color.FromRgba(c.Red + (1 - c.Red) * amount, c.Green + (1 - c.Green) * amount, c.Blue + (1 - c.Blue) * amount, c.Alpha);

    public static SkiaControl Section(string title, string caption) => new SkiaStack
    {
        Spacing = 4,
        HorizontalOptions = LayoutOptions.Center,
        Margin = new Thickness(0, 12, 0, 0),
        Children =
        {
            new SkiaLabel(title) { FontSize = 22, FontFamily = Title, TextColor = Ink, HorizontalOptions = LayoutOptions.Center, AccessibilityRole = Aria.RoleHeading },
            new SkiaLabel(caption) { FontSize = 14, TextColor = Muted, HorizontalOptions = LayoutOptions.Center },
        }
    };
}

sealed class CardButton : SkiaShape
{
    readonly SkiaLabel _caption;
    Color _fill;
    bool _hovered;

    public CardButton(string text, Color fill, double width = 150)
    {
        _fill = fill;
        CornerRadius = 10;
        WidthRequest = width;
        HeightRequest = 40;
        BackgroundColor = fill;
        AccessibilityRole = Aria.RoleButton;
        AccessibilityLabel = text;
        Children = new List<SkiaControl>
        {
            new SkiaLabel(text) { FontSize = 14, FontFamily = CardTheme.Title, TextColor = CardTheme.Ink, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }.Assign(out _caption),
        };
        this.OnHovered((me, on) =>
        {
            _hovered = on;
            BackgroundColor = _hovered ? CardTheme.Lighter(_fill, 0.14f) : _fill;
        });
    }
}

static class CardPage
{
    static SkiaShape Tile(string title, string caption) => new()
    {
        WidthRequest = 300,
        UseCache = SkiaCacheType.None,
        CornerRadius = 16,
        BackgroundColor = CardTheme.Card,
        StrokeColor = CardTheme.Line,
        StrokeWidth = 1,
        Padding = new Thickness(16),
        Children =
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
                            new SkiaLabel(title) { FontSize = 18, FontFamily = CardTheme.Title, TextColor = CardTheme.Ink, AccessibilityRole = Aria.RoleHeading },
                            new SkiaLabel(caption) { FontSize = 13, TextColor = CardTheme.Muted, HeightRequest = 36 },
                        }
                    },
                    new SkiaShape
                    {
                        CornerRadius = 12,
                        UseCache = SkiaCacheType.None,
                        BackgroundColor = CardTheme.Stage,
                        HorizontalOptions = LayoutOptions.Fill,
                        HeightRequest = 190,
                        // the hidden 3D piece
                        Children = { new SkiaLayer { IsVisible = false, VerticalOptions = LayoutOptions.Fill } },
                    },
                    new CardButton("Pause", CardTheme.Accent).WithHorizontalOptions(LayoutOptions.Center),
                }
            }
        }
    };

    public static SkiaControl Build()
    {
        var root = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = CardTheme.Page,
            Children =
            {
                new SkiaGrid
                    {
                        RowSpacing = 0,
                        VerticalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            new SkiaLayer
                            {
                                BackgroundColor = CardTheme.Header, HeightRequest = 64, Padding = new Thickness(24, 0), UseCache = SkiaCacheType.Image,
                                Children =
                                {
                                    new SkiaLabel("DrawnUI 3D") { FontSize = 17, TextColor = CardTheme.Ink, VerticalOptions = LayoutOptions.Center },
                                    new SkiaRow
                                    {
                                        Spacing = 8, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center,
                                        Children = { new CardButton("Gallery", CardTheme.Accent, 120), new CardButton("Boss fight", CardTheme.Quiet, 120) }
                                    },
                                }
                            }.SetGrid(0, 0),
                            new SkiaScroll
                            {
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
                                            Spacing = 6, UseCache = SkiaCacheType.Image,
                                            Children =
                                            {
                                                new SkiaLabel("3D, made in the app") { FontSize = 30, TextColor = CardTheme.Ink, HorizontalOptions = LayoutOptions.Center },
                                                new SkiaLabel("3D pieces and characters built and animated inside a DrawnUI screen.") { FontSize = 15, TextColor = CardTheme.Muted, HorizontalOptions = LayoutOptions.Center },
                                            }
                                        },
                                        CardTheme.Section("Made in code", "Smooth shapes in real 3D, shaded by a light: a few lines of code each."),
                                        new SkiaWrap
                                        {
                                            Spacing = 16, HorizontalOptions = LayoutOptions.Center,
                                            Children =
                                            {
                                                Tile("Loading", "A coin turning while something loads."),
                                                Tile("Loading dots", "Three dots that bounce one after another."),
                                                Tile("Achievement", "A medal that lights up and spins when unlocked."),
                                                Tile("Success", "A badge that pops in, then its check grows."),
                                                Tile("Like", "A heart that beats, and jumps when liked."),
                                                Tile("Celebration", "Confetti and stars falling: one 3D layer, many pieces."),
                                            }
                                        },
                                        CardTheme.Section("From model files", "Free 3D models with their own animations, driven by the app."),
                                        new SkiaWrap
                                        {
                                            Spacing = 16, HorizontalOptions = LayoutOptions.Center,
                                            Children = { Tile("Avatar", "A character that idles, and waves when tapped."), Tile("Reward", "A chest that opens and closes.") }
                                        },
                                        new SkiaShape
                                        {
                                            UseCache = SkiaCacheType.None, CornerRadius = 16, BackgroundColor = CardTheme.Card, StrokeColor = CardTheme.Line, StrokeWidth = 1,
                                            Padding = new Thickness(16), HorizontalOptions = LayoutOptions.Center,
                                            Children =
                                            {
                                                new SkiaStack
                                                {
                                                    Spacing = 10, HorizontalOptions = LayoutOptions.Start, WidthRequest = 1000,
                                                    Children =
                                                    {
                                                        new SkiaLabel("Mobs on a 2D field") { FontSize = 18, TextColor = CardTheme.Ink },
                                                        new SkiaShape
                                                        {
                                                            WidthRequest = 1000, HeightRequest = 280, UseCache = SkiaCacheType.None, CornerRadius = 10,
                                                            BackgroundColor = Color.FromRgb(58, 98, 74),
                                                            Children = { new SkiaLayer { IsVisible = false, VerticalOptions = LayoutOptions.Fill } },
                                                        },
                                                        new SkiaRow
                                                        {
                                                            Spacing = 8, AccessibilityRole = Aria.RoleRadioGroup,
                                                            Children = { new CardButton("10 mobs", CardTheme.Quiet, 110), new CardButton("40 mobs", CardTheme.Quiet, 110), new CardButton("100 mobs", CardTheme.Accent, 110) }
                                                        },
                                                    }
                                                }
                                            }
                                        },
                                    }
                                }
                            }.SetGrid(0, 1),
                        }
                    }
                    .WithRowDefinitions("Auto,*"),
                new SkiaLabelFps
                {
                    Margin = new Thickness(0, 0, 8, 8), VerticalOptions = LayoutOptions.End, HorizontalOptions = LayoutOptions.End,
                    FontSize = 12, BackgroundColor = Color.FromRgb(139, 0, 0), TextColor = Colors.White, UseCache = SkiaCacheType.GPU, ZIndex = 110,
                },
            }
        };
        // The gallery's game loop: an animator that keeps the frames coming.
        new ActionOnTickAnimator(root, _ => { }).Start();
        return root;
    }
}
