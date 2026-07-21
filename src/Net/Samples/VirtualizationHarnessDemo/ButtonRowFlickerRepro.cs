using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// INVESTIGATION (Blazor sandbox /pickers-spinner): while a label above them changes text every
/// frame, a row of Image-cached SkiaButtons visibly flickers — buttons alternate between two
/// different widths/x-offsets on consecutive frames although their own text never changes.
/// Mimics the page: fixed-size column, Operations-cached IsParentIndependent label mutating text
/// each frame, row of 3 Image-cached buttons. Dumps per-frame button DrawingRect to catch the
/// oscillation headlessly.
/// </summary>
public static class ButtonRowFlickerRepro
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= BUTTON ROW FLICKER UNDER PER-FRAME LABEL MUTATION =========");

        SkiaButton btn1 = null, btn2 = null, btn3 = null;
        SkiaLabel label = null;

        var row = new SkiaLayout()
        {
            Type = LayoutType.Row,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 15,
            Children =
            {
                new SkiaButton()
                {
                    UseCache = SkiaCacheType.Image,
                    Text = "Spin Random",
                    BackgroundColor = Colors.Orange,
                    TextColor = Colors.White,
                    CornerRadius = 8,
                }.Assign(out btn1),
                new SkiaButton()
                {
                    UseCache = SkiaCacheType.Image,
                    Text = "Add Item",
                    BackgroundColor = Colors.Green,
                    TextColor = Colors.White,
                    CornerRadius = 8,
                }.Assign(out btn2),
                new SkiaButton()
                {
                    UseCache = SkiaCacheType.Image,
                    Text = "Remove Item",
                    BackgroundColor = Colors.Red,
                    TextColor = Colors.White,
                    CornerRadius = 8,
                }.Assign(out btn3),
            }
        };

        var root = new SkiaLayout()
        {
            WidthRequest = 444,
            HeightRequest = 420,
            Padding = new Thickness(16),
            BackgroundColor = new Color(0.063f, 0.149f, 0.286f, 1f),
            Type = LayoutType.Column,
            Spacing = 14,
            Children =
            {
                new SkiaLabel()
                {
                    UseCache = SkiaCacheType.Operations,
                    IsParentIndependent = true,
                    Text = "Selected: None",
                    FontSize = 18,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center
                }.Assign(out label),
                row
            }
        };

        using var host = new HeadlessCanvasHost(480, 452, scale: 1f, background: Colors.Black);
        host.Canvas.Content = root;
        host.AdvanceFrames(10, 16);

        var texts = new[] { "Selected: Azure [0]", "Selected: Maui [1]", "Selected: Blazor [2]", "Selected: Skia [3]", "Selected: Docs [4]", "Selected: Gaming [5]" };

        string prev = null;
        int changes = 0;
        for (int f = 0; f < 40; f++)
        {
            label.Text = texts[f % texts.Length];
            host.RenderFrame(16);
            Thread.Sleep(2);

            var line =
                $"b1 {btn1.DrawingRect.Left:0.#},{btn1.DrawingRect.Width:0.#} | " +
                $"b2 {btn2.DrawingRect.Left:0.#},{btn2.DrawingRect.Width:0.#} | " +
                $"b3 {btn3.DrawingRect.Left:0.#},{btn3.DrawingRect.Width:0.#} | " +
                $"row {row.DrawingRect.Left:0.#},{row.DrawingRect.Width:0.#}";

            if (line != prev)
            {
                changes++;
                Console.WriteLine($"frame {f:00}: {line}");
                prev = line;
            }
        }

        Console.WriteLine(changes <= 1
            ? "=> OK button rects stable"
            : $"=> FLICKER button rects changed {changes} times");
    }
}
