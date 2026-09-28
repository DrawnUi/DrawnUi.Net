using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Models;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A container with a composite role (Aria.RoleList...) is an arrow-key group: the arrows move keyboard focus between its
/// items by index, like a native list. Recycled cells that are not realized yet are scrolled in and focused, items the
/// pointer cannot use are skipped, a control that uses the key itself keeps it, no wrap at the ends, and the whole group
/// is one Tab stop (its current item and the controls inside that item).
/// </summary>
public class KeyboardListNavigationTests
{
    private readonly ITestOutputHelper _out;
    public KeyboardListNavigationTests(ITestOutputHelper o) { _out = o; }

    /// <summary>A recycled cell that is itself the node, with an inner remove button; "locked" items take no input.</summary>
    private class ItemCell : SkiaLayout
    {
        public SkiaButton Remove;

        public ItemCell()
        {
            HeightRequest = 40;
            HorizontalOptions = LayoutOptions.Fill;
            AccessibilityRole = Aria.RoleButton;
            AccessibilityCanInteract = true;
            Children = new List<SkiaControl>
            {
                new SkiaButton("x") { WidthRequest = 30, HeightRequest = 30, HorizontalOptions = LayoutOptions.End, AccessibilityRole = Aria.RoleButton }.Assign(out Remove),
            };
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            var text = BindingContext as string;
            AccessibilityLabel = text;
            InputTransparent = text?.EndsWith("locked") == true;
        }
    }

    private static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaStack stack) TemplatedList(int count, Func<int, string> item = null)
    {
        var host = new HeadlessCanvasHost(300, 300, background: Colors.Black);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        SkiaScroll scroll = null;
        SkiaStack stack = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                AccessibilityRole = Aria.RoleList,
                Spacing = 0,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                ItemsSource = Enumerable.Range(0, count).Select(i => item?.Invoke(i) ?? $"item {i}").ToList(),
                ItemTemplate = new DataTemplate(() => new ItemCell()),
            }.Assign(out stack)
        }.Assign(out scroll);
        host.AdvanceFrames(4);
        return (host, scroll, stack);
    }

    private static SkiaControl Cell(SkiaStack stack, int index) => stack.ChildrenFactory.GetCellInUseOrNull(index);

    private static void Press(HeadlessCanvasHost host, InputKey key)
    {
        var focused = host.Canvas.AccessibilityManager.FocusedNode;
        Assert.True(SkiaAccessibilityManager.Key(focused, key), $"{key} was not used");
        host.AdvanceFrames(40); // scroll animation + focus at the frame end after the cell is drawn
    }

    private static string FocusedLabel(HeadlessCanvasHost host) => host.Canvas.AccessibilityManager.FocusedNode?.AccessibilityLabel;

    private static void AssertFocusedOnScreen(HeadlessCanvasHost host)
    {
        var node = host.Canvas.AccessibilityManager.FocusedNode;
        Assert.Same(node, host.Canvas.KeyboardFocusNode); // the ring follows
        var rect = node.GetAccessibilityPixelRect();
        Assert.True(rect.Top >= -0.5f && rect.Bottom <= 300.5f, $"focused item not in the viewport: {rect}");
    }

    private static void Focus(HeadlessCanvasHost host, ISkiaAccessibilityNode node) =>
        host.Canvas.AccessibilityManager.NotifyFocused(node);

    [Fact]
    public void RecycledList_ArrowsWalkEveryItem_ScrollingUnrealizedCellsIn()
    {
        var (host, scroll, stack) = TemplatedList(64);
        using var _ = host;

        Focus(host, Cell(stack, 0));
        for (int i = 1; i <= 20; i++)
        {
            Press(host, InputKey.ArrowDown);
            Assert.Equal($"item {i}", FocusedLabel(host));
            AssertFocusedOnScreen(host);
        }
        _out.WriteLine($"offset after 20 downs: {scroll.ViewportOffsetY}");
        Assert.True(scroll.ViewportOffsetY < -500); // item 20 is at 800..840, far below the first screen

        Press(host, InputKey.End);
        Assert.Equal("item 63", FocusedLabel(host));
        AssertFocusedOnScreen(host);

        Press(host, InputKey.ArrowDown); // no wrap: the key is used, nothing moves
        Assert.Equal("item 63", FocusedLabel(host));

        Press(host, InputKey.ArrowUp);
        Assert.Equal("item 62", FocusedLabel(host));

        Press(host, InputKey.Home);
        Assert.Equal("item 0", FocusedLabel(host));
        AssertFocusedOnScreen(host);

        Press(host, InputKey.PageDown); // one viewport: 300 / 40 = 7 items
        Assert.Equal("item 7", FocusedLabel(host));
        AssertFocusedOnScreen(host);
    }

    /// <summary>
    /// Fast key repeat: each press targets the next item before the previous scroll finished; focus ends exactly on the
    /// item pressed to and the list stays there. (On MAUI Windows a ScrollToIndex order left pending pulled the list back
    /// and rebound cells gave extra moves; both were seen only on the device, this checks the contract.)
    /// </summary>
    [Theory]
    [InlineData(64)]
    [InlineData(100000)] // windowed source, like the HelloMaui recycled list
    public void FastPresses_ListStaysOnTheFocusedItem(int count)
    {
        var (host, scroll, stack) = TemplatedList(count);
        using var _ = host;

        Focus(host, Cell(stack, 0));
        for (int i = 0; i < 15; i++)
        {
            Assert.True(SkiaAccessibilityManager.Key(host.Canvas.AccessibilityManager.FocusedNode, InputKey.ArrowDown));
            host.AdvanceFrames(1);
        }

        host.AdvanceFrames(200); // long settle: nothing may move the list afterwards
        Assert.Equal("item 15", FocusedLabel(host));
        AssertFocusedOnScreen(host);
    }

    [Fact]
    public void WindowedRecycledList_EndGoesToTheLastOfThousands()
    {
        var (host, scroll, stack) = TemplatedList(5000); // above WindowSourceThreshold: built-in source window
        using var _ = host;
        _out.WriteLine($"window: {stack.ItemsWindow?.WindowStart}..{stack.ItemsWindow?.WindowEnd}");

        Focus(host, Cell(stack, 0));
        Press(host, InputKey.End);
        host.AdvanceFrames(60);
        Assert.Equal("item 4999", FocusedLabel(host));
        AssertFocusedOnScreen(host);

        Press(host, InputKey.ArrowUp);
        Assert.Equal("item 4998", FocusedLabel(host));
    }

    [Fact]
    public void StackWithoutGroupRole_LeavesTheArrowsAlone()
    {
        using var host = new HeadlessCanvasHost(300, 300);
        SkiaButton first = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                Children =
                {
                    new SkiaButton("a") { WidthRequest = 100, HeightRequest = 40, AccessibilityRole = Aria.RoleButton }.Assign(out first),
                    new SkiaButton("b") { WidthRequest = 100, HeightRequest = 40, AccessibilityRole = Aria.RoleButton },
                }
            }
        };
        host.AdvanceFrames(4);

        Assert.False(SkiaAccessibilityManager.Key(first, InputKey.ArrowDown));
    }

    [Fact]
    public void Group_IsOneTabStop_TheCurrentItemAndItsInnerControls()
    {
        var (host, scroll, stack) = TemplatedList(20);
        using var _ = host;
        var manager = host.Canvas.AccessibilityManager;
        host.AdvanceFrames(2);

        List<ISkiaAccessibilityNode> Stops() => stack.ChildrenFactory.GetCellsInUse().Cast<ItemCell>()
            .SelectMany(c => new ISkiaAccessibilityNode[] { c, c.Remove })
            .Where(n => n.AccessibilityCanInteract && manager.IsTabStop(n)).ToList();

        var stops = Stops();
        var first = (ItemCell)Cell(stack, 0);
        Assert.Equal(2, stops.Count); // the first item and its remove button
        Assert.Contains(first, stops);
        Assert.Contains(first.Remove, stops);

        Focus(host, Cell(stack, 3)); // focus moved inside the group: the stop moves with it
        stops = Stops();
        Assert.Equal(2, stops.Count);
        Assert.Contains(Cell(stack, 3), stops);
    }

    [Fact]
    public void WrapGroup_UpDownMoveByARow()
    {
        using var host = new HeadlessCanvasHost(300, 400);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        var tiles = new List<SkiaShape>();
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaWrap
            {
                AccessibilityRole = Aria.RoleGrid,
                Spacing = 0,
                Children = Enumerable.Range(0, 12).Select(i => (SkiaControl)new SkiaShape
                {
                    WidthRequest = 100,
                    HeightRequest = 60,
                    BackgroundColor = Colors.DarkGray,
                    AccessibilityRole = Aria.RoleButton,
                    AccessibilityLabel = $"tile {i}",
                    AccessibilityCanInteract = true,
                }.Adapt(t => tiles.Add(t))).ToList()
            }
        };
        host.AdvanceFrames(4);

        // tiles per row as the wrap laid them out
        var row = tiles.Count(t => Math.Abs(t.GetAccessibilityPixelRect().Top - tiles[0].GetAccessibilityPixelRect().Top) < 1);
        _out.WriteLine($"tiles per row: {row}");
        Assert.True(row > 1);

        Focus(host, tiles[1]);
        Press(host, InputKey.ArrowDown);
        Assert.Equal($"tile {1 + row}", FocusedLabel(host));
        Press(host, InputKey.ArrowRight);
        Assert.Equal($"tile {2 + row}", FocusedLabel(host));
        Press(host, InputKey.ArrowUp);
        Assert.Equal("tile 2", FocusedLabel(host));
    }

    [Fact]
    public void ItemsThatCannotTakeInput_AreSkipped()
    {
        var (host, scroll, stack) = TemplatedList(20, i => i is 3 or 4 ? $"item {i} locked" : $"item {i}");
        using var _ = host;

        Focus(host, Cell(stack, 2));
        Press(host, InputKey.ArrowDown);
        Assert.Equal("item 5", FocusedLabel(host));
        Press(host, InputKey.ArrowUp);
        Assert.Equal("item 2", FocusedLabel(host));
    }

    [Fact]
    public void FromTheInnerButton_ArrowGoesToTheNextCell_NotItsButton()
    {
        var (host, scroll, stack) = TemplatedList(20);
        using var _ = host;

        var remove = ((ItemCell)Cell(stack, 1)).Remove;
        Focus(host, remove);
        Press(host, InputKey.ArrowDown);
        Assert.Same(Cell(stack, 2), host.Canvas.AccessibilityManager.FocusedNode);
    }

    [Fact]
    public void ControlThatUsesTheKey_KeepsIt()
    {
        using var host = new HeadlessCanvasHost(300, 300);
        SkiaSlider slider = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                AccessibilityRole = Aria.RoleList,
                Children =
                {
                    new SkiaSlider { WidthRequest = 200, HeightRequest = 40, Min = 0, Max = 10, Step = 1, End = 5 }.Assign(out slider),
                    new SkiaButton("next") { WidthRequest = 100, HeightRequest = 40, AccessibilityRole = Aria.RoleButton },
                }
            }
        };
        host.AdvanceFrames(4);

        Focus(host, slider);
        Press(host, InputKey.ArrowDown);
        Assert.Equal(4, slider.End);
        Assert.Same(slider, host.Canvas.AccessibilityManager.FocusedNode);
    }

    [Fact]
    public void PlainStackOfCards_BehavesTheSame()
    {
        using var host = new HeadlessCanvasHost(300, 300);
        host.Canvas.AccessibilityManager.MinUpdateIntervalMs = 0;
        var cards = new List<SkiaShape>();
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaStack
            {
                AccessibilityRole = Aria.RoleList,
                Spacing = 10,
                Children = Enumerable.Range(0, 12).Select(i => (SkiaControl)new SkiaShape
                {
                    HeightRequest = 80,
                    HorizontalOptions = LayoutOptions.Fill,
                    BackgroundColor = Colors.DarkGray,
                    AccessibilityRole = Aria.RoleButton,
                    AccessibilityLabel = $"card {i}",
                    AccessibilityCanInteract = true,
                }.Adapt(c => cards.Add(c))).ToList()
            }
        };
        host.AdvanceFrames(4);

        Focus(host, cards[0]);
        for (int i = 1; i < 12; i++)
        {
            Press(host, InputKey.ArrowDown);
            Assert.Equal($"card {i}", FocusedLabel(host));
            AssertFocusedOnScreen(host);
        }
    }
}
