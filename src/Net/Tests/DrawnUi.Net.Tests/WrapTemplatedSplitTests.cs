using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A templated SkiaWrap with Split (hello apps' Layouts page, "SkiaWrap ItemsSource · Split=3"): every item gets a cell
/// arranged in its slot and drawn. With RecyclingTemplate.Enabled it reserved its height but drew no cell (10 cells in use,
/// all 0x0): the structure is measured on the template instance, and the draw pass measured a recycled view only on a
/// size-key mismatch, a key that is main-axis only and 0 for a Wrap.
/// </summary>
public class WrapTemplatedSplitTests
{
    private readonly ITestOutputHelper _out;
    public WrapTemplatedSplitTests(ITestOutputHelper o) { _out = o; }

    private class Chip
    {
        public string Text { get; set; }
    }

    private class ChipCell : SkiaDynamicDrawnCell
    {
        private readonly SkiaLabel _label;

        public ChipCell()
        {
            HorizontalOptions = LayoutOptions.Fill;
            Children = new List<SkiaControl>
            {
                new SkiaShape
                {
                    CornerRadius = 10,
                    BackgroundColor = Colors.SteelBlue,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = new List<SkiaControl>
                    {
                        new SkiaLabel { FontSize = 13, Padding = new Thickness(12, 8), HorizontalOptions = LayoutOptions.Center }.Assign(out _label),
                    },
                },
            };
        }

        protected override void SetContent(object ctx)
        {
            base.SetContent(ctx);
            if (ctx is Chip chip)
                _label.Text = chip.Text;
        }
    }

    private static SkiaWrap Wrap(int split, RecyclingTemplate recycling) => new()
    {
        Spacing = 8,
        Split = split,
        RecyclingTemplate = recycling,
        ItemsSource = Enumerable.Range(1, 10).Select(i => new Chip { Text = $"Item {i}" }).ToList(),
        ItemTemplate = new DataTemplate(() => new ChipCell()),
    };

    private static SkiaControl Card(SkiaControl content) => new SkiaShape
    {
        CornerRadius = 8,
        BackgroundColor = Colors.DarkSlateGray,
        HorizontalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>
        {
            new SkiaStack { Padding = new Thickness(16, 12), Spacing = 10, Children = new List<SkiaControl> { content } },
        },
    };

    public enum Host { Alone, Card, ScrollCard }

    [Theory]
    [InlineData(Host.Alone, 3, RecyclingTemplate.Enabled)]
    [InlineData(Host.Card, 3, RecyclingTemplate.Enabled)]
    [InlineData(Host.ScrollCard, 3, RecyclingTemplate.Enabled)]
    [InlineData(Host.ScrollCard, 3, RecyclingTemplate.Disabled)]
    [InlineData(Host.ScrollCard, 0, RecyclingTemplate.Enabled)]
    [InlineData(Host.ScrollCard, 2, RecyclingTemplate.Enabled)]
    public void EveryCellArrangedAndDrawn(Host host, int split, RecyclingTemplate recycling)
    {
        using var canvasHost = new HeadlessCanvasHost(800, 600, scale: 1f, background: Colors.Black);
        var wrap = Wrap(split, recycling);
        SkiaControl content = host switch
        {
            Host.Alone => wrap,
            Host.Card => Card(wrap),
            _ => new SkiaScroll
            {
                VerticalOptions = LayoutOptions.Fill,
                Content = new SkiaStack { Children = new List<SkiaControl> { Card(wrap) } },
            },
        };
        canvasHost.Canvas.Content = new SkiaLayer { VerticalOptions = LayoutOptions.Fill, Children = { content } };
        canvasHost.AdvanceFrames(6);

        var cells = Enumerable.Range(0, 10).Select(i => wrap.ChildrenFactory.GetCellInUseOrNull(i)).ToList();
        _out.WriteLine($"{host} split={split} {recycling}: wrap {wrap.DrawingRect} measured {wrap.MeasuredSize.Pixels} " +
                       $"rendered {wrap.RenderTree?.AsSpans().Length ?? -1}");
        foreach (var c in wrap.GetStackStructure().GetChildren())
            _out.WriteLine($"  cell {c?.ControlIndex}: wasMeasured={c?.WasMeasured} measured={c?.Measured.Pixels} dest={c?.Destination} visible={c?.IsVisible} drawn={c?.Drawn}");
        for (var i = 0; i < cells.Count; i++)
            _out.WriteLine($"  {i}: {(cells[i] == null ? "null" : $"{cells[i].DrawingRect} needMeasure={cells[i].NeedMeasure} visible={cells[i].IsVisible} canDraw={cells[i].CanDraw} measured={cells[i].MeasuredSize.Pixels} wasMeasured={cells[i].WasMeasured} parent={cells[i].Parent?.GetType().Name} ctx={cells[i].ContextIndex}")}");

        Assert.Equal(10, wrap.RenderTree?.AsSpans().Length ?? 0);
        Assert.All(cells, c => Assert.True(c != null && c.DrawingRect.Width >= 1 && c.DrawingRect.Height >= 1));
    }
}
