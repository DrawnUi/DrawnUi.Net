using DrawnUi.Draw;
using SkiaSharp;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Tab and Shift+Tab walk every focusable control of a page in page order, also the ones of a scroll that are outside its
/// viewport (as browsers, WPF, WinUI, DrawnUi.React and DrawnUi.Rust do), and the scroll brings the focused one into view.
/// In a templated list whose cells are made on demand, Tab past the last made cell scrolls the list on so the next
/// item's cell exists, then focuses it (as WinUI's ListView).
/// </summary>
public class KeyboardTabScrollTests
{
    private const float ViewportHeight = 280;

    /// <summary>A recycled cell that is itself a button, named by its item.</summary>
    private class ItemCell : SkiaLayout
    {
        public ItemCell()
        {
            HeightRequest = 40;
            HorizontalOptions = LayoutOptions.Fill;
            AccessibilityRole = Aria.RoleButton;
            AccessibilityCanInteract = true;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            AccessibilityLabel = BindingContext as string;
        }
    }

    private static SkiaButton Button(string text) => new(text)
    {
        HeightRequest = 40,
        HorizontalOptions = LayoutOptions.Fill,
        AccessibilityRole = Aria.RoleButton,
        AccessibilityLabel = text,
    };

    private static (HeadlessCanvasHost Host, SkiaScroll Scroll) Page(bool virtualized, int count)
    {
        var host = new HeadlessCanvasHost(300, 400, background: Colors.Black);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;

        SkiaStack items;
        if (virtualized)
        {
            items = new SkiaStack
            {
                Spacing = 0,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                ItemsSource = Enumerable.Range(0, count).Select(i => $"Item {i}").ToList(),
                ItemTemplate = new DataTemplate(() => new ItemCell()),
            };
        }
        else
        {
            items = new SkiaStack { Spacing = 0 };
            for (var i = 0; i < count; i++)
                items.AddSubView(Button($"Item {i}"));
        }

        SkiaScroll scroll = null;
        host.Canvas.Content = new SkiaStack
        {
            Spacing = 0,
            Children =
            {
                new SkiaRow { Spacing = 0, Children = { Button("Back").WithWidth(120), Button("Home").WithWidth(120) } },
                new SkiaScroll { HeightRequest = ViewportHeight, HorizontalOptions = LayoutOptions.Fill, Content = items }.Assign(out scroll),
                Button("Footer"),
            }
        };
        host.AdvanceFrames(4);
        return (host, scroll);
    }

    private static string Press(HeadlessCanvasHost host, bool shift)
    {
        Assert.True(host.Canvas.HandleKeyboardNavigation(InputKey.Tab, shift), "Tab was not used");
        host.AdvanceFrames(30); // a scroll toward an item not drawn, then the focus at a frame end
        return host.Canvas.KeyboardFocusNode?.AccessibilityLabel;
    }

    private static void AssertInView(HeadlessCanvasHost host, SkiaScroll scroll, string label)
    {
        if (!label.StartsWith("Item ") && !label.StartsWith("Card "))
            return;
        var rect = host.Canvas.KeyboardFocusNode.GetAccessibilityPixelRect();
        var view = SKRect.Create(scroll.GetSelfDrawingPosition(), scroll.LastDrawnAt.Size);
        Assert.False(rect.IsEmpty, $"{label} is not drawn");
        Assert.True(rect.Top >= view.Top - 0.5f && rect.Bottom <= view.Bottom + 0.5f, $"{label} {rect} is not inside the viewport {view}");
    }

    [Theory]
    [InlineData(false, 30)]
    [InlineData(true, 200)]
    public void Tab_WalksEveryItem_InPageOrder_ScrollingEachIntoView_AndShiftTabWalksBack(bool virtualized, int count)
    {
        var (host, scroll) = Page(virtualized, count);
        using var _ = host;

        var expected = new[] { "Back", "Home" }.Concat(Enumerable.Range(0, count).Select(i => $"Item {i}")).Append("Footer").ToArray();

        foreach (var label in expected)
        {
            Assert.Equal(label, Press(host, false));
            AssertInView(host, scroll, label);
        }

        foreach (var label in Enumerable.Reverse(expected).Skip(1))
        {
            Assert.Equal(label, Press(host, true));
            AssertInView(host, scroll, label);
        }
    }

    /// <summary>Cards taller than the viewport, a button at the top and one at the bottom of each, cached or not.</summary>
    [Theory]
    [InlineData(SkiaCacheType.None)]
    [InlineData(SkiaCacheType.Image)]
    public void Tab_ReachesButtonsDeepInTallCards(SkiaCacheType cardCache)
    {
        var host = new HeadlessCanvasHost(300, 400, background: Colors.Black);
        using var _ = host;
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        SkiaScroll scroll = null;
        host.Canvas.Content = new SkiaStack
        {
            Spacing = 0,
            Children =
            {
                Button("Back"),
                new SkiaScroll
                {
                    HeightRequest = ViewportHeight, HorizontalOptions = LayoutOptions.Fill,
                    Content = new SkiaStack
                    {
                        Spacing = 20,
                        Children = Enumerable.Range(0, 3).Select(i => (SkiaControl)new SkiaShape
                        {
                            UseCache = cardCache,
                            HeightRequest = 600,
                            HorizontalOptions = LayoutOptions.Fill,
                            BackgroundColor = Colors.DarkGray,
                            Children =
                            {
                                Button($"Card {i} top"),
                                Button($"Card {i} bottom").WithVerticalOptions(LayoutOptions.End),
                            }
                        }).ToList()
                    }
                }.Assign(out scroll),
                Button("Footer"),
            }
        };
        host.AdvanceFrames(4);

        var expected = new[] { "Back" }.Concat(Enumerable.Range(0, 3).SelectMany(i => new[] { $"Card {i} top", $"Card {i} bottom" }))
            .Append("Footer").ToArray();
        foreach (var label in expected)
        {
            Assert.Equal(label, Press(host, false));
            AssertInView(host, scroll, label);
        }
        foreach (var label in Enumerable.Reverse(expected).Skip(1))
            Assert.Equal(label, Press(host, true));
    }

    /// <summary>A recycled cell holding two buttons, each named by the item.</summary>
    private class TwoButtonCell : SkiaLayout
    {
        private readonly SkiaButton _a, _b;

        public TwoButtonCell()
        {
            Type = LayoutType.Row;
            HeightRequest = 40;
            HorizontalOptions = LayoutOptions.Fill;
            Children = new List<SkiaControl>
            {
                new SkiaButton("a") { WidthRequest = 100, HeightRequest = 36, AccessibilityRole = Aria.RoleButton }.Assign(out _a),
                new SkiaButton("b") { WidthRequest = 100, HeightRequest = 36, AccessibilityRole = Aria.RoleButton }.Assign(out _b),
            };
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            _a.AccessibilityLabel = $"{BindingContext} a";
            _b.AccessibilityLabel = $"{BindingContext} b";
        }
    }

    /// <summary>Shift+Tab into a recycled cell with two stops lands on its last one, then its first (WinUI, browsers).</summary>
    [Fact]
    public void ShiftTab_IntoRecycledCellsWithTwoStops_TakesEachCellsLastStopFirst()
    {
        var host = new HeadlessCanvasHost(300, 400, background: Colors.Black);
        using var _ = host;
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        SkiaScroll scroll = null;
        const int count = 200;
        host.Canvas.Content = new SkiaStack
        {
            Spacing = 0,
            Children =
            {
                new SkiaRow { Spacing = 0, Children = { Button("Back").WithWidth(120), Button("Home").WithWidth(120) } },
                new SkiaScroll
                {
                    HeightRequest = ViewportHeight, HorizontalOptions = LayoutOptions.Fill,
                    Content = new SkiaStack
                    {
                        Spacing = 0,
                        RecyclingTemplate = RecyclingTemplate.Enabled,
                        MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                        ItemsSource = Enumerable.Range(0, count).Select(i => $"Item {i}").ToList(),
                        ItemTemplate = new DataTemplate(() => new TwoButtonCell()),
                    }
                }.Assign(out scroll),
                Button("Footer"),
            }
        };
        host.AdvanceFrames(4);

        var expected = new[] { "Back", "Home" }
            .Concat(Enumerable.Range(0, count).SelectMany(i => new[] { $"Item {i} a", $"Item {i} b" }))
            .Append("Footer").ToArray();
        foreach (var label in expected)
        {
            Assert.Equal(label, Press(host, false));
            AssertInView(host, scroll, label);
        }
        foreach (var label in Enumerable.Reverse(expected).Skip(1))
        {
            Assert.Equal(label, Press(host, true));
            AssertInView(host, scroll, label);
        }
    }
}
