using System.Collections.ObjectModel;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Drag-to-reorder moves one item a step at a time (ObservableCollection.Move). Every row must stay drawn and
/// bound to the item at its position, whatever the measuring strategy. Under MeasureAll each Move lost the
/// neighbour cell that slid into the old index (ViewsAdapter.HandleMove removed that index after the shift):
/// a drag through six rows left six blank rows, the layout still keeping their space.
/// </summary>
public class TemplatedMoveTests
{
    [Theory]
    [InlineData(MeasuringStrategy.MeasureAll, false)]
    [InlineData(MeasuringStrategy.MeasureAll, true)]
    [InlineData(MeasuringStrategy.MeasureFirst, false)]
    [InlineData(MeasuringStrategy.MeasureVisible, false)]
    public void DragSteps_EveryRowDrawnAndBound(MeasuringStrategy strategy, bool stepsInOneFrame)
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 12).Select(i => $"item{i}"));
        SkiaLayout list = null;
        using var host = new HeadlessCanvasHost(300, 600);
        host.Canvas.Content = new SkiaScroll
        {
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Column,
                Spacing = 3,
                HorizontalOptions = LayoutOptions.Fill,
                MeasureItemsStrategy = strategy,
                ItemTemplate = new DataTemplate(() => new SkiaShape
                {
                    HeightRequest = 30, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Red
                }),
                ItemsSource = items,
            }.Assign(out list),
        };
        host.AdvanceFrames(5);

        // row 2 dragged down to 7, then up to 1
        for (int i = 2; i < 7; i++)
        {
            items.Move(i, i + 1);
            if (!stepsInOneFrame)
                host.AdvanceFrames(2);
        }
        for (int i = 7; i > 1; i--)
        {
            items.Move(i, i - 1);
            if (!stepsInOneFrame)
                host.AdvanceFrames(2);
        }
        host.AdvanceFrames(5);

        var problems = new List<string>();
        for (int i = 0; i < items.Count; i++)
        {
            var cell = list.RenderTree?.FirstOrDefault(c => c.FreezeIndex == i);
            if (cell == null)
                problems.Add($"{i}: not drawn");
            else if (!Equals(cell.Control.BindingContext, items[i]))
                problems.Add($"{i}: shows {cell.Control.BindingContext}, holds {items[i]}");
        }

        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }
}
