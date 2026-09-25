using AppoMobi.Gestures;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// DrawnCamera Adjust: a SkiaSlider below the fold of a vertical SkiaScroll that sits in the star row of a
/// SkiaGrid. Scroll down, press the thumb and start dragging: the scroll must keep its offset while the
/// slider takes the pan (it jumped back to 0 in the app).
/// </summary>
public class SliderInScrollDragTests
{
    private readonly ITestOutputHelper _out;
    public SliderInScrollDragTests(ITestOutputHelper o) { _out = o; }

    /// <summary>Mirrors AdjustPage.SliderRow: title + value label on top, slider below, the label follows End.</summary>
    private static SkiaLayout SliderRow(string title, double value, double min, double max, double step, out SkiaSlider slider, bool doubleTapHandler)
    {
        SkiaLabel valueLabel = null;
        SkiaSlider s;
        var row = new SkiaLayout
        {
            Type = LayoutType.Column,
            Spacing = 6,
            Padding = new Thickness(16, 12, 16, 10),
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaLayer
                {
                    Children =
                    {
                        new SkiaLabel { Text = title, TextColor = Colors.White },
                        new SkiaLabel { Text = value.ToString("0.00"), FontSize = 14, TextColor = Colors.Gray, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center }.Assign(out valueLabel),
                    }
                },
                new SkiaSlider
                {
                    ControlStyle = PrebuiltControlStyle.Windows,
                    Min = min, Max = max, Step = step, End = value,
                    HorizontalOptions = LayoutOptions.Fill,
                }
                .Assign(out s)
                .ObserveSelf((me, prop) =>
                {
                    if (prop != nameof(SkiaSlider.End)) return;
                    valueLabel.Text = me.End.ToString("0.00");
                }),
            }
        };
        if (doubleTapHandler)
        {
            s.WithGestures((me, args, apply) => args.Type != TouchActionResult.Tapped ? null : null);
        }
        slider = s;
        return row;
    }

    private static SkiaShape Card(params SkiaControl[] rows) => new()
    {
        Type = ShapeType.Rectangle,
        CornerRadius = 14,
        BackgroundColor = Colors.DarkSlateGray,
        HorizontalOptions = LayoutOptions.Fill,
        Children = { new SkiaLayout { Type = LayoutType.Column, Spacing = 0, Children = rows.ToList() } },
    };

    private static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaSlider sharpness) Scene(bool doubleTapHandler)
    {
        var host = new HeadlessCanvasHost(480, 520, scale: 1f, background: Colors.Black);
        SkiaScroll scroll;
        SkiaSlider sharpness;

        var grid = new SkiaLayout
        {
            Type = LayoutType.Grid,
            RowSpacing = 0,
            Padding = new Thickness(24, 20),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaLayout
                {
                    Type = LayoutType.Column, Spacing = 6, VerticalOptions = LayoutOptions.Start,
                    Children =
                    {
                        new SkiaLabel { Text = "ADJUST", FontSize = 32, TextColor = Colors.Red },
                        new SkiaLabel { Text = "Fine-tune the target of your filter. Double tap a slider to reset it.", FontSize = 13, TextColor = Colors.Gray, HorizontalOptions = LayoutOptions.Fill },
                    }
                }.WithRow(0),

                new SkiaScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    ScrollBar = new SkiaScrollBar(),
                    Content = new SkiaLayout
                    {
                        Type = LayoutType.Column, Spacing = 0, HorizontalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            new SkiaLabel { Text = "IMAGE", TextColor = Colors.Gray, Margin = new Thickness(0, 12, 0, 8) },
                            Card(
                                SliderRow("Brightness", 1, 0.5, 1.5, 0.01, out _, doubleTapHandler),
                                SliderRow("Contrast", 1, 0.5, 1.5, 0.01, out _, doubleTapHandler),
                                SliderRow("Saturation", 1, 0, 2, 0.01, out _, doubleTapHandler),
                                SliderRow("Hue", 0, -180, 180, 1, out _, doubleTapHandler)),
                            new SkiaLabel { Text = "DETAIL", TextColor = Colors.Gray, Margin = new Thickness(0, 12, 0, 8) },
                            Card(SliderRow("Sharpness", 0.3, 0, 1, 0.01, out sharpness, doubleTapHandler)),
                        }
                    }
                }.Assign(out scroll).WithRow(1),

                Card(new SkiaLayout { Type = LayoutType.Row, Padding = new Thickness(16, 12), Children = { new SkiaLabel { Text = "Background only", TextColor = Colors.White }, new SkiaSwitch { HorizontalOptions = LayoutOptions.End } } })
                    .WithMargin(0, 12, 0, 0).WithRow(2),

                new SkiaLayout
                {
                    Type = LayoutType.Row, Spacing = 12, Margin = new Thickness(0, 16, 0, 0), HorizontalOptions = LayoutOptions.Center,
                    Children = { new SkiaButton { Text = "Reset", WidthRequest = 100 }, new SkiaButton { Text = "Undo", WidthRequest = 100 }, new SkiaButton { Text = "Close", WidthRequest = 100 } }
                }.WithRow(3),
            }
        }.WithRowDefinitions("Auto,*,Auto,Auto");

        host.Canvas.Content = grid;
        for (int i = 0; i < 3; i++) host.RenderFrame();
        return (host, scroll, sharpness);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DraggingThumbBelowTheFold_KeepsScrollOffset(bool doubleTapHandler)
    {
        var (host, scroll, slider) = Scene(doubleTapHandler);
        using var _ = host;
        var robot = new GestureRobot(host);

        var vp = scroll.DrawingRect;
        _out.WriteLine($"viewport {vp} content {scroll.Content.MeasuredSize.Pixels}");

        // scroll to the bottom with a pan inside the viewport, then let it settle
        robot.Pan(vp.MidX, vp.Bottom - 40, vp.MidX, vp.Top + 40, durationMs: 250, steps: 16);
        robot.SettleFling(scroll);
        for (int i = 0; i < 3; i++) host.RenderFrame();
        var offset = scroll.ViewportOffsetY;
        // DrawingRect is the layout slot inside the cached card; the visible position folds the scroll and caches in
        var pos = slider.GetSelfDrawingPosition();
        var rect = new SkiaSharp.SKRect(pos.X, pos.Y, pos.X + slider.DrawingRect.Width, pos.Y + slider.DrawingRect.Height);
        _out.WriteLine($"after scroll: offset {offset} slider slot {slider.DrawingRect} visible {rect}");
        Assert.True(offset < -50, $"scroll did not move: {offset}");
        Assert.True(rect.Top > vp.Top && rect.Bottom < vp.Bottom, $"slider not in viewport: {rect} vs {vp}");

        // press the thumb (End = 0.3 → 30% along the track) and drag it right
        var x = rect.Left + rect.Width * 0.3f; var y = rect.MidY;
        var endBefore = slider.End;
        robot.PointerDown(x, y);
        _out.WriteLine($"  down: offset {scroll.ViewportOffsetY}");
        for (int i = 1; i <= 8; i++)
        {
            robot.PointerMoveTo(x + i * 8, y + (i % 2));
            _out.WriteLine($"  move {i}: offset {scroll.ViewportOffsetY} end {slider.End}");
        }
        var offsetDuringDrag = scroll.ViewportOffsetY;
        robot.PointerUp();
        for (int i = 0; i < 3; i++) host.RenderFrame();

        _out.WriteLine($"after drag: offset {scroll.ViewportOffsetY} end {endBefore} -> {slider.End}");
        Assert.Equal(offset, offsetDuringDrag, 1f);
        Assert.Equal(offset, scroll.ViewportOffsetY, 1f);
        Assert.True(slider.End > endBefore, $"slider did not take the drag: {endBefore} -> {slider.End}");
    }

    /// <summary>The snap-home moved to the draw: content that really shrinks to fit still lands on 0.</summary>
    [Fact]
    public void ContentThatShrinksToFit_LandsOnZero()
    {
        var host = new HeadlessCanvasHost(400, 300, scale: 1f, background: Colors.Black);
        using var _ = host;
        SkiaShape spacer;
        var scroll = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Column, HorizontalOptions = LayoutOptions.Fill,
                Children = { new SkiaShape { Type = ShapeType.Rectangle, HeightRequest = 900, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Gray }.Assign(out spacer) }
            }
        };
        host.Canvas.Content = scroll;
        for (int i = 0; i < 3; i++) host.RenderFrame();
        var robot = new GestureRobot(host);
        robot.Pan(200, 250, 200, 50, durationMs: 250, steps: 16);
        robot.SettleFling(scroll);
        for (int i = 0; i < 3; i++) host.RenderFrame();
        Assert.True(scroll.ViewportOffsetY < -100, $"scroll did not move: {scroll.ViewportOffsetY}");

        spacer.HeightRequest = 100; // everything fits now
        for (int i = 0; i < 5; i++) host.RenderFrame();
        Assert.Equal(0, scroll.ViewportOffsetY, 0.5f);
    }
}
