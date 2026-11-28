using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// An endless feed keeps asking for pages past the point where the built-in items window engages (300 items):
/// scrolled down at a steady speed, every page that arrives brings the next LoadMore.
/// </summary>
public class LoadMoreKeepsComingTests
{
    private readonly ITestOutputHelper _out;
    public LoadMoreKeepsComingTests(ITestOutputHelper output) => _out = output;

    private sealed class Act(Action run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => run();
    }

    private sealed class Row : SkiaDynamicDrawnCell
    {
        private readonly SkiaLabel _t = new() { FontSize = 15 };
        public Row(bool uneven)
        {
            HorizontalOptions = LayoutOptions.Fill;
            if (!uneven) HeightRequest = 80;
            Children = new List<SkiaControl> { _t };
        }
        protected override void SetContent(object ctx)
        {
            base.SetContent(ctx);
            _t.Text = string.Concat(Enumerable.Repeat("word ", 4 + ((int)ctx % 5) * 12));
        }
    }

    [Theory]
    [InlineData(MeasuringStrategy.MeasureFirst)]
    [InlineData(MeasuringStrategy.MeasureVisible)]
    public void PagesKeepComing(MeasuringStrategy strategy)
    {
        var host = new HeadlessCanvasHost(411, 800, 2.625f, Colors.Black);
        var items = new ObservableCollection<int>(Enumerable.Range(0, 50));
        var asks = new List<int>();
        var pending = new List<(int Frame, int Count)>();
        var frame = 0;
        var list = new SkiaStack
        {
            Spacing = 0,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = strategy,
            ItemTemplate = new DataTemplate(() => new Row(strategy == MeasuringStrategy.MeasureVisible)),
            ItemsSource = items,
        };
        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            LoadMoreOffset = 1600,
            Content = list,
        };
        scroll.LoadMoreCommand = new Act(() =>
        {
            asks.Add(items.Count);
            if (pending.Count > 0)
                return; // busy: a real app ignores a second ask while its page is on the way
            pending.Add((frame + 50, items.Count)); // a page arrives 50 frames later
        });
        host.Canvas.Content = scroll;

        for (frame = 0; frame < 3000 && items.Count < 600; frame++)
        {
            foreach (var p in pending.Where(p => p.Frame == frame).ToList())
            {
                pending.Remove(p);
                for (var i = 0; i < 50; i++) items.Add(items.Count);
            }
            var limit = -Math.Max(0, scroll.ContentSize.Units.Height - scroll.Viewport.Units.Height);
            scroll.ViewportOffsetY = (float)Math.Max(limit, scroll.ViewportOffsetY - 40);
            host.RenderFrame(16);
            Thread.Sleep(8); // background measurement runs on real threads
        }
        _out.WriteLine($"{strategy}: items={items.Count} asks={string.Join(",", asks)} window={list.ItemsWindow?.WindowStart}");
        Assert.True(items.Count >= 600, $"feed stopped at {items.Count} items, asks at {string.Join(",", asks)}");
    }

    /// <summary>
    /// A page appended while the list rests at the end of a full items window lands outside the window: the list must
    /// still draw a frame by itself, so the scroll slides the window to it. Drawn only when a frame was asked for, as
    /// an app is.
    /// </summary>
    [Fact]
    public void PageAppendedAtRestSlidesTheFullWindow()
    {
        var host = new HeadlessCanvasHost(411, 800, 2.625f, Colors.Black);
        var items = new ObservableCollection<int>(Enumerable.Range(0, 400));
        var list = new SkiaStack
        {
            Spacing = 0,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
            ItemTemplate = new DataTemplate(() => new Row(false)),
            ItemsSource = items,
        };
        var scroll = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            LoadMoreOffset = 1600,
            LoadMoreCommand = new Act(() => { }), // asked, answered later
            Content = list,
        };
        host.Canvas.Content = scroll;

        // to the very end of the source, sliding the window on the way
        for (var f = 0; f < 600 && (list.ItemsWindow == null || list.ItemsWindow.WindowEnd < items.Count || scroll.ViewportOffsetY > -Math.Max(0, scroll.ContentSize.Units.Height - scroll.Viewport.Units.Height) + 1); f++)
        {
            var limit = -Math.Max(0, scroll.ContentSize.Units.Height - scroll.Viewport.Units.Height);
            scroll.ViewportOffsetY = (float)Math.Max(limit, scroll.ViewportOffsetY - 200);
            host.RenderFrame(16);
            Thread.Sleep(5);
        }
        Assert.NotNull(list.ItemsWindow);
        Assert.Equal(items.Count, list.ItemsWindow.WindowEnd);
        for (var f = 0; f < 100 && host.NeedsFrame; f++)
            host.RenderFrame(16); // settle until the list rests

        // the app answers LoadMore: a page appended past the full window, nobody touches the list
        for (var i = 0; i < 50; i++) items.Add(items.Count);
        for (var f = 0; f < 200 && host.NeedsFrame; f++)
        {
            host.RenderFrame(16);
            Thread.Sleep(5);
        }

        Assert.True(list.ItemsWindow.WindowEnd > 400, $"the window stayed at {list.ItemsWindow.WindowEnd}: no frame was drawn for the new page");
    }
}
