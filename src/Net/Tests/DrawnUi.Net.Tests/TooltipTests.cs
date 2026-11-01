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
    public void SkiaButton_ShowsItsTooltip()
    {
        using var host = new HeadlessCanvasHost(W, H);
        host.Canvas.TooltipStyle = new TooltipStyle { ShowDelayMs = Delay };
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaButton("Publish") { WidthRequest = 120, HeightRequest = 44, Margin = new Thickness(20, 100, 0, 0) }
                    .WithTooltip("Uploads the current version"),
            },
        };
        host.AdvanceFrames(3);

        MoveTo(host, 60, 120);
        WaitShown(host, "Uploads the current version");
    }

    [Fact]
    public void RowsInsideAScroll_ShowTheirTooltips()
    {
        using var host = new HeadlessCanvasHost(W, H);
        host.Canvas.TooltipStyle = new TooltipStyle { ShowDelayMs = Delay };
        host.Canvas.Content = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaShape
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    HeightRequest = 200,
                    Margin = new Thickness(0, 100, 0, 0),
                    IsClippedToBounds = true,
                    Children = new List<SkiaControl>
                    {
                        new SkiaScroll
                        {
                            VerticalOptions = LayoutOptions.Fill,
                            Content = new SkiaStack
                            {
                                Spacing = 1,
                                Children = Enumerable.Range(1, 30).Select(i => (SkiaControl)new SkiaLayer
                                {
                                    HeightRequest = 36,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    BackgroundColor = Colors.Gray,
                                    Children = new List<SkiaControl> { new SkiaLabel($"Row {i}") },
                                }.WithTooltip($"Details of row {i}")).ToList(),
                            },
                        },
                    },
                },
            },
        };
        host.AdvanceFrames(3);

        MoveTo(host, 100, 120);
        WaitShown(host, "Details of row 1");

        // scrolled: the next row's tooltip sits at the row where it is now, not where it was laid out
        host.Canvas.HandleDesktopPointerLeave();
        host.Canvas.HandleDesktopWheel(100, 200, -120, W, H);
        host.AdvanceFrames(60);
        MoveTo(host, 100, 200);
        WaitShown(host, host.Canvas.ShownTooltip ?? "Details of row");
        var shown = host.Canvas.ShownTooltip;
        Assert.NotNull(shown);
        host.RenderFrame();
        var rect = TooltipRect(host);
        var view = TooltipView(host);
        var boxBottom = rect.Bottom - (float)view.Margin.Bottom;
        // its box sits 6 pt above the row under the pointer, so near the pointer, not far up where the row was laid out
        Assert.InRange(boxBottom, 200 - 36 - 10, 200 + 1);
    }

    [Fact]
    public void RowsInACachedCard_OnAScrolledPage_TooltipAtTheRow()
    {
        using var host = new HeadlessCanvasHost(W, H);
        host.Canvas.TooltipStyle = new TooltipStyle { ShowDelayMs = Delay };
        host.Canvas.Content = new SkiaScroll
        {
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Spacing = 16,
                Children = new List<SkiaControl>
                {
                    new SkiaShape { HeightRequest = 500, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.DarkGray },
                    new SkiaShape
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.Operations, // a cached card, as in the Hello page
                        Padding = new Thickness(10),
                        Children = new List<SkiaControl>
                        {
                            new SkiaShape
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                HeightRequest = 200,
                                IsClippedToBounds = true,
                                Children = new List<SkiaControl>
                                {
                                    new SkiaScroll
                                    {
                                        VerticalOptions = LayoutOptions.Fill,
                                        Content = new SkiaStack
                                        {
                                            Spacing = 1,
                                            Children = Enumerable.Range(1, 30).Select(i => (SkiaControl)new SkiaLayer
                                            {
                                                HeightRequest = 36,
                                                HorizontalOptions = LayoutOptions.Fill,
                                                BackgroundColor = Colors.Gray,
                                                Children = new List<SkiaControl> { new SkiaLabel($"Row {i}") },
                                            }.WithTooltip($"Details of row {i}")).ToList(),
                                        },
                                    },
                                },
                            },
                        },
                    },
                    new SkiaShape { HeightRequest = 300, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.DarkGray },
                },
            },
        };
        host.AdvanceFrames(3);

        // scroll the page so the list is in view
        for (var i = 0; i < 2; i++)
        {
            host.Canvas.HandleDesktopWheel(100, 50, -120, W, H);
            host.AdvanceFrames(30);
        }
        host.AdvanceFrames(60);

        // find a point over a row
        MoveTo(host, 100, 300);
        for (var i = 0; i < 100 && host.Canvas.ShownTooltip == null; i++)
            Wait(host, 20);
        Assert.NotNull(host.Canvas.ShownTooltip);
        host.RenderFrame();

        var rect = TooltipRect(host);
        var view = TooltipView(host);
        var boxTop = rect.Top + (float)view.Margin.Top;
        var boxBottom = rect.Bottom - (float)view.Margin.Bottom;
        // attached to the row under the pointer at y 300: just above it or just below it, never far away
        Assert.True(Math.Abs(boxBottom - 300) < 60 || Math.Abs(boxTop - 300) < 60, $"tooltip box {boxTop}..{boxBottom}, pointer at 300");
    }

    [Fact]
    public void TextInSpans_NoAccessibilityTooltip()
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
                    new SkiaShape
                    {
                        AccessibilityLabel = "File",
                        Padding = new Thickness(10),
                        Children = new List<SkiaControl>
                        {
                            new SkiaLabel { FontSize = 30, Spans = { new TextSpan { Text = "F", Underline = true }, new TextSpan { Text = "ile" } } },
                        },
                    },
                },
            };
            host.AdvanceFrames(3);

            MoveTo(host, 25, 25);
            Wait(host, Delay * 3);
            Assert.Null(host.Canvas.ShownTooltip);
        }
        finally
        {
            Super.TooltipsFromAccessibility = was;
        }
    }

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
