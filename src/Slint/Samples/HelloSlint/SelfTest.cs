using DrawnUi.Draw;
using DrawnUi.Slint;
using HelloSlint;
using Slint;
using Event = Slint.WindowEvent;

/// Sends window events, as a user's mouse and keyboard would, saves frames, and quits.
internal static class SelfTest
{
    public static void Run(MainWindow window, SlintCanvasHost cards, SlintCanvasHost keys, CardsScene scene, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var times = new List<double>();
        cards.FrameRendered += times.Add;
        void Send(Event e) => window.Window.DispatchEvent(e);
        // The center of a DrawnUI control, in window coordinates.
        LogicalPosition At(SkiaControl c, float dy = 0) =>
            new(window.CardsX + c.DrawingRect.MidX / cards.Scale, window.CardsY + c.DrawingRect.MidY / cards.Scale + dy);
        void Click(LogicalPosition p)
        {
            Send(new Event.PointerMoved(p));
            Send(new Event.PointerPressed(p, PointerEventButton.Left));
            Send(new Event.PointerReleased(p, PointerEventButton.Left));
        }
        void Type(string text)
        {
            Send(new Event.KeyPressed(text));
            Send(new Event.KeyReleased(text));
        }

        var steps = new (int Ms, Action Act)[]
        {
            (900, () => Console.WriteLine($"window {window.Window.Size} logical {window.Window.LogicalSize}, cards at {window.CardsX},{window.CardsY}, keys at {window.KeysX},{window.KeysY}")),
            (1000, () => Send(new Event.PointerMoved(At(scene.Scroll)))),
            (1200, () => cards.SaveFrame(Path.Combine(outDir, "1-hover.png"))),
            (1300, () => Click(At(scene.Button))),
            (1500, () => Click(At(scene.Editor))),
            (1700, () => { foreach (var c in "Hi Slint") Type(c.ToString()); Type("\u0008"); }),
            (2000, () => Send(new Event.PointerScrolled(At(scene.Scroll), 0, -300))),
            (3300, () => Console.WriteLine($"scroll offset before leaving: {scene.Scroll.ViewportOffsetY:F0}")),
            (3400, () =>
            {
                var p = new LogicalPosition(window.KeysX + 20, window.KeysY + 80);
                Click(p);
                for (var i = 0; i < 3; i++) Type("");
                Type("");
            }),
            (4000, () =>
            {
                cards.SaveFrame(Path.Combine(outDir, "2-cards.png"));
                keys.SaveFrame(Path.Combine(outDir, "3-keys.png"));
                Console.WriteLine($"status: {window.Status}");
                Console.WriteLine($"editor: {scene.Editor.Text}");
                Console.WriteLine($"scroll offset: {scene.Scroll.ViewportOffsetY:F0}");
                var rest = times.Skip(1).ToList();
                Console.WriteLine($"scale {cards.Scale}, frames {times.Count}, first {times[0]:F1} ms, then average {rest.Average():F2} ms, max {rest.Max():F2} ms");
                EventLoop.Quit();
            }),
        };
        foreach (var (ms, act) in steps)
            Slint.Timer.SingleShot(TimeSpan.FromMilliseconds(ms), act);
    }
}
