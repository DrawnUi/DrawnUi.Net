using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A templated Column with MeasureAll and recycling off, measured once by hand with N items, must measure
/// every cell a bounded number of times. Creating or binding a new cell must not remeasure all the others
/// (that would be N^2: 100 rows took 19.6 s on a phone in RepoReport, 200 ms per row).
/// </summary>
public class TemplatedMeasureAllCountTests
{
    class CountingCell : SkiaLayout
    {
        public static int Measures;

        public CountingCell()
        {
            Type = LayoutType.Grid;
            HeightRequest = 48;
            HorizontalOptions = LayoutOptions.Fill;
            Children = new List<SkiaControl>
            {
                new SkiaLabel { Text = "#" }.SetGrid(0, 0),
                new SkiaShape { Type = ShapeType.Circle, WidthRequest = 34, HeightRequest = 34 }.SetGrid(1, 0),
                new SkiaLabel().SetGrid(2, 0),
                new SkiaLabel { Text = "1" }.SetGrid(3, 0),
            };
            this.WithColumnDefinitions("44,52,*,120");
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (Views.Count > 2 && Views[2] is SkiaLabel label)
                label.Text = BindingContext?.ToString();
        }

        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            Measures++;
            return base.OnMeasuring(widthConstraint, heightConstraint, scale);
        }
    }

    static int MeasureOnce(int items)
    {
        CountingCell.Measures = 0;
        var layout = new SkiaLayout
        {
            Type = LayoutType.Column,
            HorizontalOptions = LayoutOptions.Fill,
            RecyclingTemplate = RecyclingTemplate.Disabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureAll,
            Spacing = 0,
            ItemsSource = Enumerable.Range(1, items).Select(i => $"user{i}").ToList(),
            ItemTemplate = new DataTemplate(() => new CountingCell()),
        };

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var size = layout.Measure(1150, float.PositiveInfinity, 1f);
        Console.WriteLine($"[count] {items} items: {CountingCell.Measures} cell measures, {clock.ElapsedMilliseconds} ms, height {size.Pixels.Height}");
        Assert.Equal(items * 48, size.Pixels.Height);
        return CountingCell.Measures;
    }

    [Fact]
    public void MeasureAll_CellMeasureCount_GrowsLinearly()
    {
        var m25 = MeasureOnce(25);
        var m100 = MeasureOnce(100);

        // linear: 4x the items -> about 4x the measures. N^2 would be 16x.
        Assert.True(m100 <= m25 * 6, $"25 items: {m25} cell measures, 100 items: {m100} cell measures");
    }
}
