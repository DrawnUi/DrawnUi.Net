using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;

// Headless demo proving GestureRobot drives real scrolling + fling on a SkiaScroll.

using var host = new HeadlessCanvasHost(400, 600, scale: 1f, background: Colors.White);

var content = new SkiaLayout
{
    Type = LayoutType.Column,
    HorizontalOptions = LayoutOptions.Fill,
    WidthRequest = 400,
    HeightRequest = 3000, // far taller than the 600px viewport → scrollable
    BackgroundColor = Colors.LightYellow
};

for (var i = 0; i < 30; i++)
{
    content.Children.Add(new SkiaLayout
    {
        Tag = $"cell{i}",
        HorizontalOptions = LayoutOptions.Fill,
        HeightRequest = 100,
        BackgroundColor = (i % 2 == 0) ? Colors.CornflowerBlue : Colors.Coral,
        Children =
        {
            new SkiaLabel { Text = $"Row {i}", FontSize = 24, TextColor = Colors.White, Margin = new Thickness(16, 32) }
        }
    });
}

var scroll = new SkiaScroll
{
    Tag = "scroll",
    Orientation = ScrollOrientation.Vertical,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Content = content
};

host.Canvas.Content = scroll;
host.RenderFrame(); // initial layout + listener registration

var robot = new GestureRobot(host);

Console.WriteLine($"Scale={host.Scale}");
Console.WriteLine($"Initial   ViewportOffsetY = {scroll.ViewportOffsetY:0.0}");

// 1) Slow drag up by ~360px (finger 500 → 140). Should scroll, little/no fling.
robot.Pan(200, 500, 200, 140, durationMs: 250, steps: 16);
var afterPan = scroll.ViewportOffsetY;
Console.WriteLine($"After Pan ViewportOffsetY = {afterPan:0.0}");

// settle any residual motion
robot.SettleFling(scroll);
var afterPanSettled = scroll.ViewportOffsetY;
Console.WriteLine($"Pan settled            = {afterPanSettled:0.0}");

// 2) Fast flick up (short fast drag) → strong fling.
var beforeFling = scroll.ViewportOffsetY;
var frames = robot.Fling(new System.Drawing.PointF(200, 500), new System.Drawing.PointF(200, 360), scroll,
    durationMs: 70, steps: 7);
var afterFling = scroll.ViewportOffsetY;
Console.WriteLine($"Fling moved {Math.Abs(afterFling - beforeFling):0.0}px over {frames} settle frames");
Console.WriteLine($"Final     ViewportOffsetY = {afterFling:0.0}");

host.SavePng(Path.Combine(AppContext.BaseDirectory, "gesture-demo-final.png"));

// crude self-check — offset goes negative when scrolling down; compare magnitudes
var ok = Math.Abs(afterPanSettled) > 100 && Math.Abs(afterFling) > Math.Abs(afterPanSettled);
Console.WriteLine(ok ? "RESULT: PASS — scroll moved on pan and advanced further on fling."
                     : "RESULT: FAIL — offsets did not change as expected.");
Environment.Exit(ok ? 0 : 1);
