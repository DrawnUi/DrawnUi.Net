using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Infrastructure;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Pdf.SplitStackToPages must return page offsets relative to the control that will be scrolled,
/// break only between rows of the stack, and never leave content uncovered, whatever padding or
/// siblings sit above the stack. The rows are read from the render tree, so every test renders
/// the content once at unlimited height first, the way an exporter does.
/// </summary>
public class PdfSplitToPagesTests
{
    const float RowHeight = 40;

    private class Row : SkiaControl
    {
        public Row()
        {
            HorizontalOptions = LayoutOptions.Fill;
            HeightRequest = RowHeight;
        }
    }

    /// <summary>
    /// Padded column: a header of headerHeight, then a templated stack of rows.
    /// </summary>
    private static (SkiaLayout content, SkiaLayout stack) Build(int rows, float paddingTop, float headerHeight, float rowHeight = RowHeight)
    {
        var items = new ObservableCollection<int>(Enumerable.Range(0, rows));
        var stack = new SkiaLayout
        {
            Type = LayoutType.Column,
            Spacing = 0,
            HorizontalOptions = LayoutOptions.Fill,
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() => new Row { HeightRequest = rowHeight }),
            MeasureItemsStrategy = MeasuringStrategy.MeasureAll,
            RecyclingTemplate = RecyclingTemplate.Disabled,
        };
        var content = new SkiaLayout
        {
            Type = LayoutType.Column,
            Spacing = 0,
            Padding = new Thickness(0, paddingTop, 0, 0),
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaControl { HorizontalOptions = LayoutOptions.Fill, HeightRequest = headerHeight },
                stack,
            }
        };
        return (content, stack);
    }

    /// <summary>
    /// Measures at unlimited height and renders once into a discarded recording, at the given origin.
    /// </summary>
    private static void RenderDetached(SkiaControl layout, float width, float originY)
    {
        layout.Measure(width, float.PositiveInfinity, 1f);
        var size = layout.MeasuredSize.Pixels;
        layout.Arrange(new SKRect(0, originY, size.Width, originY + size.Height), size.Width, size.Height, 1f);

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, originY + size.Height));
        var ctx = new SkiaDrawingContext { Canvas = canvas, Width = width, Height = originY + size.Height };
        layout.Render(new DrawingContext(ctx, new SKRect(0, originY, width, originY + size.Height), 1f));
        using var picture = recorder.EndRecording();
    }

    [Fact]
    public void PagesBreakBetweenRows_WithPaddingAndHeaderAboveTheStack()
    {
        const float paddingTop = 30;
        const float headerHeight = 100;
        var (content, stack) = Build(rows: 30, paddingTop, headerHeight);
        RenderDetached(content, 500, originY: 0);
        Assert.Equal(30, stack.RenderTree.Count);

        var paper = new SKSize(500, 300);
        var pages = Pdf.SplitStackToPages(content, isTemplated: true, paper);

        // first page starts at the top and holds the header plus whole rows only
        Assert.Equal(0, pages[0].Position.Y);
        var stackTop = paddingTop + headerHeight;
        Assert.True(pages.Count > 1);
        foreach (var page in pages.Skip(1))
        {
            var local = page.Position.Y - stackTop;
            Assert.True(local >= 0, $"page {page.Index} starts above the stack");
            Assert.Equal(0, local % RowHeight);
        }

        // page 0 fits 4 rows (30 + 100 + 4*40 = 290 <= 300), so page 1 starts at row 4
        Assert.Equal(stackTop + 4 * RowHeight, pages[1].Position.Y);

        // every page height is the printable height, never above the paper, pages are contiguous
        for (var i = 0; i < pages.Count; i++)
        {
            Assert.True(pages[i].Height > 0 && pages[i].Height <= paper.Height);
            if (i > 0)
                Assert.Equal(pages[i - 1].Position.Y + pages[i - 1].Height, pages[i].Position.Y);
        }
        var last = pages[^1];
        Assert.Equal(content.MeasuredSize.Pixels.Height, last.Position.Y + last.Height);
    }

    [Fact]
    public void OffsetsAreRelativeToTheControl_WhenRenderedBelowTheOrigin()
    {
        var (a, _) = Build(rows: 20, paddingTop: 0, headerHeight: 0);
        var (b, _) = Build(rows: 20, paddingTop: 0, headerHeight: 0);
        RenderDetached(a, 500, originY: 0);
        RenderDetached(b, 500, originY: 123);

        var paper = new SKSize(500, 300);
        var pagesA = Pdf.SplitStackToPages(a, true, paper);
        var pagesB = Pdf.SplitStackToPages(b, true, paper);

        Assert.Equal(pagesA.Count, pagesB.Count);
        for (var i = 0; i < pagesA.Count; i++)
            Assert.Equal(pagesA[i].Position.Y, pagesB[i].Position.Y);
    }

    [Fact]
    public void RowTallerThanAPage_IsSlicedWithoutGaps()
    {
        var (content, _) = Build(rows: 3, paddingTop: 0, headerHeight: 0, rowHeight: 250);
        RenderDetached(content, 500, originY: 0);

        var paper = new SKSize(500, 100);
        var pages = Pdf.SplitStackToPages(content, true, paper);

        var covered = 0f;
        foreach (var page in pages)
        {
            Assert.Equal(covered, page.Position.Y);
            Assert.True(page.Height <= paper.Height);
            covered += page.Height;
        }
        Assert.Equal(750, covered);
    }

    [Fact]
    public void SplitToPages_FillsHeightOfEveryPage()
    {
        var pages = Pdf.SplitToPages(new SKSize(500, 250), new SKSize(500, 100));
        Assert.Equal(3, pages.Count);
        Assert.Equal(100, pages[0].Height);
        Assert.Equal(100, pages[1].Height);
        Assert.Equal(50, pages[2].Height);
        Assert.Equal(200, pages[2].Position.Y);
    }
}
