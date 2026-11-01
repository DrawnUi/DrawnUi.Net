using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Tooltips: a control with a Tooltip shows it after the mouse rests on it, the next one follows at once, a press,
/// the wheel or leaving closes it, and with TooltipsFromAccessibility icon controls use their accessibility text.
/// </summary>
[Collection("Tooltips")] // keys from other classes (KeyboardManager is static) close a tooltip
public class TooltipTests
{
    const int W = 400, H = 400;
    const int Delay = 80;

    static HeadlessCanvasHost Scene(out SkiaShape save, out SkiaShape open, out SkiaShape plain)
    {
        var host = new HeadlessCanvasHost(W, H);
        host.Canvas.TooltipStyle = new TooltipStyle { ShowDelayMs = Delay };
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaShape { WidthRequest = 100, HeightRequest = 100, BackgroundColor = Colors.Gray }
                    .WithTooltip("Save").Assign(out save),
                new SkiaShape { WidthRequest = 100, HeightRequest = 100, BackgroundColor = Colors.Gray, Margin = new Thickness(120, 0, 0, 0) }
                    .WithTooltip("Open").Assign(out open),
                new SkiaShape { WidthRequest = 100, HeightRequest = 100, BackgroundColor = Colors.Gray, Margin = new Thickness(240, 0, 0, 0) }
                    .Assign(out plain),
            },
        };
        host.AdvanceFrames(3);
        return host;
    }

    static void MoveTo(HeadlessCanvasHost host, float x, float y)
    {
        host.Canvas.HandleDesktopPointerMove(x, y, false, W, H);
        host.RenderFrame();
    }

    static void Wait(HeadlessCanvasHost host, int ms)
    {
        Thread.Sleep(ms);
        host.RenderFrame();
    }

    // the delay runs on a real timer: under a loaded test run it may fire late
    static void WaitShown(HeadlessCanvasHost host, string expected)
    {
        for (var i = 0; i < 100 && host.Canvas.ShownTooltip != expected; i++)
            Wait(host, 20);
        host.RenderFrame();
        Assert.Equal(expected, host.Canvas.ShownTooltip);
    }

    [Fact]
    public void ShowsAfterDelay_FollowsAtOnce_ClosesOnLeave()
    {
        using var host = Scene(out _, out _, out _);

        MoveTo(host, 50, 50);
        Assert.Null(host.Canvas.ShownTooltip); // not before the delay

        WaitShown(host, "Save");

        MoveTo(host, 170, 50); // the next control: no new wait
        Assert.Equal("Open", host.Canvas.ShownTooltip);

        MoveTo(host, 290, 50); // a control without a tooltip
        Assert.Null(host.Canvas.ShownTooltip);

        MoveTo(host, 50, 50);
        WaitShown(host, "Save");
        host.Canvas.HandleDesktopPointerLeave();
        Assert.Null(host.Canvas.ShownTooltip);
    }

    [Fact]
    public void Drawn_NextToItsControl()
    {
        using var host = Scene(out _, out _, out _);
        var before = host.NonBackgroundFraction(Colors.Black);

        MoveTo(host, 95, 95); // the tooltip opens below the control, on the empty canvas
        WaitShown(host, "Save");
        Assert.True(host.NonBackgroundFraction(Colors.Black) > before, "tooltip not drawn");
    }

    [Fact]
    public void LongText_WrapsAtMaxWidth()
    {
        using var host = Scene(out var save, out _, out _);
        save.Tooltip = string.Join(" ", Enumerable.Repeat("long tooltip text", 20));

        MoveTo(host, 50, 50);
        WaitShown(host, save.Tooltip);
        host.RenderFrame();

        var view = (SkiaControl)typeof(DrawnView).GetField("_tooltipView",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(host.Canvas);
        var label = view.Views.OfType<SkiaLabel>().Single();
        Assert.True(label.LinesCount > 1, $"lines {label.LinesCount}");
        var box = view.MeasuredSize.Units.Width - view.Margin.HorizontalThickness; // the margin holds the shadow
        Assert.True(box <= 320.5, $"width {box}");
    }

    [Fact]
    public void AttachedToItsControl_StaysWhileThePointerMoves()
    {
        using var host = Scene(out _, out var open, out _);

        MoveTo(host, 140, 30);
        WaitShown(host, "Open");
        host.RenderFrame();
        var rect = TooltipRect(host);

        // centered on the control; no room above it at the top of the canvas: below it
        var view = TooltipView(host);
        var boxLeft = rect.Left + (float)view.Margin.Left;
        var boxWidth = rect.Width - (float)view.Margin.HorizontalThickness;
        Assert.Equal(open.HitBoxAuto.MidX, boxLeft + boxWidth / 2, 1.5);
        Assert.Equal(open.HitBoxAuto.Bottom + 6, rect.Top + (float)view.Margin.Top, 1.5);

        MoveTo(host, 200, 80); // still on the control
        host.RenderFrame();
        Assert.Equal(rect, TooltipRect(host));
    }

    static SkiaControl TooltipView(HeadlessCanvasHost host) => (SkiaControl)typeof(DrawnView).GetField("_tooltipView",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(host.Canvas);

    static SkiaSharp.SKRect TooltipRect(HeadlessCanvasHost host) => (SkiaSharp.SKRect)typeof(DrawnView).GetField("_tooltipViewRect",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(host.Canvas);

    [Fact]
    public void PressCloses_UntilLeavingTheControl()
    {
        using var host = Scene(out _, out _, out _);

        MoveTo(host, 50, 50);
        WaitShown(host, "Save");

        host.Canvas.HandleDesktopPointerDown(50, 50, W, H);
        host.Canvas.HandleDesktopPointerUp(50, 50, W, H);
        host.RenderFrame();
        Assert.Null(host.Canvas.ShownTooltip);

        MoveTo(host, 52, 52); // still on it: stays closed
        Wait(host, Delay * 3);
        Assert.Null(host.Canvas.ShownTooltip);

        MoveTo(host, 290, 50);
        MoveTo(host, 50, 50); // came back
        WaitShown(host, "Save");
    }

    [Fact]
    public void WheelCloses()
    {
        using var host = Scene(out _, out _, out _);

        MoveTo(host, 50, 50);
        WaitShown(host, "Save");

        host.Canvas.HandleDesktopWheel(50, 50, -120, W, H);
        host.RenderFrame();
        Assert.Null(host.Canvas.ShownTooltip);
    }

    [Fact]
    public void FromAccessibility_OnlyWhenOptedIn()
    {
        var was = Super.TooltipsFromAccessibility;
        try
        {
            using var host = Scene(out _, out _, out var plain);
            plain.AccessibilityLabel = "Settings"; // an icon control: no text of its own

            MoveTo(host, 290, 50);
            Wait(host, Delay * 3);
            Assert.Null(host.Canvas.ShownTooltip);

            Super.TooltipsFromAccessibility = true;
            MoveTo(host, 291, 51);
            WaitShown(host, "Settings");

            plain.AccessibilityHint = "Opens the settings";
            MoveTo(host, 50, 50);
            MoveTo(host, 290, 50);
            Assert.Equal("Opens the settings", host.Canvas.ShownTooltip);
        }
        finally
        {
            Super.TooltipsFromAccessibility = was;
        }
    }

    [Fact]
    public void LabelWithItsOwnText_NoAccessibilityTooltip()
    {
        var was = Super.TooltipsFromAccessibility;
        try
        {
            Super.TooltipsFromAccessibility = true;
            using var host = new HeadlessCanvasHost(W, H);
            host.Canvas.TooltipStyle = new TooltipStyle { ShowDelayMs = Delay };
            host.Canvas.Content = new SkiaLayer
            {
                VerticalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel("Save") { FontSize = 40, AccessibilityLabel = "Save the file" },
                },
            };
            host.AdvanceFrames(3);

            MoveTo(host, 20, 20);
            Wait(host, Delay * 3);
            Assert.Null(host.Canvas.ShownTooltip);
        }
        finally
        {
            Super.TooltipsFromAccessibility = was;
        }
    }
}

[CollectionDefinition("Tooltips", DisableParallelization = true)]
public class TooltipsCollection;
