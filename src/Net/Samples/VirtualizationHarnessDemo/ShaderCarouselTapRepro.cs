using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// FiltersCamera viewer: templated SkiaShaderCarousel, a tappable control inside every slide.
/// Slides never move in a shader carousel (AnimateVisibleChild is empty), so every slide sits
/// on the same rect. Which slide's control gets the tap when slide N is selected?
/// </summary>
public static class ShaderCarouselTapRepro
{
    class Item { public int Id { get; init; } public override string ToString() => $"item{Id}"; }

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("============= SHADER CAROUSEL TAP (stacked slides) =============");
        int bad = 0;
        foreach (var looped in new[] { false, true })
        foreach (var selected in new[] { 0, 1, 2 })
        {
            bad += RunCore(looped, selected);
        }
        Console.WriteLine(bad == 0 ? "=> PASS" : $"=> FAIL ({bad})");
    }

    static int RunCore(bool looped, int selected)
    {
        using var host = new HeadlessCanvasHost(400, 800, scale: 1f, background: Colors.Black);
        var tapped = new List<string>();
        var items = Enumerable.Range(0, 3).Select(i => new Item { Id = i }).ToList();

        SkiaShaderCarousel carousel = null;
        host.Canvas.Content = new SkiaLayer
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaShaderCarousel
                {
                    IsLooped = looped,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    ItemsSource = items,
                    SelectedIndex = selected,
                    ItemTemplate = new DataTemplate(() =>
                    {
                        SkiaShape play = null;
                        var cell = new SkiaLayer
                        {
                            UseCache = SkiaCacheType.Image,
                            BackgroundColor = Colors.DarkGray,
                            Children =
                            {
                                new SkiaShape
                                {
                                    Type = ShapeType.Rectangle,
                                    WidthRequest = 72, HeightRequest = 72,
                                    BackgroundColor = Colors.White,
                                    HorizontalOptions = LayoutOptions.Center,
                                    VerticalOptions = LayoutOptions.Center,
                                }
                                .Assign(out play)
                                .OnTapped(me => tapped.Add(me.BindingContext?.ToString() ?? "null")),
                            }
                        };
                        return cell;
                    }),
                }.Assign(out carousel)
            }
        };

        host.AdvanceFrames(30, 16);
        var robot = new GestureRobot(host);
        robot.Tap(200, 400);
        host.AdvanceFrames(10, 16);

        var expect = $"item{selected}";
        var got = string.Join(",", tapped);
        var ok = tapped.Count == 1 && tapped[0] == expect;
        Console.WriteLine($"  looped={looped} selected={selected} (carousel.SelectedIndex={carousel.SelectedIndex}) tapped=[{got}] {(ok ? "ok" : "FAIL expected " + expect)}");
        return ok ? 0 : 1;
    }
}
