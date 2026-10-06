using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A plain SkiaLabel draws a glyph its font lacks with the first FontFamilyFallback font that has it,
/// the rest of the text keeps the label's font. FontFamilyFallback can list several aliases.
/// </summary>
[Collection("FontRegistration")] // registers fonts: never in parallel with another class that does
public class LabelGlyphFallbackTests
{
    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Path.Exists(Path.Combine(dir.FullName, ".git"))) // a worktree has a .git file
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    static LabelGlyphFallbackTests()
    {
        SkiaFontManager.Instance.RegisterFont("FallbackTestText", RepoFile(@"Tests\PreviewTests\Resources\Fonts\OpenSans-Regular.ttf"));
        SkiaFontManager.Instance.RegisterFont("FallbackTestMath", RepoFile(@"src\Blazor\DrawnUi\wwwroot\fonts\NotoSansMathSymbols-Subset.ttf"));
        SkiaFontManager.Instance.RegisterFont("FallbackTestSym2", RepoFile(@"src\Blazor\DrawnUi\wwwroot\fonts\NotoSansSymbols2-Subset.ttf"));
        SkiaFontManager.Instance.RegisterFont("FallbackTestEmoji", RepoFile(@"src\Wpf\Samples\HelloWpf\fonts\NotoColorEmoji-Subset-COLRv0.ttf"));
        SkiaFontManager.Instance.Initialize();
    }

    private static SkiaLabel Render(string text, string fallback) => Render(text, fallback, out _);

    private static SkiaLabel Render(string text, string fallback, out HeadlessCanvasHost host)
    {
        host = new HeadlessCanvasHost(500, 200, scale: 1f, background: Colors.Black);
        var label = new SkiaLabel
        {
            Text = text,
            FontSize = 16,
            FontFamily = "FallbackTestText",
            FontFamilyFallback = fallback,
        };
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { label }
        };
        host.AdvanceFrames(3);
        return label;
    }

    private static SKTypeface Face(string alias) => SkiaFontManager.Instance.GetFont(alias);

    [Fact]
    public void SymbolInsideText_DrawnWithFallbackFont()
    {
        var label = Render("Go → and ★ done", "FallbackTestMath, FallbackTestSym2");

        var line = Assert.Single(label.Lines);
        Assert.Equal("Go → and ★ done", line.Value);

        var math = line.Spans.Single(s => s.Text == "→");
        var star = line.Spans.Single(s => s.Text == "★");
        Assert.Equal(Face("FallbackTestMath").FamilyName, math.Span.TypeFace.FamilyName);
        Assert.Equal(Face("FallbackTestSym2").FamilyName, star.Span.TypeFace.FamilyName);
        Assert.Equal(Face("FallbackTestText").FamilyName, line.Spans.First().Span.TypeFace.FamilyName);
    }

    [Fact]
    public void WithoutFallback_MissingGlyphStaysFallbackCharacter()
    {
        var label = Render("Go → done", null);

        Assert.DoesNotContain("→", Assert.Single(label.Lines).Value); // dropped, as before
    }

    [Fact]
    public void NothingMissing_KeepsPlainPath()
    {
        var label = Render("Total and done", "FallbackTestMath");

        Assert.All(Assert.Single(label.Lines).Spans, s => Assert.Null(s.Span));
    }

    [Fact]
    public void ColrV0Emoji_DrawnInColor() // the hello apps' FontEmoji on Windows: the COLRv1 original draws 0 px there
    {
        var label = Render("😀 🙂", "FallbackTestEmoji", out var host);

        var emoji = Assert.Single(label.Lines).Spans.First(s => s.Text.Contains("😀"));
        Assert.Equal(Face("FallbackTestEmoji").FamilyName, emoji.Span.TypeFace.FamilyName);
        using var bitmap = SKBitmap.FromImage(host.Snapshot());
        var colored = bitmap.Pixels.Count(c => Math.Max(c.Red, Math.Max(c.Green, c.Blue)) - Math.Min(c.Red, Math.Min(c.Green, c.Blue)) > 40);
        Assert.True(colored > 200, $"colored px: {colored}");
    }
}
