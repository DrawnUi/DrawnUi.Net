using System.Collections.ObjectModel;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A control that stayed in the same place for many frames still follows every change made to it or around it: after
/// the change it is arranged and drawn exactly as a control built with that change from the start (its rects and the
/// whole picture), with or without a cache above it.
/// </summary>
public class ArrangeStillFramesTests
{
    public static IEnumerable<object[]> Changes() =>
        from change in new[]
        {
            "margin", "width", "alignment", "hidden", "expand", "shadow", "viewport", "parent-width", "padding",
            "translation", "scale",
        }
        from cache in new[] { SkiaCacheType.None, SkiaCacheType.Image, SkiaCacheType.Operations }
        select new object[] { change, cache };

    static void Apply(string change, SkiaShape target, SkiaLayout parent)
    {
        switch (change)
        {
            case "margin": target.Margin = new Thickness(30, 12, 0, 0); break;
            case "width": target.WidthRequest = 220; break;
            case "alignment": target.HorizontalOptions = LayoutOptions.End; break;
            case "hidden": target.IsVisible = false; break;
            case "expand": target.ExpandDirtyRegion = new Thickness(10); break;
            case "shadow": target.Shadows = new List<SkiaShadow> { new() { Color = Colors.Red, Blur = 6, X = 4, Y = 4 } }; break;
            case "viewport": target.ViewportHeightLimit = 30; break;
            case "parent-width": parent.WidthRequest = 300; break;
            case "padding": parent.Padding = new Thickness(25); break;
            case "translation": target.TranslationX = 17; break;
            case "scale": target.ScaleX = 1.3; break;
        }
    }

    static (HeadlessCanvasHost host, SkiaShape target) Build(SkiaCacheType cache, string change, bool changedFromStart)
    {
        var host = new HeadlessCanvasHost(500, 400, 1.5f, Colors.Black);
        SkiaShape target = null;
        SkiaLayout parent = null;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaStack
                {
                    UseCache = cache,
                    WidthRequest = 400,
                    Padding = new Thickness(10),
                    BackgroundColor = Colors.DarkSlateGray,
                    Children =
                    {
                        new SkiaShape { HeightRequest = 30, BackgroundColor = Colors.Gray, HorizontalOptions = LayoutOptions.Fill },
                        new SkiaShape
                        {
                            WidthRequest = 120,
                            HeightRequest = 60,
                            CornerRadius = 8,
                            BackgroundColor = Colors.Orange,
                            Children = { new SkiaLabel("still") { FontSize = 14, TextColor = Colors.Black } },
                        }.Assign(out target),
                        new SkiaShape { HeightRequest = 30, BackgroundColor = Colors.Gray, HorizontalOptions = LayoutOptions.Fill },
                    }
                }.Assign(out parent),
            }
        };
        if (changedFromStart)
            Apply(change, target, parent);
        host.AdvanceFrames(8);
        if (!changedFromStart)
        {
            Apply(change, target, parent);
            host.AdvanceFrames(8);
        }
        return (host, target);
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void A_change_after_still_frames_lands_as_if_made_from_the_start(string change, SkiaCacheType cache)
    {
        var (late, lateTarget) = Build(cache, change, changedFromStart: false);
        var (early, earlyTarget) = Build(cache, change, changedFromStart: true);
        using (late)
        using (early)
        {
            // a hidden control is not arranged at all: only the picture says it is gone
            if (change != "hidden")
            {
                Assert.Equal(earlyTarget.DrawingRect, lateTarget.DrawingRect);
                Assert.Equal(earlyTarget.Destination, lateTarget.Destination);
                Assert.Equal(earlyTarget.DirtyRegion, lateTarget.DirtyRegion);
            }
            Assert.True(SamePixels(early, late), $"{change} under {cache}: the picture differs from a tree built with the change");
        }
    }

    [Fact]
    public void Cells_scrolled_away_and_back_land_where_they_were()
    {
        var items = new ObservableCollection<int>(Enumerable.Range(0, 100));
        using var host = new HeadlessCanvasHost(400, 500, 1f, Colors.Black);
        SkiaScroll scroll = null!;
        SkiaLayout stack = null!;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaLayout
            {
                Type = LayoutType.Column,
                HorizontalOptions = LayoutOptions.Fill,
                Virtualisation = VirtualisationType.Enabled,
                ItemsSource = items,
                ItemTemplate = new DataTemplate(() => new Row()),
            }.Assign(out stack),
        }.Assign(out scroll);
        host.AdvanceFrames(10);
        using var before = host.Snapshot();

        foreach (var y in new[] { 400f, 1600f, 3000f, 900f, 0f })
        {
            scroll.ScrollTo(0, -y, 0, true);
            host.AdvanceFrames(6);
        }
        host.AdvanceFrames(6);
        using var after = host.Snapshot();

        Assert.True(SamePixels(before, after), "the list scrolled back to the top does not look as before");
    }

    class Row : SkiaShape
    {
        readonly SkiaLabel _text;

        public Row()
        {
            HeightRequest = 48;
            HorizontalOptions = LayoutOptions.Fill;
            BackgroundColor = Colors.SteelBlue;
            Margin = new Thickness(0, 0, 0, 4);
            Children = new List<SkiaControl> { new SkiaLabel { FontSize = 14 }.Assign(out _text) };
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            _text.Text = $"Row {BindingContext}";
        }
    }

    static bool SamePixels(HeadlessCanvasHost a, HeadlessCanvasHost b)
    {
        using var ia = a.Snapshot();
        using var ib = b.Snapshot();
        return SamePixels(ia, ib);
    }

    static bool SamePixels(SkiaSharp.SKImage a, SkiaSharp.SKImage b)
    {
        using var pa = a.PeekPixels();
        using var pb = b.PeekPixels();
        if (pa == null || pb == null || pa.Width != pb.Width || pa.Height != pb.Height)
            return false;
        return pa.GetPixelSpan().SequenceEqual(pb.GetPixelSpan());
    }
}
