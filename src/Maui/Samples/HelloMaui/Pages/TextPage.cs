using DrawnUi.Draw;
using DrawnUi.Views;

namespace HelloMaui.Pages;

/// <summary>
/// SkiaLabel text engine: wrapping, MaxLines, alignment, spacing, weights, transforms, spans,
/// markdown, stroke/shadow and glyph fallback. Ported from the React demo's TextPage.tsx.
/// </summary>
public class TextPage : SkiaLayer
{
    private const string Markdown = """
        # Heading 1
        ## Heading 2
        ### Heading 3
        A paragraph with **bold**, *italic*, ~~strikethrough~~, `inline code` and a [tappable link](https://drawnui.net).
        Soft line breaks stay inside the paragraph.

        - Bullet item with **bold**
        - Second bullet
        1. Numbered item
        2. Another one, *emphasised*

        ```
        var label = new SkiaRichLabel();
        label.Text = "# Hello";
        ```
        """;

    private const string Lorem =
        "DrawnUI draws every pixel itself: text is shaped and rasterized by Skia, so a label wraps by words, respects MaxLines with an ellipsis, aligns horizontally and vertically, and never leaves the canvas for a native view. This paragraph is long on purpose so it wraps across several lines at whatever width the layout gives it.";

    private static readonly Color Body = Color.Parse("#DEE2E6");
    private static readonly Color Muted = Color.Parse("#ADB5BD");

    private SkiaLabel _tapped;
    private SkiaLabel _link;

    /// <summary>Builds the page.</summary>
    public TextPage()
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
                    // An auto-width (Center) Column on the C# engine takes the width of its non-Fill
                    // children only; with every child Fill it adopts the constraint, capped here at 720,
                    // and centres — so the title below is Fill with centred text rather than a Center label.
                    HorizontalOptions = LayoutOptions.Center,
                    MaximumWidthRequest = 720,
                    UseCache = SkiaCacheType.Operations,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel("SkiaLabel") { FontSize = 24, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },

                        Card("Word wrap · HorizontalOptions=Fill",
                            new SkiaLabel(Lorem) { FontSize = 15, TextColor = Body, HorizontalOptions = LayoutOptions.Fill }),

                        Card("Spans — TextSpan children: color, size, bold, italic, underline, strikeout, background, Tapped",
                            new SkiaLabel
                            {
                                FontSize = 16,
                                TextColor = Body,
                                HorizontalOptions = LayoutOptions.Fill,
                                FontFamilyFallback = "FontSymbols",
                                Spans =
                                {
                                    new TextSpan { Text = "One label, many styles: " },
                                    new TextSpan { Text = "bold", IsBold = true },
                                    new TextSpan { Text = ", " },
                                    new TextSpan { Text = "italic", IsItalic = true },
                                    new TextSpan { Text = ", " },
                                    new TextSpan { Text = "colored", TextColor = Color.Parse("#FFC107") },
                                    new TextSpan { Text = ", " },
                                    new TextSpan { Text = "bigger", FontSize = 22, TextColor = Color.Parse("#20C997") },
                                    new TextSpan { Text = ", " },
                                    new TextSpan { Text = "underlined", Underline = true },
                                    new TextSpan { Text = ", " },
                                    new TextSpan { Text = "struck out", Strikeout = true },
                                    new TextSpan { Text = ", " },
                                    new TextSpan { Text = " highlighted ", BackgroundColor = Color.Parse("#6610F2"), TextColor = Colors.White },
                                    new TextSpan { Text = " and a " },
                                    Link("tappable link ", () => _tapped.Text = $"Last span tap: link tapped at {DateTime.Now:T}"),
                                    // Its own span: a span has one typeface, so the arrow must not share one
                                    // with latin text — it gets the symbols face directly.
                                    Link("→", () => _tapped.Text = $"Last span tap: link tapped at {DateTime.Now:T}", "FontSymbols"),
                                    new TextSpan { Text = " that wraps with the rest of the paragraph like any other word." },
                                },
                            },
                            new SkiaLabel("Last span tap: nothing yet") { FontSize = 13, TextColor = Muted }.Assign(out _tapped)),

                        Card("SkiaRichLabel — markdown in Text, rendered as spans",
                            new SkiaRichLabel(Markdown)
                            {
                                FontSize = 15,
                                TextColor = Body,
                                HorizontalOptions = LayoutOptions.Fill,
                                FontFamilyFallback = "FontSymbols,FontSymbols2",
                            }.Adapt(me => me.LinkTapped += (_, url) => _link.Text = $"Last link tapped: {url}"),
                            new SkiaLabel("Last link tapped: none") { FontSize = 13, TextColor = Muted }.Assign(out _link)),

                        Card("MaxLines=2 · TailTruncation (default)",
                            new SkiaLabel(Lorem) { FontSize = 15, TextColor = Body, HorizontalOptions = LayoutOptions.Fill, MaxLines = 2 }),

                        Card("LineSpacing=1.6",
                            new SkiaLabel(Lorem) { FontSize = 14, TextColor = Body, HorizontalOptions = LayoutOptions.Fill, LineSpacing = 1.6, MaxLines = 3 }),

                        Card("HorizontalTextAlignment Start / Center / End",
                            new SkiaWrap
                            {
                                Spacing = 12,
                                Children = new List<SkiaControl>
                                {
                                    Aligned("Start aligned text wraps inside its own column", DrawTextAlignment.Start),
                                    Aligned("Center aligned text wraps inside its own column", DrawTextAlignment.Center),
                                    Aligned("End aligned text wraps inside its own column", DrawTextAlignment.End),
                                },
                            }),

                        Card("VerticalTextAlignment in a 90pt box",
                            new SkiaWrap
                            {
                                Spacing = 12,
                                Children = new List<SkiaControl>
                                {
                                    Boxed("Start", TextAlignment.Start),
                                    Boxed("Center", TextAlignment.Center),
                                    Boxed("End", TextAlignment.End),
                                },
                            }),

                        Card("StrokeColor / StrokeWidth, StrokeGradient, DropShadow* — outline under the fill, shadow below (C# DrawText order)",
                            new SkiaWrap
                            {
                                Spacing = 16,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaLabel("Outlined")
                                    {
                                        FontSize = 32, FontFamily = "FontTextBold", TextColor = Color.Parse("#212529"),
                                        StrokeColor = Color.Parse("#FFC107"), StrokeWidth = 1.5,
                                    },
                                    new SkiaLabel("Gradient stroke")
                                    {
                                        FontSize = 32, FontFamily = "FontTextBold", TextColor = Color.Parse("#212529"),
                                        StrokeColor = Colors.White, StrokeWidth = 2,
                                        StrokeGradient = Linear(0, "#0DCAF0", "#D63384"),
                                    },
                                    new SkiaLabel("Drop shadow")
                                    {
                                        FontSize = 32, FontFamily = "FontTextBold", TextColor = Body,
                                        DropShadowColor = Colors.Black, DropShadowSize = 2, DropShadowOffsetX = 3, DropShadowOffsetY = 3,
                                    },
                                    new SkiaLabel("Both + gradient fill")
                                    {
                                        FontSize = 32, FontFamily = "FontTextBold", TextColor = Colors.White,
                                        FillGradient = Linear(90, "#FFC107", "#FD7E14"),
                                        StrokeColor = Color.Parse("#3D2B00"), StrokeWidth = 1,
                                        DropShadowColor = Color.Parse("#66000000"), DropShadowSize = 3, DropShadowOffsetX = 2, DropShadowOffsetY = 4,
                                    },
                                },
                            }),

                        Card("FontFamilyFallback — symbols and emoji the text font lacks",
                            new SkiaLabel("Arrows ← ↑ → ↓ ⇒ ⇔  math ∑ ∞ ≈ ≠ ≤ ≥ √  misc ♥ ★ ✓ ✗ ⚠ via FontFamilyFallback=\"FontSymbols,FontSymbols2\"")
                            {
                                FontSize = 16, TextColor = Body, FontFamilyFallback = "FontSymbols,FontSymbols2", HorizontalOptions = LayoutOptions.Fill,
                            },
#if WINDOWS
                            new SkiaLabel("Emoji 😀 😎 🤖 😂 👍 🙌 via FontFamilyFallback=\"FontEmoji\" (Noto Color Emoji faces + hands subset)")
                            {
                                FontSize = 16, TextColor = Body, FontFamilyFallback = "FontEmoji", HorizontalOptions = LayoutOptions.Fill,
                            },
#else
                            // No FontEmoji face on this platform yet (see MauiProgram.cs): AutoFont switches the label to
                            // the system emoji face of its first glyph (Apple Color Emoji, Noto Color Emoji), so the emoji
                            // get a label of their own.
                            new SkiaRow
                            {
                                Spacing = 8,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaLabel("Emoji") { FontSize = 16, TextColor = Body, VerticalOptions = LayoutOptions.Center },
                                    new SkiaLabel("😀 😎 🤖 😂 👍 🙌") { FontSize = 16, TextColor = Body, AutoFont = true, VerticalOptions = LayoutOptions.Center },
                                    new SkiaLabel("via the system font (AutoFont=true picks the system emoji font)") { FontSize = 16, TextColor = Body, VerticalOptions = LayoutOptions.Center },
                                },
                            },
#endif
                            // A glyph no font has becomes FallbackCharacter, a space by default.
                            new SkiaLabel("Without a fallback the same arrow → and emoji 😀 are drawn as blank spaces")
                            {
                                FontSize = 16, TextColor = Muted, HorizontalOptions = LayoutOptions.Fill,
                            }),

                        Card("FontAttributes / FontWeight (weights registered via ConfigureFonts)",
                            new SkiaLabel("Regular 400 — the family default") { FontSize = 16, TextColor = Body },
                            new SkiaLabel("FontAttributes=Bold → nearest registered weight (600 Semibold)") { FontSize = 16, TextColor = Body, FontAttributes = FontAttributes.Bold, FontFamilyFallback = "FontSymbols,FontSymbols2" },
                            new SkiaLabel("FontAttributes=Italic → synthetic skew when no italic face") { FontSize = 16, TextColor = Body, FontAttributes = FontAttributes.Italic, FontFamilyFallback = "FontSymbols,FontSymbols2" },
                            new SkiaLabel("FontAttributes=BoldItalic") { FontSize = 16, TextColor = Body, FontAttributes = FontAttributes.Bold | FontAttributes.Italic },
                            new SkiaLabel("FontWeight=600 explicit") { FontSize = 16, TextColor = Body, FontWeight = 600 }),

                        Card("TextTransform · NoWrap · Padding",
                            new SkiaLabel("uppercase transform applied at layout time") { FontSize = 14, TextColor = Body, TextTransform = TextTransform.Uppercase },
                            new SkiaLabel("Titlecase transform applied at layout time") { FontSize = 14, TextColor = Body, TextTransform = TextTransform.Titlecase },
                            new SkiaLabel("LineBreakMode=NoWrap keeps this on one line even when it is far too long for the card width, so it simply runs past the edge")
                            {
                                FontSize = 14, TextColor = Body, LineBreakMode = LineBreakMode.NoWrap, HorizontalOptions = LayoutOptions.Fill,
                            },
                            new SkiaLabel("Padding=(12, 6) + background")
                            {
                                FontSize = 14, TextColor = Colors.White, BackgroundColor = Color.Parse("#0D6EFD"), Padding = new Thickness(12, 6),
                            }),

                        Card("Multiline text with explicit line breaks",
                            new SkiaLabel("Line one\nLine two is a bit longer\nLine three")
                            {
                                FontSize = 14, TextColor = Body, HorizontalTextAlignment = DrawTextAlignment.Center, HorizontalOptions = LayoutOptions.Fill,
                            }),
                    },
                },
            }.Fill(),
        };
    }

    /// <summary>A titled card, the React page's Card component.</summary>
    private static SkiaControl Card(string title, params SkiaControl[] content) => new SkiaShape
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 8,
        BackgroundColor = Color.Parse("#2B3035"),
        HorizontalOptions = LayoutOptions.Fill,
        UseCache = SkiaCacheType.Image,
        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 8,
                Padding = new Thickness(16, 12),
                Children = new List<SkiaControl>
                {
                    new SkiaLabel(title)
                    {
                        FontSize = 12,
                        TextColor = Color.Parse("#6EA8FE"),
                        FontAttributes = FontAttributes.Bold,
                        TextTransform = TextTransform.Uppercase,
                    },
                }.Concat(content).ToList(),
            },
        },
    };

    /// <summary>A tappable span — TextSpan exposes Tapped as an event, so it is wired in a factory.</summary>
    private static TextSpan Link(string text, Action onTapped, string fontFamily = null)
    {
        var span = new TextSpan { Text = text, TextColor = Color.Parse("#6EA8FE"), Underline = true, FontFamily = fontFamily };
        span.Tapped += (_, _) => onTapped();
        return span;
    }

    private static SkiaLabel Aligned(string text, DrawTextAlignment alignment) => new(text)
    {
        FontSize = 13,
        TextColor = Body,
        WidthRequest = 215,
        HorizontalTextAlignment = alignment,
        BackgroundColor = Color.Parse("#22FFFFFF"),
        Padding = new Thickness(6),
    };

    private static SkiaLabel Boxed(string text, TextAlignment vertical) => new(text)
    {
        FontSize = 14,
        TextColor = Body,
        WidthRequest = 215,
        HeightRequest = 90,
        VerticalTextAlignment = vertical,
        HorizontalTextAlignment = DrawTextAlignment.Center,
        BackgroundColor = Color.Parse("#22FFFFFF"),
    };

    private static SkiaGradient Linear(double angle, params string[] colors) => new()
    {
        Type = GradientType.Linear,
        Angle = angle,
        Colors = colors.Select(Color.Parse).ToList(),
    };
}
