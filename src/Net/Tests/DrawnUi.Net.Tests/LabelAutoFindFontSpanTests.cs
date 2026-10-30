using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A span with AutoFindFont (emoji in a text label) is measured with the font it is drawn with from the first measure on.
/// Its font was found only after the span's paint was set up, so the first measure used the label's text font: a label
/// measured once (inside a scroll) drew the emoji run wider than its slot, over the text after it.
/// </summary>
public class LabelAutoFindFontSpanTests
{
    private readonly ITestOutputHelper _out;
    public LabelAutoFindFontSpanTests(ITestOutputHelper o) { _out = o; }

    const string Emoji = "🌐🚒🙎🏽👻🤖";

    static SkiaLabel Label() => new()
    {
        FontSize = 15,
        TextColor = Colors.White,
        HorizontalOptions = LayoutOptions.Fill,
        Spans =
        {
            new TextSpan { Text = "Use colored emojis " },
            new TextSpan { Text = Emoji, AutoFindFont = true },
            new TextSpan { Text = " Format text as" },
        }
    };

    static float EmojiWidth(SkiaLabel label) =>
        label.Lines.SelectMany(l => l.Spans).Where(s => s.Text == Emoji).Sum(s => s.Size.Width);

    [Fact]
    public void EmojiSpan_InAScroll_MeasuredWithTheEmojiFont()
    {
        SkiaLabel plain = null, scrolled = null;
        using var host = new HeadlessCanvasHost(1300, 400, scale: 2f, background: Colors.Black);
        host.Canvas.Content = new SkiaStack
        {
            Children =
            {
                new SkiaStack { HorizontalOptions = LayoutOptions.Center, MaximumWidthRequest = 620, Children = { Label().Assign(out plain) } },
                new SkiaScroll
                {
                    HeightRequest = 60,
                    Content = new SkiaStack { HorizontalOptions = LayoutOptions.Center, MaximumWidthRequest = 620, Children = { Label().Assign(out scrolled) } }
                },
            }
        };
        host.AdvanceFrames(6);

        var expected = EmojiWidth(plain);
        var actual = EmojiWidth(scrolled);
        _out.WriteLine($"emoji run: plain {expected}, in a scroll {actual}");
        Assert.True(expected > 0);
        Assert.Equal(expected, actual, 1);
    }
}
