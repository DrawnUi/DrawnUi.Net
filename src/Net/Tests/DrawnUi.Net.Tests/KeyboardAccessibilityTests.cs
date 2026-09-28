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
/// Keyboard navigation contract shared by the heads (MAUI Windows, WPF, Blazor): a slider steps from the keyboard
/// and ignores activation, toggles flip on activation, the canvas focus ring outlines the node where it is drawn.
/// </summary>
public class KeyboardAccessibilityTests
{
    private readonly ITestOutputHelper _out;
    public KeyboardAccessibilityTests(ITestOutputHelper o) { _out = o; }

    [Fact]
    public void Slider_KeysStep_ActivationKeepsValue()
    {
        using var host = new HeadlessCanvasHost(400, 200);
        var slider = new SkiaSlider { WidthRequest = 300, HeightRequest = 40, Min = 0, Max = 10, Step = 0.5, End = 2 };
        host.Canvas.Content = new SkiaLayer { Children = { slider } };
        host.AdvanceFrames(3);

        slider.OnAccessibilityActivated(); // a tap at the center would move the value to 5
        host.AdvanceFrames(1);
        Assert.Equal(2, slider.End);

        Assert.True(slider.OnAccessibilityKey(InputKey.ArrowRight));
        Assert.Equal(2.5, slider.End);
        Assert.True(slider.OnAccessibilityKey(InputKey.ArrowUp));
        Assert.Equal(3, slider.End);
        Assert.True(slider.OnAccessibilityKey(InputKey.ArrowLeft));
        Assert.Equal(2.5, slider.End);
        Assert.True(slider.OnAccessibilityKey(InputKey.PageUp));
        Assert.Equal(3.5, slider.End);
        Assert.True(slider.OnAccessibilityKey(InputKey.End));
        Assert.Equal(10, slider.End);
        Assert.True(slider.OnAccessibilityKey(InputKey.ArrowRight)); // at Max: used, stays
        Assert.Equal(10, slider.End);
        Assert.True(slider.OnAccessibilityKey(InputKey.Home));
        Assert.Equal(0, slider.End);
        Assert.False(slider.OnAccessibilityKey(InputKey.KeyA));

        slider.RespondsToGestures = false;
        Assert.False(slider.OnAccessibilityKey(InputKey.ArrowRight));
        Assert.Equal(0, slider.End);
    }

    [Fact]
    public void Slider_WithoutStep_MovesHundredthOfRange()
    {
        using var host = new HeadlessCanvasHost(400, 200);
        var slider = new SkiaSlider { WidthRequest = 300, HeightRequest = 40, Min = 0, Max = 2, Step = 0, End = 1 };
        host.Canvas.Content = new SkiaLayer { Children = { slider } };
        host.AdvanceFrames(3);

        slider.OnAccessibilityKey(InputKey.ArrowRight);
        Assert.Equal(1.02, slider.End, 6);
        slider.OnAccessibilityKey(InputKey.PageDown);
        Assert.Equal(0.82, slider.End, 6);
    }

    [Fact]
    public void Toggles_FlipOnActivation_AndReportState()
    {
        using var host = new HeadlessCanvasHost(400, 200);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;
        var toggle = new SkiaSwitch { WidthRequest = 60, HeightRequest = 30 };
        var check = new SkiaCheckbox { WidthRequest = 30, HeightRequest = 30 };
        host.Canvas.Content = new SkiaStack { Children = { toggle, check } };
        host.AdvanceFrames(3);

        toggle.OnAccessibilityActivated();
        check.OnAccessibilityActivated();
        host.AdvanceFrames(2);

        Assert.True(toggle.IsToggled);
        Assert.True(check.IsToggled);
        Assert.True(Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleSwitch).IsPressed);
        Assert.True(Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleCheckbox).IsPressed);

        toggle.OnAccessibilityActivated();
        host.AdvanceFrames(2);
        Assert.False(toggle.IsToggled);
        Assert.False(Assert.Single(manager.Snapshot, n => n.Role == Aria.RoleSwitch).IsPressed);
    }

    /// <summary>
    /// The ring is drawn where the node is visible: a button inside a cached card inside a scrolled SkiaScroll,
    /// not at its layout slot, and it goes away with KeyboardFocusNode = null.
    /// </summary>
    [Fact]
    public void FocusRing_DrawnAtVisiblePosition_InsideCachedScrolledContent()
    {
        using var host = new HeadlessCanvasHost(300, 200, background: Colors.Black);
        SkiaScroll scroll = null;
        SkiaButton button = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                UseCache = SkiaCacheType.Image,
                Spacing = 0,
                Children =
                {
                    new SkiaControl { HeightRequest = 300 },
                    new SkiaShape
                    {
                        UseCache = SkiaCacheType.Operations,
                        BackgroundColor = Colors.Black,
                        Padding = new Thickness(20, 10),
                        HorizontalOptions = LayoutOptions.Start,
                        Children =
                        {
                            new SkiaButton("Go") { WidthRequest = 100, HeightRequest = 40, BackgroundColor = Colors.Black, TextColor = Colors.Black }.Assign(out button),
                        }
                    },
                    new SkiaControl { HeightRequest = 400 },
                }
            }
        }.Assign(out scroll);
        host.AdvanceFrames(3);

        scroll.ScrollTo(0, -250, 0, true);
        host.AdvanceFrames(3);

        _out.WriteLine($"offset {scroll.ViewportOffsetY}");
        Assert.Equal(-250, scroll.ViewportOffsetY, 1);

        host.Canvas.KeyboardFocusNode = button;
        host.AdvanceFrames(2);

        // button visible at x 20..120, y 300 + 10 - 250 = 60..100; ring 2px outside, 2px wide
        var ring = new SKColor(0x6E, 0xA8, 0xFE);
        using var image = host.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var top = bitmap.GetPixel(70, 58);
        var left = bitmap.GetPixel(18, 80);
        var layoutSlot = bitmap.GetPixel(70, 308);
        _out.WriteLine($"top {top} left {left} layoutSlot {layoutSlot}");
        Assert.True(Near(top, ring), $"no ring above the button: {top}");
        Assert.True(Near(left, ring), $"no ring left of the button: {left}");

        host.Canvas.KeyboardFocusNode = null;
        host.AdvanceFrames(2);
        using var image2 = host.Snapshot();
        using var bitmap2 = SKBitmap.FromImage(image2);
        Assert.False(Near(bitmap2.GetPixel(70, 58), ring), "ring stayed after KeyboardFocusNode = null");
    }

    /// <summary>
    /// Node rects must be canvas rects of what is on screen: controls inside Image-cached cards inside a scrolled
    /// SkiaScroll (LooksPage / a cached side panel), including cards never drawn yet below the fold.
    /// </summary>
    [Theory]
    [InlineData(SkiaCacheType.Image, SkiaCacheType.Image)]
    [InlineData(SkiaCacheType.None, SkiaCacheType.Image)]
    [InlineData(SkiaCacheType.None, SkiaCacheType.None)]
    public void SnapshotRects_AreVisibleCanvasRects(SkiaCacheType contentCache, SkiaCacheType cardCache)
    {
        using var host = new HeadlessCanvasHost(400, 300, background: Colors.Black);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;
        SkiaScroll scroll = null;
        var switches = new List<SkiaSwitch>();

        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                UseCache = contentCache,
                Spacing = 20,
                Padding = new Thickness(10),
                Children = Enumerable.Range(0, 5).Select(i => (SkiaControl)new SkiaShape
                {
                    UseCache = cardCache,
                    BackgroundColor = Colors.DarkGray,
                    Padding = new Thickness(30, 20),
                    HeightRequest = 100,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaSwitch { WidthRequest = 60, HeightRequest = 30, AccessibilityLabel = $"s{i}" }.Adapt(s => switches.Add(s)),
                    }
                }).ToList()
            }
        }.Assign(out scroll);
        host.AdvanceFrames(4);

        void Dump(string when)
        {
            foreach (var n in manager.Snapshot)
                _out.WriteLine($"{when} {n.Label} {n.Rect}");
        }

        // card i top = 10 + i * 120, switch = card + (30, 20); a node is in the snapshot while it is drawn (live or
        // into a cache that is blitted), so without a content cache only the cards in the viewport are there
        void AssertRects(float offset, int expectedCount)
        {
            Assert.Equal(expectedCount, manager.Snapshot.Length);
            foreach (var n in manager.Snapshot)
            {
                var i = int.Parse(n.Label.Substring(1));
                Assert.Equal(40, n.Rect.Left, 1);
                Assert.Equal(30 + i * 120 + offset, n.Rect.Top, 1);
            }
        }

        Dump("start");
        AssertRects(0, contentCache == SkiaCacheType.None ? 3 : 5);

        scroll.ScrollTo(0, -200, 0, true);
        host.AdvanceFrames(4);
        Dump("scrolled");
        AssertRects(-200, contentCache == SkiaCacheType.None ? 4 : 5); // uncached: card 0 is no longer drawn, it leaves
    }

    /// <summary>
    /// Recycled cells of a templated stack: only the realized cells in the viewport are nodes, with their visible
    /// rects; pooled cells never show up at their old place, cells scrolled in join the snapshot.
    /// </summary>
    [Fact]
    public void RecycledCells_OnlyDrawnCellsAreNodes()
    {
        using var host = new HeadlessCanvasHost(300, 300, background: Colors.Black);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;
        SkiaScroll scroll = null;

        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Spacing = 0,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                ItemsSource = Enumerable.Range(0, 50).Select(i => $"item {i}").ToList(),
                ItemTemplate = new DataTemplate(() => new LabelCell
                {
                    HeightRequest = 40,
                    HorizontalOptions = LayoutOptions.Fill,
                    AccessibilityRole = Aria.RoleButton,
                    AccessibilityCanInteract = true,
                })
            }
        }.Assign(out scroll);
        host.AdvanceFrames(4);

        void Check(string when, float offset)
        {
            var snap = manager.Snapshot;
            _out.WriteLine($"{when}: {string.Join(", ", snap.Select(n => $"{n.Label}@{n.Rect.Top}"))}");
            Assert.NotEmpty(snap);
            foreach (var n in snap)
            {
                var i = int.Parse(n.Label.Substring(5));
                Assert.Equal(i * 40 + offset, n.Rect.Top, 1);
                Assert.True(n.Rect.Bottom >= 0 && n.Rect.Top <= 300, $"{n.Label} is outside the viewport");
            }
        }

        Check("start", 0);
        Assert.Equal("item 0", manager.Snapshot[0].Label);

        scroll.ScrollTo(0, -1000, 0, true);
        host.AdvanceFrames(4);
        Check("scrolled", -1000);
        Assert.Contains(manager.Snapshot, n => n.Label == "item 25");
    }


    /// <summary>
    /// A small templated list whose stack is Image-cached draws every cell into the cache, so every cell is a node
    /// keyboard navigation reaches, and focusing one below the fold scrolls it into view.
    /// </summary>
    [Fact]
    public void RecycledCellsInCachedStack_AllReachable_AndScrollIntoView()
    {
        using var host = new HeadlessCanvasHost(300, 300, background: Colors.Black);
        var manager = host.Canvas.AccessibilityManager;
        manager.MinUpdateIntervalMs = 0;
        SkiaScroll scroll = null;
        SkiaStack stack = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                UseCache = SkiaCacheType.Image,
                Spacing = 0,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                ItemsSource = Enumerable.Range(0, 20).Select(i => $"item {i}").ToList(),
                ItemTemplate = new DataTemplate(() => new LabelCell { HeightRequest = 40, HorizontalOptions = LayoutOptions.Fill, AccessibilityRole = Aria.RoleButton, AccessibilityCanInteract = true })
            }.Assign(out stack)
        }.Assign(out scroll);
        host.AdvanceFrames(4);

        Assert.Equal(20, manager.Snapshot.Length);

        var cell = stack.ChildrenFactory.GetCellsInUse().First(c => (string)c.BindingContext == "item 15");
        SkiaScroll.EnsureVisible(cell, 0);
        host.AdvanceFrames(4);

        // cell 15 spans 600..640: its bottom lands 8pt above the viewport bottom (300)
        _out.WriteLine($"offset {scroll.ViewportOffsetY}");
        Assert.Equal(-348, scroll.ViewportOffsetY, 1);
        var node = Assert.Single(manager.Snapshot, n => n.Label == "item 15");
        Assert.Equal(252, node.Rect.Top, 1);
    }

    private class LabelCell : SkiaLayout
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            AccessibilityLabel = BindingContext as string;
        }
    }

    private static bool Near(SKColor a, SKColor b, int tolerance = 40) =>
        Math.Abs(a.Red - b.Red) <= tolerance && Math.Abs(a.Green - b.Green) <= tolerance && Math.Abs(a.Blue - b.Blue) <= tolerance;
}
