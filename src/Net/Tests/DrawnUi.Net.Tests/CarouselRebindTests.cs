using System.Drawing;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A templated, recycling SkiaCarousel re-bound to a NEW ItemsSource with SelectedIndex set in
/// the same cycle (a viewer paging a window of slides along a big selection) must come up on
/// that index over the new items, not reset to 0 and not keep the old count.
/// </summary>
public class CarouselRebindTests
{
    private readonly ITestOutputHelper _output;

    public CarouselRebindTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class Item
    {
        public string Name { get; init; } = "";
        public override string ToString() => Name;
    }

    private static List<Item> Items(string prefix, int count) =>
        Enumerable.Range(0, count).Select(i => new Item { Name = $"{prefix}{i}" }).ToList();

    [Fact]
    public void RebindWithSelectedIndex_ShowsThatIndexOverTheNewItems()
    {
        const int count = 8;
        var host = new HeadlessCanvasHost(400, 700, scale: 1f, background: Colors.Black);

        var first = Items("A", count);
        var second = Items("B", count);

        var changes = new List<int>();
        var carousel = new SkiaCarousel
        {
            HeightRequest = 350,
            HorizontalOptions = LayoutOptions.Fill,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            ItemTemplate = new DataTemplate(() => new SkiaShape
            {
                Type = ShapeType.Rectangle,
                BackgroundColor = Colors.DarkSlateBlue,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            }),
            ItemsSource = first,
            SelectedIndex = 6,
        };
        carousel.SelectedIndexChanged += (s, i) => changes.Add(i);

        host.Canvas.Content = new SkiaLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl> { carousel }
        };
        host.AdvanceFrames(8);

        Assert.Equal(count, carousel.ChildrenTotal);
        Assert.Equal(6, carousel.SelectedIndex);

        // the window slides: new items, new index, same cycle
        carousel.ItemsSource = second;
        carousel.SelectedIndex = 4;
        host.AdvanceFrames(8);

        _output.WriteLine($"after rebind: SelectedIndex={carousel.SelectedIndex} ChildrenTotal={carousel.ChildrenTotal} " +
                          $"SnapPoints={carousel.SnapPoints?.Count} changes=[{string.Join(",", changes)}]");

        Assert.Equal(count, carousel.ChildrenTotal);
        Assert.Equal(count, carousel.SnapPoints?.Count ?? 0);
        Assert.Equal(4, carousel.SelectedIndex);
        Assert.DoesNotContain(0, changes); // never reset to the first slide on the way

        var view = carousel.ChildrenFactory.GetViewForIndex(4);
        Assert.NotNull(view);
        try
        {
            Assert.Same(second[4], view.BindingContext);
        }
        finally
        {
            carousel.ChildrenFactory.ReleaseViewInUseForIndex(view.ContextIndex, view);
        }
    }
}
