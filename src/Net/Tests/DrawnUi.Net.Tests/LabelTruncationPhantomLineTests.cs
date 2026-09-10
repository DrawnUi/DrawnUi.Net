using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Regression for issue #338 (commit fb47b272): MaxLines=1 + TailTruncation on unbreakable text
/// reported LinesCount=2 (phantom empty line after the truncation exit), doubling ContentSize and
/// defeating VerticalTextAlignment=Center. Mirrors FastRepro/Issue338Page.
/// </summary>
public class LabelTruncationPhantomLineTests
{
    private static SkiaLabel Subject(string text) => new SkiaLabel
    {
        Text = text,
        FontSize = 17,
        MaxLines = 1,
        LineBreakMode = LineBreakMode.TailTruncation,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalTextAlignment = DrawTextAlignment.Center,
        HeightRequest = 50,
        HorizontalOptions = LayoutOptions.Fill,
    };

    [Theory]
    [InlineData("Demo.App.Very.Long.Namespace.Without.Any.Break.Opportunity.At.All.Truncate.Me")]
    [InlineData("Demo App with spaces that is also far too long to fit into the label width given")]
    [InlineData("Demo.App")]
    public void MaxLines1_TailTruncation_ReportsOneLine(string text)
    {
        using var host = new HeadlessCanvasHost(300, 100, scale: 1f, background: Colors.Black);
        var label = Subject(text);
        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { label }
        };

        for (int i = 0; i < 3; i++) host.RenderFrame(16);

        Assert.Equal(1, label.LinesCount);
        Assert.True(label.MeasuredLineHeight > 0, "line height not measured");
        Assert.True(label.ContentSize.Pixels.Height <= label.MeasuredLineHeight * 1.5f,
            $"ContentSize {label.ContentSize.Pixels.Height} spans more than one line of {label.MeasuredLineHeight}");
    }
}
