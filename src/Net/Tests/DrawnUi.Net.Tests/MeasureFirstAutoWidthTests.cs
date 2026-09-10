using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A templated stack that sizes ITSELF from its cells on the cross axis (auto-width Column, the
/// ArtOfFoto spot-meter results list) must grow when a wider item is appended. MeasureFirst's
/// uniform-add fast path stamped every new cell with the first cell's width, so a longer result
/// measured into the old width and trailed with "..." while the stack never widened.
/// </summary>
public class MeasureFirstAutoWidthTests
{
    private class WidthFromContextCell : SkiaControl
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is int width)
            {
                WidthRequest = width;
                HeightRequest = 20;
            }
        }
    }

    [Fact]
    public void AutoWidthColumn_MeasureFirst_GrowsWhenAWiderItemIsAdded()
    {
        using var host = new HeadlessCanvasHost(400, 400, scale: 1f, background: Colors.Black);

        var items = new ObservableCollection<int> { 50 };
        var stack = new SkiaLayout
        {
            Type = LayoutType.Column,
            Spacing = 0,
            HorizontalOptions = LayoutOptions.Start, // auto width from cells, like the results list
            VerticalOptions = LayoutOptions.Start,
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() => new WidthFromContextCell()),
            // defaults on purpose: RecyclingTemplate.Enabled + MeasuringStrategy.MeasureFirst
        };
        var frame = new SkiaShape
        {
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Children = { stack }
        };
        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { frame }
        };

        for (int i = 0; i < 5; i++) host.RenderFrame(16);
        Assert.Equal(50, stack.MeasuredSize.Pixels.Width);

        items.Add(200);
        for (int i = 0; i < 8; i++) host.RenderFrame(16);

        var second = stack.ChildrenFactory.GetCellInUseOrNull(1);
        Assert.NotNull(second);
        Assert.Equal(200, second.MeasuredSize.Pixels.Width);
        Assert.Equal(200, stack.MeasuredSize.Pixels.Width);
        Assert.Equal(200, frame.DrawingRect.Width);
    }
}
