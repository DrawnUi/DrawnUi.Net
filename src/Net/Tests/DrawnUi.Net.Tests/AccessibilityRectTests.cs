using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Accessibility node rects are where the control is drawn: transforms of the control and of its ancestors count,
/// and children kept from an ImageComposite cache stay in the snapshot while only a dirty sibling is redrawn.
/// </summary>
public class AccessibilityRectTests
{
    private readonly ITestOutputHelper _out;
    public AccessibilityRectTests(ITestOutputHelper o) { _out = o; }

    [Fact]
    public void Rect_FollowsTranslationAndScale_OfControlAndAncestors()
    {
        using var host = new HeadlessCanvasHost(400, 400);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;
        SkiaSwitch moved = null, scaled = null;
        SkiaLayout panel = null;

        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaLayout
                {
                    WidthRequest = 200,
                    HeightRequest = 200,
                    Children =
                    {
                        new SkiaSwitch { WidthRequest = 60, HeightRequest = 30, AccessibilityLabel = "moved" }.Assign(out moved),
                        new SkiaSwitch { WidthRequest = 60, HeightRequest = 30, Margin = new Thickness(0, 100, 0, 0), AccessibilityLabel = "scaled" }.Assign(out scaled),
                    }
                }.Assign(out panel),
            }
        };
        host.AdvanceFrames(4);

        Assert.Equal(new SKRect(0, 0, 60, 30), moved.GetAccessibilityPixelRect());

        moved.TranslationX = 20;          // own translation
        panel.TranslationY = 50;          // ancestor translation, a panel slid in
        scaled.ScaleX = 2;                // own scale around the center
        scaled.ScaleY = 2;
        host.AdvanceFrames(4);

        var movedRect = moved.GetAccessibilityPixelRect();
        var scaledRect = scaled.GetAccessibilityPixelRect();
        _out.WriteLine($"moved {movedRect} scaled {scaledRect}");

        Assert.Equal(new SKRect(20, 50, 80, 80), movedRect);
        // 60x30 at (0,100) scaled 2x around its center (30,115) -> (-30,85)..(90,145), then the panel's +50
        Assert.Equal(new SKRect(-30, 135, 90, 195), scaledRect);
        Assert.Contains(manager.Snapshot, n => n.Label == "moved" && n.Rect == movedRect);
    }

    [Fact]
    public void ImageComposite_CleanChildren_StayInSnapshot_WhenOneChildRedraws()
    {
        using var host = new HeadlessCanvasHost(400, 300);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;
        var cards = new List<SkiaShape>();

        SkiaLayout stack = null;
        host.Canvas.Content = new SkiaStack
        {
            UseCache = SkiaCacheType.ImageComposite,
            Spacing = 10,
            Children = Enumerable.Range(0, 4).Select(i => (SkiaControl)new SkiaShape
            {
                WidthRequest = 60, HeightRequest = 30, BackgroundColor = Colors.Gray,
                AccessibilityRole = Aria.RoleButton, AccessibilityLabel = $"c{i}",
            }.Adapt(c => cards.Add(c))).ToList()
        }.Assign(out stack);
        host.AdvanceFrames(4);
        Assert.Equal(4, manager.Snapshot.Length);

        // one child changes: a composition pass redraws it alone, the others are kept from the cache
        cards[2].BackgroundColor = Colors.Red;
        host.AdvanceFrames(3);

        _out.WriteLine($"frame {host.Canvas.FrameNumber} composition {stack.IsRenderingWithComposition} rendered {string.Join(",", cards.Select(x => x.RenderedFrame))}");
        Assert.True(stack.IsRenderingWithComposition, "the test did not reach the partial composition pass");
        Assert.Equal(4, manager.Snapshot.Length);
        Assert.All(cards, c => Assert.False(c.GetAccessibilityPixelRect().IsEmpty, "a clean child dropped out"));
    }
}
