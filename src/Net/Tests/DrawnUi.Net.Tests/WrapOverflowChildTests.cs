using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A SkiaWrap child with a fixed size whose own child overflows it (an unclipped circle pushed out by a negative
/// margin) takes its fixed size in the line: the overflow must not widen or heighten its slot (Layouts page,
/// IsClippedToBounds card: three 140 x 70 boxes broke over two lines with a gap above).
/// </summary>
public class WrapOverflowChildTests
{
    private readonly ITestOutputHelper _out;
    public WrapOverflowChildTests(ITestOutputHelper output) => _out = output;

    private static SkiaControl Demo(bool clip) => new SkiaLayer
    {
        WidthRequest = 140,
        HeightRequest = 70,
        BackgroundColor = Colors.DarkSlateGray,
        IsClippedToBounds = clip,
        Children =
        {
            new SkiaShape
            {
                Type = ShapeType.Circle, BackgroundColor = Colors.DeepPink, WidthRequest = 110, LockRatio = 1,
                HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, -30, -30),
            },
        }
    };

    [Fact]
    public void FixedSizeChildren_WithOverflowingContent_ShareOneLine()
    {
        using var host = new HeadlessCanvasHost(840, 300, scale: 1f, background: Colors.Black);
        SkiaLayout wrap = null;
        host.Canvas.Content = new SkiaWrap
        {
            Spacing = 24,
            Children = { Demo(false), Demo(true), Demo(true) },
        }.Assign(out wrap);
        host.AdvanceFrames(3);

        var rects = wrap.Views.Select(v => v.DrawingRect).ToArray();
        foreach (var r in rects)
            _out.WriteLine(r.ToString());
        Assert.All(rects, r => Assert.Equal(rects[0].Top, r.Top, 0.5));
        Assert.Equal(0, rects[0].Top, 1.5); // no blank line above (the layout draws 1 px in)
        Assert.Equal(70, rects[0].Height, 0.5);
    }
}
