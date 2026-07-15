using DrawnUi.Draw;
using DrawnUi.Testing;
using SkiaSharp;

namespace VirtualizationHarnessDemo;

/// <summary>
/// INVESTIGATION (Blazor sandbox /effects-hover-mask): SkiaHoverMask should darken the whole parent
/// surface except its own shape cutout, but renders only a small dark square the size of its own
/// bounds. Mimics the page: absolute layout with light background + centered 184x184 circle mask.
/// Asserts a corner pixel of the parent (far outside the mask bounds) actually got darkened.
/// </summary>
public static class HoverMaskRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= HOVERMASK PARENT COVERAGE =========");

        var root = new SkiaLayout()
        {
            Margin = new Thickness(16),
            WidthRequest = 328,
            HeightRequest = 328,
            BackgroundColor = Colors.White,
            Children =
            {
                new SkiaHoverMask()
                {
                    WidthRequest = 184,
                    HeightRequest = 184,
                    Type = ShapeType.Circle,
                    BackgroundColor = new Color(0f, 0f, 0f, 0.55f),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
            }
        };

        using var host = new HeadlessCanvasHost(360, 360, scale: 1f, background: Colors.Black);
        host.Canvas.Content = root;
        host.RenderFrame(16);
        var png1 = Path.Combine(Path.GetTempPath(), "hovermask_frame1.png");
        host.SavePng(png1);
        host.AdvanceFrames(9, 16);

        var png = Path.Combine(Path.GetTempPath(), "hovermask_headless.png");
        host.SavePng(png);
        Console.WriteLine($"saved {png}");

        using var bmp = SKBitmap.Decode(png);
        // parent top-left corner (inside layout, outside mask bounds): expected darkened white = ~115
        var corner = bmp.GetPixel(30, 30);
        // center of mask circle: cutout, expected pure white
        var center = bmp.GetPixel(180, 180);
        Console.WriteLine($"corner(30,30)={corner} center(180,180)={center}");

        bool cornerDark = corner.Red < 200 && corner.Green < 200 && corner.Blue < 200;
        bool centerClear = center.Red > 240;

        Console.WriteLine(cornerDark && centerClear
            ? "=> OK mask covers parent, cutout clear"
            : $"=> FAIL cornerDark={cornerDark} centerClear={centerClear}");
    }
}
