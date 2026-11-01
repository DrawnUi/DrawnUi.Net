using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A scroll with a Header places its Footer right after the content at every screen scale: the header's size, the
/// content's size and the content offset are added in the same unit.
/// </summary>
public class ScrollFooterAfterHeaderTests
{
    static (HeadlessCanvasHost host, SkiaScroll scroll, SkiaLayout content, SkiaShape footer) Scene(ScrollOrientation orientation, float scale)
    {
        var vertical = orientation == ScrollOrientation.Vertical;
        var host = new HeadlessCanvasHost((int)(400 * scale), (int)(600 * scale), scale, Colors.Black);
        SkiaLayout content;
        SkiaShape footer;
        SkiaScroll scroll;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaScroll
                {
                    Orientation = orientation,
                    WidthRequest = 400,
                    HeightRequest = vertical ? 600 : 100,
                    Header = vertical
                        ? new SkiaShape { HeightRequest = 47, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Red }
                        : new SkiaShape { WidthRequest = 47, VerticalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Red },
                    Footer = (vertical
                        ? new SkiaShape { HeightRequest = 30, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Blue }
                        : new SkiaShape { WidthRequest = 30, VerticalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Blue }).Assign(out footer),
                    Content = (vertical
                        ? new SkiaStack
                        {
                            Children = Enumerable.Range(0, 3).Select(_ => (SkiaControl)new SkiaShape { HeightRequest = 50, HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.White }).ToList()
                        }
                        : (SkiaLayout)new SkiaRow
                        {
                            Children = Enumerable.Range(0, 3).Select(_ => (SkiaControl)new SkiaShape { WidthRequest = 50, VerticalOptions = LayoutOptions.Fill, BackgroundColor = Colors.White }).ToList()
                        }).Assign(out content),
                }.Assign(out scroll)
            }
        };
        host.AdvanceFrames(6);
        return (host, scroll, content, footer);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    public void Vertical_FooterRightAfterContent(float scale)
    {
        var (host, _, content, footer) = Scene(ScrollOrientation.Vertical, scale);
        using var _ = host;

        // both are placed by a translation in points on top of their drawing rect
        Assert.Equal(content.DrawingRect.Bottom + (float)content.AddTranslationY * scale,
            footer.DrawingRect.Top + (float)footer.AddTranslationY * scale, 1.5);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    public void Horizontal_FooterRightAfterContent(float scale)
    {
        var (host, _, content, footer) = Scene(ScrollOrientation.Horizontal, scale);
        using var _ = host;

        Assert.Equal(content.DrawingRect.Right + (float)content.AddTranslationX * scale,
            footer.DrawingRect.Left + (float)footer.AddTranslationX * scale, 1.5);
    }
}
