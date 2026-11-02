using DrawnUi.Slint;
using Pong.Game;
using Slint;
using SlintPong;
using Event = Slint.WindowEvent;

/// Plays with Slint window events, as a user's keyboard would, without a click first, saves frames, and quits.
internal static class SelfTest
{
    // Slint's key codes, private-use characters.
    private const string LeftArrow = "";

    public static void Run(MainWindow window, SlintCanvasHost host, PongGame game, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var times = new List<double>();
        host.FrameRendered += times.Add;
        void Send(Event e) => window.Window.DispatchEvent(e);
        double paddleX = 0, ballY = 0;

        var steps = new (int Ms, Action Act)[]
        {
            (1000, () =>
            {
                host.SaveFrame(Path.Combine(outDir, "1-start.png"));
                paddleX = game.PlayerPaddle.Left;
                ballY = game.Ball.Top;
            }),
            (1100, () =>
            {
                Send(new Event.KeyPressed(" "));
                Send(new Event.KeyReleased(" "));
                Send(new Event.KeyPressed(LeftArrow));
            }),
            (1600, () => Send(new Event.KeyReleased(LeftArrow))),
            (2200, () =>
            {
                host.SaveFrame(Path.Combine(outDir, "2-playing.png"));
                Console.WriteLine($"paddle moved {game.PlayerPaddle.Left - paddleX:F0} points, ball moved {game.Ball.Top - ballY:F0} points");
            }),
            (2400, () =>
            {
                var rest = times.Skip(1).ToList();
                Console.WriteLine($"scale {host.Scale}, frames {times.Count}, average {rest.Average():F2} ms, max {rest.Max():F2} ms");
                EventLoop.Quit();
            }),
        };
        foreach (var (ms, act) in steps)
            Slint.Timer.SingleShot(TimeSpan.FromMilliseconds(ms), act);
    }
}
