using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The room a label's outline (StrokeWidth) and drop shadow need is added once per line: a text split into several
/// spans (a rich label's markdown runs: "!" is its own run, mixed styles) is as wide as the same text in one span.
/// </summary>
[Collection("FontRegistration")] // registers fonts: never in parallel with another class that does
public class LabelSpanEffectsSpacingTests
{
    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Path.Exists(Path.Combine(dir.FullName, ".git"))) // a worktree has a .git file
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    static LabelSpanEffectsSpacingTests()
    {
        SkiaFontManager.Instance.RegisterFont("SpanEffectsText", RepoFile(@"Tests\PreviewTests\Resources\Fonts\OpenSans-Regular.ttf"));
        SkiaFontManager.Instance.Initialize();
    }

    static T Title<T>(T label) where T : SkiaLabel
    {
        label.FontFamily = "SpanEffectsText";
        label.FontSize = 30;
        label.StrokeWidth = 2;
        label.StrokeColor = Colors.Black;
        label.DropShadowColor = Colors.Black;
        label.DropShadowOffsetY = 2;
        label.DropShadowSize = 1;
        return label;
    }

    static TextLine[] Lines(SkiaLabel label, float width)
    {
        using var host = new HeadlessCanvasHost((int)width, 300);
        label.HorizontalOptions = LayoutOptions.Fill;
        host.Canvas.Content = label;
        host.AdvanceFrames(3);
        return label.Lines;
    }

    [Fact]
    public void A_rich_label_with_an_outline_is_as_wide_as_a_plain_one()
    {
        var plain = Lines(Title(new SkiaLabel("BRICKS BREAKER!")), 600);
        var rich = Lines(Title(new SkiaRichLabel("BRICKS BREAKER!")), 600);

        Assert.Single(plain);
        Assert.Single(rich);
        Assert.True(rich[0].Spans.Count > 1, "the markdown runs make several spans");
        Assert.Equal(plain[0].Width, rich[0].Width, 0.5f);
    }

    [Fact]
    public void Spans_with_an_outline_sit_as_close_as_one_span()
    {
        var one = Lines(Title(new SkiaLabel("LEVEL 1 COMPLETE!")), 600);
        var two = Title(new SkiaLabel());
        two.Spans.Add(new TextSpan { Text = "LEVEL 1 COMPLETE" });
        two.Spans.Add(new TextSpan { Text = "!" });
        var split = Lines(two, 600);

        Assert.Equal(one[0].Width, split[0].Width, 0.5f);
    }
}
