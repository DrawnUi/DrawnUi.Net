using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A recycled templated list that gets a new ItemsSource (a new collection, or an empty one first) keeps its cells and
/// rebinds them, and a MeasureFirst list binds only the cells it shows, however many items there are.
/// </summary>
public class ItemsSourceSwapKeepsCellsTests
{
    private sealed class Row : SkiaDynamicDrawnCell
    {
        [ThreadStatic] public static int Created, Binds;
        private readonly SkiaLabel _title = new() { FontSize = 15 };

        public Row()
        {
            Created++;
            HorizontalOptions = LayoutOptions.Fill;
            Padding = new Thickness(16, 4);
            Children = new List<SkiaControl> { _title };
        }

        protected override void SetContent(object ctx)
        {
            Binds++;
            base.SetContent(ctx);
            _title.Text = ctx?.ToString();
        }
    }

    [Theory]
    [InlineData(MeasuringStrategy.MeasureFirst)]
    [InlineData(MeasuringStrategy.MeasureAll)]
    public void NewItemsSourceReusesCells(MeasuringStrategy strategy)
    {
        var host = new HeadlessCanvasHost(400, 800, 1, Colors.Black);
        var list = new SkiaStack
        {
            Spacing = 0,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = strategy,
            ItemTemplate = new DataTemplate(() => new Row()),
        };
        host.Canvas.Content = new SkiaScroll { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Content = list };

        var count = strategy == MeasuringStrategy.MeasureAll ? 200 : 10000;
        list.ItemsSource = new ObservableCollection<string>(Enumerable.Range(0, count).Select(i => "A" + i));
        host.AdvanceFrames(6);
        Assert.NotEmpty(list.ChildrenFactory.GetCellsInUse());

        for (var swap = 0; swap < 3; swap++)
        {
            Row.Created = 0;
            list.ItemsSource = new ObservableCollection<string>();
            host.AdvanceFrames(4);
            list.ItemsSource = new ObservableCollection<string>(Enumerable.Range(0, count).Select(i => $"B{swap}-{i}"));
            host.AdvanceFrames(6);

            Assert.Equal(0, Row.Created);
            Assert.NotEmpty(list.ChildrenFactory.GetCellsInUse());
        }
    }

    [Fact]
    public void MeasureFirstBindsOnlyShownCells()
    {
        var host = new HeadlessCanvasHost(400, 800, 1, Colors.Black);
        var list = new SkiaStack
        {
            Spacing = 0,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
            ItemTemplate = new DataTemplate(() => new Row()),
        };
        host.Canvas.Content = new SkiaScroll { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Content = list };
        host.AdvanceFrames(2);

        Row.Binds = 0;
        list.ItemsSource = new ObservableCollection<string>(Enumerable.Range(0, 10000).Select(i => "Item " + i));
        host.RenderFrame(16);

        var shown = list.ChildrenFactory.GetCellsInUse().Count;
        Assert.True(shown > 0);
        Assert.True(Row.Binds <= shown + 2, $"binds={Row.Binds} for {shown} shown cells");
    }
}
