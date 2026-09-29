using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Japanese and Chinese have no spaces: WordWrap must break between ideographs and kana (never before 、。ー」 or small
/// kana, never after 「), and a Latin word wider than the line (a URL) breaks by characters instead of overflowing.
/// </summary>
public class LabelCjkWrapTests
{
    private readonly ITestOutputHelper _out;
    public LabelCjkWrapTests(ITestOutputHelper o) { _out = o; }

    static readonly string[] JapaneseFonts =
    {
        @"C:\Dev\Cases\GitHub\FiltersCamera\src\app\Resources\Fonts\IBMPlexSansJP-Regular.ttf",
        @"C:\Windows\Fonts\NotoSansJP-VF.ttf",
    };

    const string Japanese = "背景の部屋をどう見せるかを選びます。今のプリセットは下のスイッチでオフにできます。";

    private TextLine[] Layout(string text, LineBreakMode mode, int maxLines, float width, string fontFamily = null)
    {
        using var host = new HeadlessCanvasHost(400, 400, scale: 1f, background: Colors.Black);
        var label = new SkiaLabel
        {
            Text = text,
            FontSize = 16,
            WidthRequest = width,
            LineBreakMode = mode,
            MaxLines = maxLines,
        };
        if (fontFamily != null)
            label.FontFamily = fontFamily;

        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { label }
        };
        for (var i = 0; i < 3; i++) host.RenderFrame(16);

        foreach (var line in label.Lines)
            _out.WriteLine($"{line.Width,6:0} '{line.Value}'");
        return label.Lines;
    }

    private static string JapaneseFont()
    {
        var path = JapaneseFonts.FirstOrDefault(File.Exists);
        if (path == null)
            return null;

        SkiaFontManager.Instance.RegisterFont("FontJapaneseTest", path);
        SkiaFontManager.Instance.Initialize();
        return "FontJapaneseTest";
    }

    [Fact]
    public void Japanese_WordWrap_BreaksBetweenCharacters_WithKinsoku()
    {
        var font = JapaneseFont();
        if (font == null)
            return; // no Japanese font on this machine, nothing to prove

        var lines = Layout(Japanese, LineBreakMode.WordWrap, -1, 300, font);

        Assert.InRange(lines.Length, 2, 4);
        Assert.All(lines, l => Assert.True(l.Width <= 301, $"line wider than the label: {l.Width} '{l.Value}'"));
        Assert.All(lines.Skip(1), l => Assert.DoesNotContain(l.Value[0], "、。ー」』）っゃゅょッャュョ"));
        Assert.Equal(Japanese, string.Concat(lines.Select(l => l.Value)));
    }

    [Fact]
    public void Japanese_AfterALatinWord_FillsTheLine()
    {
        var font = JapaneseFont();
        if (font == null)
            return;

        var text = "DrawnCamera " + Japanese;
        var lines = Layout(text, LineBreakMode.WordWrap, -1, 300, font);

        Assert.StartsWith("DrawnCamera 背景", lines[0].Value);
        Assert.All(lines, l => Assert.True(l.Width <= 301, $"line wider than the label: {l.Width} '{l.Value}'"));
    }

    [Fact]
    public void Japanese_TailTruncation_KeepsMaxLines()
    {
        var font = JapaneseFont();
        if (font == null)
            return;

        var lines = Layout(Japanese + Japanese, LineBreakMode.TailTruncation, 2, 300, font);

        Assert.Equal(2, lines.Length);
        Assert.All(lines, l => Assert.True(l.Width <= 301, $"line wider than the label: {l.Width} '{l.Value}'"));
    }

    [Fact]
    public void LongLatinWord_WordWrap_BreaksByCharacters()
    {
        const string url = "https://drawnui.net/articles/a/very/long/path/without/any/space/that/cannot/fit/on/one/line";
        var lines = Layout("See " + url, LineBreakMode.WordWrap, -1, 200);

        Assert.Equal("See", lines[0].Value.Trim());
        Assert.True(lines.Length > 2, "the URL was not broken");
        Assert.All(lines, l => Assert.True(l.Width <= 201, $"line wider than the label: {l.Width} '{l.Value}'"));
        Assert.Equal(url, string.Concat(lines.Skip(1).Select(l => l.Value)));
    }

    [Theory]
    [InlineData("背景", 1, true)]
    [InlineData("部屋。", 2, false)]   // never before 。
    [InlineData("「今", 1, false)]     // never after 「
    [InlineData("ャッ", 1, false)]     // never before small kana
    [InlineData("ab", 1, false)]       // Latin keeps breaking at spaces
    [InlineData("aの", 1, true)]
    public void BreakOpportunities(string text, int index, bool expected)
    {
        Assert.Equal(expected, SkiaLabel.CanBreakInsideWord(text, index));
    }
}
