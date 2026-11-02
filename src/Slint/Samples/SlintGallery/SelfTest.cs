using DrawnUi.Slint;
using Slint;
using SlintGallery;
using Event = Slint.WindowEvent;

/// Opens pages with clicks on the Slint sidebar, as a user would, saves each page's frame, and quits.
internal static class SelfTest
{
    private static readonly string[] Pages = ["looks", "animations", "shaders", "cells", "text", "shapes"];

    public static void Run(MainWindow window, SlintCanvasHost host, List<string> routes, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var times = new List<double>();
        host.FrameRendered += times.Add;
        void Send(Event e) => window.Window.DispatchEvent(e);
        // Scrolls the list with the wheel until the row shows.
        void ScrollTo(int index)
        {
            var target = -Math.Max(0, window.RowHeight * (index + 1) - window.ListHeight);
            Send(new Event.PointerScrolled(new LogicalPosition(window.ListX + 60, window.ListY + 40), 0, target - window.ListScroll));
        }
        void ClickRow(int index)
        {
            var p = new LogicalPosition(window.ListX + 60, window.ListY + window.ListScroll + window.RowHeight * (index + 0.5f));
            Send(new Event.PointerMoved(p));
            Send(new Event.PointerPressed(p, PointerEventButton.Left));
            Send(new Event.PointerReleased(p, PointerEventButton.Left));
        }

        var ms = 1000;
        Slint.Timer.SingleShot(TimeSpan.FromMilliseconds(ms), () => host.SaveFrame(Path.Combine(outDir, "0-menu.png")));
        for (var i = 0; i < Pages.Length; i++)
        {
            var (n, route) = (i + 1, Pages[i]);
            var index = routes.IndexOf(route);
            Slint.Timer.SingleShot(TimeSpan.FromMilliseconds(ms += 300), () => ScrollTo(index));
            Slint.Timer.SingleShot(TimeSpan.FromMilliseconds(ms += 300), () => ClickRow(index));
            Slint.Timer.SingleShot(TimeSpan.FromMilliseconds(ms += 1500), () =>
            {
                host.SaveFrame(Path.Combine(outDir, $"{n}-{route}.png"));
                if (host.IsGpu)
                    host.SaveWindow(Path.Combine(outDir, $"{n}-{route}-window.png"));
                Console.WriteLine($"{route}: current {window.Current}, row {index}");
            });
        }
        Slint.Timer.SingleShot(TimeSpan.FromMilliseconds(ms += 300), () =>
        {
            var rest = times.Skip(1).ToList();
            Console.WriteLine($"GPU {host.IsGpu}, scale {host.Scale}, frames {times.Count}, average {rest.Average():F2} ms, max {rest.Max():F2} ms");
            EventLoop.Quit();
        });
    }
}
