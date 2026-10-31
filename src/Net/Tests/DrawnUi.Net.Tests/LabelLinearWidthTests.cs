using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Text is measured with unhinted advances, so a label has the same width on every platform and in every DrawnUI engine
/// (DrawnUi.Rust and DrawnUi.React measure the same numbers with the same font).
/// </summary>
[Collection("FontRegistration")] // registers fonts: never in parallel with another class that does
public class LabelLinearWidthTests
{
    static LabelLinearWidthTests()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Path.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;
        SkiaFontManager.Instance.RegisterFont("LinearTestText", Path.Combine(dir!.FullName, @"Tests\PreviewTests\Resources\Fonts\OpenSans-Regular.ttf"));
        SkiaFontManager.Instance.Initialize();
    }

    static float Width(SkiaLabel label)
    {
        var host = new HeadlessCanvasHost(600, 200, scale: 1f, background: Colors.Black);
        label.FontFamily = "LinearTestText";
        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { label }
        };
        host.AdvanceFrames(3);
        return Assert.Single(label.Lines).Width;
    }

    [Fact]
    public void Letter_SameWidthAsOtherEngines()
    {
        Assert.Equal(14.756, Width(new SkiaLabel("H") { FontSize = 20 }), 2);
    }

    [Fact]
    public void Line_SameWidthAsOtherEngines()
    {
        Assert.Equal(171.344, Width(new SkiaLabel("One label, many styles:") { FontSize = 16 }), 2);
    }

    [Fact]
    public void Spans_SameWidthAsOneText()
    {
        var spans = Width(new SkiaLabel
        {
            FontSize = 16,
            Spans = { new TextSpan { Text = "One label, " }, new TextSpan { Text = "many styles:" } }
        });

        Assert.Equal(171.344, spans, 2);
    }
}
