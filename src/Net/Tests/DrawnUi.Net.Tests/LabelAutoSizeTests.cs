using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// SkiaLabel.AutoSize. FitHorizontal only shrinks the font so the text fits the label width, and must come back
/// to FontSize when the text gets shorter or the box wider: it used to keep the smallest size it ever reached,
/// because the size the AutoSize loop wrote into FontDefault was hidden from the SetupDefaultPaint guard.
/// The other modes are pinned to the results they gave before that fix.
/// </summary>
public class LabelAutoSizeTests
{
    const string LongText = "This text is way too long to fit";
    const string ShortText = "Hi";
    const float Width = 200;

    static (HeadlessCanvasHost host, SkiaLabel label) Render(AutoSizeType mode, string text,
        double heightRequest = 50, int maxLines = 0, LineBreakMode lineBreak = LineBreakMode.WordWrap)
    {
        var host = new HeadlessCanvasHost(700, 300, scale: 1f, background: Colors.Black);
        var label = new SkiaLabel
        {
            Text = text,
            FontSize = 40,
            AutoSize = mode,
            WidthRequest = Width,
            HeightRequest = heightRequest,
            MaxLines = maxLines,
            LineBreakMode = lineBreak,
            TextColor = Colors.White,
        };
        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { label }
        };
        Frames(host);
        return (host, label);
    }

    static void Frames(HeadlessCanvasHost host)
    {
        for (int i = 0; i < 4; i++) host.RenderFrame(16);
    }

    /// <summary>
    /// Results every mode gave on a fresh label before the FitHorizontal fix (200pt wide, FontSize 40).
    /// heightRequest -1 = the 300pt canvas height.
    /// </summary>
    [Theory]
    [InlineData(AutoSizeType.None, LongText, 50, 0, 40.0, 1)]
    [InlineData(AutoSizeType.None, LongText, -1, 0, 40.0, 3)]
    [InlineData(AutoSizeType.None, ShortText, 50, 0, 40.0, 1)]
    [InlineData(AutoSizeType.FitFillHorizontal, LongText, 50, 0, 15.4, 1)]
    [InlineData(AutoSizeType.FitFillHorizontal, LongText, -1, 1, 15.4, 1)]
    [InlineData(AutoSizeType.FitFillHorizontal, ShortText, 50, 0, 207.5, 1)]
    [InlineData(AutoSizeType.FitFillVertical, LongText, 50, 0, 19.1, 2)]
    [InlineData(AutoSizeType.FitFillVertical, LongText, -1, 0, 45.3, 4)]
    [InlineData(AutoSizeType.FitFillVertical, ShortText, 50, 0, 40.0, 1)]
    [InlineData(AutoSizeType.FitFillVertical, ShortText, -1, 0, 111.7, 1)]
    [InlineData(AutoSizeType.FitHorizontal, LongText, 50, 0, 15.4, 1)]
    [InlineData(AutoSizeType.FitHorizontal, LongText, -1, 1, 15.4, 1)]
    [InlineData(AutoSizeType.FitHorizontal, ShortText, 50, 0, 40.0, 1)]
    [InlineData(AutoSizeType.FillHorizontal, LongText, 50, 0, 42.65, 1)] // 42.6 or 42.7 depending on how many measures ran
    [InlineData(AutoSizeType.FillHorizontal, LongText, -1, 0, 42.7, 3)]
    [InlineData(AutoSizeType.FillHorizontal, ShortText, 50, 0, 207.5, 1)]
    [InlineData(AutoSizeType.FitVertical, LongText, 50, 0, 19.1, 2)]
    [InlineData(AutoSizeType.FitVertical, LongText, 50, 1, 15.4, 1)]
    [InlineData(AutoSizeType.FitVertical, LongText, -1, 0, 40.0, 3)]
    [InlineData(AutoSizeType.FitVertical, ShortText, 50, 0, 40.0, 1)]
    [InlineData(AutoSizeType.FillVertical, LongText, 50, 0, 40.0, 1)]
    [InlineData(AutoSizeType.FillVertical, LongText, -1, 0, 45.3, 4)]
    [InlineData(AutoSizeType.FillVertical, ShortText, -1, 0, 111.7, 1)]
    public void FreshLabel_KeepsPinnedResult(AutoSizeType mode, string text, double heightRequest, int maxLines,
        double font, int lines)
    {
        var (host, label) = Render(mode, text, heightRequest, maxLines);
        using (host)
        {
            Assert.Equal(font, label.UsingFontSize, 0.06);
            Assert.Equal(lines, label.LinesCount);
        }
    }

    /// <summary>
    /// FitFillVertical with MaxLines=1 never finished measuring: growing made the text wrap past the one line,
    /// cut, shrink, room again, grow... forever, under the lock every label shares. It ends at the largest size
    /// that fits, as FitVertical does.
    /// </summary>
    [Fact]
    public void FitFillVertical_MaxLinesOne_Finishes()
    {
        var measured = Task.Run(() =>
        {
            var (host, label) = Render(AutoSizeType.FitFillVertical, LongText, 50, 1);
            using (host)
                return (label.UsingFontSize, label.LinesCount, label.IsCut);
        });

        Assert.True(measured.Wait(TimeSpan.FromSeconds(10)), "measure never finished");
        Assert.Equal(15.4, measured.Result.UsingFontSize, 0.06);
        Assert.Equal(1, measured.Result.LinesCount);
        Assert.False(measured.Result.IsCut);
    }

    /// <summary>
    /// FitFill modes restart from the last size on purpose (the faster mode for changing text): pinned too.
    /// </summary>
    [Theory]
    [InlineData(AutoSizeType.FitFillHorizontal, 207.5, 15.4)]
    [InlineData(AutoSizeType.FitFillVertical, 40.0, 19.1)]
    public void FitFill_TextChange_KeepsPinnedResult(AutoSizeType mode, double shortFont, double longFont)
    {
        var (host, label) = Render(mode, ShortText);
        using (host)
        {
            Assert.Equal(shortFont, label.UsingFontSize, 0.06);
            label.Text = LongText;
            Frames(host);
            Assert.Equal(longFont, label.UsingFontSize, 0.06);
        }
    }

    [Fact]
    public void FitHorizontal_LongText_ShrinksToFitWidth()
    {
        var (host, label) = Render(AutoSizeType.FitHorizontal, LongText);
        using (host)
        {
            Assert.True(label.UsingFontSize < 40, $"font {label.UsingFontSize}");
            Assert.Equal(1, label.LinesCount);
            Assert.False(label.IsCut);
            Assert.True(label.ContentSize.Pixels.Width <= Width + 1, $"text {label.ContentSize.Pixels.Width} wide");
        }
    }

    [Fact]
    public void FitHorizontal_ShortText_KeepsFontSize()
    {
        var (host, label) = Render(AutoSizeType.FitHorizontal, ShortText);
        using (host)
        {
            Assert.Equal(40, label.UsingFontSize, 0.06);
        }
    }

    [Fact]
    public void FitHorizontal_ShorterText_GrowsBackToFontSize()
    {
        var (host, label) = Render(AutoSizeType.FitHorizontal, LongText);
        using (host)
        {
            Assert.True(label.UsingFontSize < 40);
            label.Text = ShortText;
            Frames(host);
            Assert.Equal(40, label.UsingFontSize, 0.06);
            label.Text = LongText;
            Frames(host);
            Assert.Equal(15.4, label.UsingFontSize, 0.06);
        }
    }

    [Fact]
    public void FitHorizontal_WiderBox_GrowsBackToFontSize()
    {
        var (host, label) = Render(AutoSizeType.FitHorizontal, LongText);
        using (host)
        {
            Assert.True(label.UsingFontSize < 40);
            label.WidthRequest = 600; // the text is 521px wide at 40
            Frames(host);
            Assert.Equal(40, label.UsingFontSize, 0.06);
            Assert.Equal(1, label.LinesCount);
        }
    }

    /// <summary>
    /// NoWrap keeps an overflowing line whole: the line count never changed, so the text was never shrunk.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FitHorizontal_NoWrap_ShrinksToFitWidth(int maxLines)
    {
        var (host, label) = Render(AutoSizeType.FitHorizontal, LongText, 50, maxLines, LineBreakMode.NoWrap);
        using (host)
        {
            Assert.Equal(1, label.LinesCount);
            Assert.True(label.ContentSize.Pixels.Width <= Width + 1, $"text {label.ContentSize.Pixels.Width} wide");

            // nothing drawn right of the box
            using var image = host.Snapshot();
            using var pixmap = image.PeekPixels();
            for (int x = (int)Width + 2; x < 700; x += 2)
            for (int y = 0; y < 60; y++)
                Assert.True(pixmap.GetPixelColor(x, y).Red < 40, $"ink at {x},{y}");
        }
    }
}
