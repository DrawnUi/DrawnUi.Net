using System.ComponentModel;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A templated stack measuring every item: a recycled cell on screen that grows again and again (an image arrived,
/// a text grew) keeps pushing the cells below it, however many times it changes.
/// </summary>
public class TemplatedStackGrowthTests
{
    private readonly ITestOutputHelper _output;
    public TemplatedStackGrowthTests(ITestOutputHelper output) => _output = output;

    private class Item : INotifyPropertyChanged
    {
        private int _height;
        public int Height
        {
            get => _height;
            set { _height = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Height))); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>A cell whose height follows its item.</summary>
    private class GrowingCell : SkiaShape
    {
        private Item _item;

        public GrowingCell()
        {
            BackgroundColor = Colors.DarkSlateBlue;
            HorizontalOptions = LayoutOptions.Fill;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (_item != null)
                _item.PropertyChanged -= OnItemChanged;
            _item = BindingContext as Item;
            if (_item != null)
            {
                _item.PropertyChanged += OnItemChanged;
                HeightRequest = _item.Height;
            }
        }

        private void OnItemChanged(object sender, PropertyChangedEventArgs e) => HeightRequest = _item.Height;
    }

    [Fact]
    public void Column_VisibleCellGrowsAgainAndAgain_PushesTheCellsBelow()
    {
        using var host = new HeadlessCanvasHost(200, 400, scale: 1f, background: Colors.Black);
        var items = Enumerable.Range(0, 12).Select(i => new Item { Height = 40 + (i % 3) * 10 }).ToList();
        SkiaLayout column = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Column,
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Fill,
                RecyclingTemplate = RecyclingTemplate.Enabled,
                MeasureItemsStrategy = MeasuringStrategy.MeasureAll,
                ItemsSource = items,
                ItemTemplate = new DataTemplate(() => new GrowingCell()),
            }.Assign(out column)
        };
        host.AdvanceFrames(8);

        for (var round = 0; round < 6; round++)
        {
            items[2].Height += 20; // on screen
            host.AdvanceFrames(4);

            var structure = column.GetStackStructure();
            float top = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var c = structure.GetForIndex(i);
                Assert.True(Math.Abs(top - c.Destination.Top) < 1 && Math.Abs(items[i].Height - c.Destination.Height) < 1,
                    $"round {round}: #{i} at {c.Destination}, expected top {top} height {items[i].Height}");
                top += items[i].Height;
            }
            _output.WriteLine($"round {round}: content {column.MeasuredSize.Units.Height}, expected {top}");
            Assert.Equal(top, column.MeasuredSize.Units.Height, 1);
        }
    }
}
