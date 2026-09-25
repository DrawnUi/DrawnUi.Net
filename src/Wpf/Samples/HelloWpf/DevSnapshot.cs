using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfPoint = System.Windows.Point;

namespace HelloWpf;

/// <summary>
/// Development aid for verifying a ported page without a human at the screen: when the
/// <c>HELLOWPF_SNAPSHOT</c> environment variable holds a file path, the drawn surface is rendered to
/// that PNG a couple of seconds after load, and the app exits when <c>HELLOWPF_EXIT</c> is set too.
/// <para>
/// Inert in a normal run — no variable, no timer, no cost. It uses WPF's own
/// <see cref="RenderTargetBitmap"/> rather than a screen grab, so it captures exactly what the
/// element painted and works on machines with no interactive desktop session.
/// </para>
/// </summary>
public static class DevSnapshot
{
    /// <summary>
    /// Navigates straight to a route when <c>HELLOWPF_NAV</c> names one, so a page can be verified
    /// without driving the menu. Inert when the variable is unset.
    /// </summary>
    public static void NavigateIfRequested(DrawnUi.Draw.SkiaShell shell)
    {
        var route = Environment.GetEnvironmentVariable("HELLOWPF_NAV");
        if (string.IsNullOrWhiteSpace(route))
            return;

        // After the first layout: the shell measures its slide distance from its own arranged width.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _ = shell.GoToAsync(route);
        };
        timer.Start();
    }

    /// <summary>Arms the snapshot for an element, if the environment asks for one.</summary>
    public static void ArmIfRequested(FrameworkElement element)
    {
        var path = Environment.GetEnvironmentVariable("HELLOWPF_SNAPSHOT");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var delay = double.TryParse(Environment.GetEnvironmentVariable("HELLOWPF_SNAPSHOT_DELAY"), out var seconds)
            ? seconds
            : 2.5;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();

            // HELLOWPF_WHEEL=<notches> scrolls the canvas down before capturing, which is how a page's
            // scroll end gets verified without a human at the wheel.
            if (element is DrawnUi.Wpf.DrawnUiElement drawn
                && int.TryParse(Environment.GetEnvironmentVariable("HELLOWPF_WHEEL"), out var notches)
                && notches != 0)
            {
                var w = (float)(drawn.ActualWidth * 2);
                var h = (float)(drawn.ActualHeight * 2);
                var step = Math.Sign(notches);

                // HELLOWPF_WHEEL_DELAY=<ms> spaces the notches out (default 20).
                var pause = int.TryParse(Environment.GetEnvironmentVariable("HELLOWPF_WHEEL_DELAY"), out var ms) ? ms : 20;

                // HELLOWPF_PACE=1 records every composition tick during the wheel run: tick spacing,
                // WPF's RenderingTime spacing and the spacing of frames the canvas actually drew.
                var pace = Environment.GetEnvironmentVariable("HELLOWPF_PACE") == "1";
                var ticks = new List<(double wall, double render, long frame)>();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                EventHandler onTick = (_, args) =>
                {
                    var rt = args is RenderingEventArgs r ? r.RenderingTime.TotalMilliseconds : -1;
                    long frame = 0;
                    for (var c = 0; c < VisualTreeHelper.GetChildrenCount(drawn); c++)
                        if (VisualTreeHelper.GetChild(drawn, c) is DrawnUi.Draw.SkiaViewAccelerated gpu) frame = gpu.FrameTime;
                    ticks.Add((clock.Elapsed.TotalMilliseconds, rt, frame));
                };
                if (pace) CompositionTarget.Rendering += onTick;
                for (var i = 0; i < Math.Abs(notches); i++)
                {
                    // HELLOWPF_WHEEL_X / _Y place the pointer (pixels); default centre. A page with nested
                    // scrolls needs the pointer over the outer one (a side margin) to scroll the page.
                    var wx = float.TryParse(Environment.GetEnvironmentVariable("HELLOWPF_WHEEL_X"), out var px) ? px : w / 2;
                    var wy = float.TryParse(Environment.GetEnvironmentVariable("HELLOWPF_WHEEL_Y"), out var py) ? py : h / 2;
                    var notchClock = System.Diagnostics.Stopwatch.StartNew();
                    drawn.Canvas.HandleDesktopWheel(wx, wy, -120 * 5 * step, w, h);
                    if (pace) Console.WriteLine($"[DevSnapshot] pace notch {i} sync cost {notchClock.Elapsed.TotalMilliseconds:0.0} ms at tick #{ticks.Count}");
                    await Task.Delay(pause);
                }

                await Task.Delay(1200);

                if (pace)
                {
                    CompositionTarget.Rendering -= onTick;
                    static string Hist(IEnumerable<double> deltas)
                    {
                        var d = deltas.ToList();
                        if (d.Count == 0) return "none";
                        int b0 = d.Count(x => x < 10), b1 = d.Count(x => x >= 10 && x < 20), b2 = d.Count(x => x >= 20 && x < 30), b3 = d.Count(x => x >= 30);
                        return $"n={d.Count} <10ms:{b0} 10-20:{b1} 20-30:{b2} >=30:{b3} min={d.Min():0.0} max={d.Max():0.0} avg={d.Average():0.0}";
                    }
                    var wallDeltas = ticks.Zip(ticks.Skip(1), (p, n) => n.wall - p.wall);
                    var renderDeltas = ticks.Zip(ticks.Skip(1), (p, n) => n.render - p.render);
                    var dupRender = ticks.Zip(ticks.Skip(1), (p, n) => n.render == p.render).Count(x => x);
                    var frames = ticks.Where(t => t.frame > 0).Select(t => t.frame).Distinct().ToList();
                    var frameDeltas = frames.Zip(frames.Skip(1), (p, n) => (n - p) / 1_000_000.0);
                    var ticksPerFrame = ticks.Count(t => t.frame > 0) / Math.Max(1.0, frames.Count);
                    Console.WriteLine($"[DevSnapshot] pace ticks(wall): {Hist(wallDeltas)}");
                    Console.WriteLine($"[DevSnapshot] pace ticks(RenderingTime): {Hist(renderDeltas)} sameRenderingTimeTicks={dupRender}");
                    Console.WriteLine($"[DevSnapshot] pace drawn frames: {Hist(frameDeltas)} ticksPerDrawnFrame={ticksPerFrame:0.00}");
                    var seq = string.Join(" ", ticks.Zip(ticks.Skip(1), (p, n) => $"{n.wall - p.wall:0}{(n.frame != p.frame ? "*" : "")}").Take(120));
                    Console.WriteLine($"[DevSnapshot] pace first ticks (ms, * = new frame): {seq}");
                }

                // Reports where the first scroll in the tree ended up, so a wheel run has a number to compare.
                var scroll = (drawn.Canvas.Content as DrawnUi.Draw.SkiaControl)?.FindView<DrawnUi.Draw.SkiaScroll>();
                Console.WriteLine($"[DevSnapshot] wheel: notches={notches} delay={pause}ms offsetY={scroll?.ViewportOffsetY:0.#} offsetX={scroll?.ViewportOffsetX:0.#}");
            }

            // HELLOWPF_TAP="x,y" taps the canvas at those PIXEL coordinates before capturing, so a
            // page can be driven through one of its own buttons.
            var tap = Environment.GetEnvironmentVariable("HELLOWPF_TAP");
            if (element is DrawnUi.Wpf.DrawnUiElement tapTarget && !string.IsNullOrWhiteSpace(tap))
            {
                // several taps: "x,y;x,y" with HELLOWPF_TAP_DELAY ms between them (default 1500).
                var tapDelay = int.TryParse(Environment.GetEnvironmentVariable("HELLOWPF_TAP_DELAY"), out var td) ? td : 1500;
                foreach (var one in tap.Split(';'))
                {
                    var parts = one.Split(',');
                    if (parts.Length == 2
                        && float.TryParse(parts[0], out var x)
                        && float.TryParse(parts[1], out var y))
                    {
                        var w = (float)(tapTarget.ActualWidth * 2);
                        var h = (float)(tapTarget.ActualHeight * 2);
                        tapTarget.Canvas.HandleDesktopPointerDown(x, y, w, h);
                        tapTarget.Canvas.HandleDesktopPointerUp(x, y, w, h);
                        await Task.Delay(tapDelay);
                    }
                }
            }

            // HELLOWPF_DRAG="x,y,dx,dy" drags the pointer from (x,y) by (dx,dy) pixels in 12 steps
            // (several drags: "x,y,dx,dy;x,y,dx,dy"), for pull-to-refresh, snapping and drawers.
            var drag = Environment.GetEnvironmentVariable("HELLOWPF_DRAG");
            if (element is DrawnUi.Wpf.DrawnUiElement dragTarget && !string.IsNullOrWhiteSpace(drag))
            {
                foreach (var one in drag.Split(';'))
                {
                    var p = one.Split(',');
                    if (p.Length != 4 || !float.TryParse(p[0], out var x) || !float.TryParse(p[1], out var y)
                        || !float.TryParse(p[2], out var dx) || !float.TryParse(p[3], out var dy))
                        continue;

                    var w = (float)(dragTarget.ActualWidth * 2);
                    var h = (float)(dragTarget.ActualHeight * 2);
                    dragTarget.Canvas.HandleDesktopPointerDown(x, y, w, h);
                    for (var i = 1; i <= 12; i++)
                    {
                        await Task.Delay(16);
                        dragTarget.Canvas.HandleDesktopPointerMove(x + dx * i / 12f, y + dy * i / 12f, true, w, h);
                    }

                    await Task.Delay(16);
                    dragTarget.Canvas.HandleDesktopPointerUp(x + dx, y + dy, w, h);
                    await Task.Delay(1500);
                }
            }

            // HELLOWPF_TYPE="text" types through the real WPF input path (SendInput unicode events),
            // HELLOWPF_KEYS="Enter,Shift+Left,Ctrl+A" presses virtual keys; both need the window focused.
            var type = Environment.GetEnvironmentVariable("HELLOWPF_TYPE");
            var keys = Environment.GetEnvironmentVariable("HELLOWPF_KEYS");
            if (!string.IsNullOrEmpty(type) || !string.IsNullOrEmpty(keys))
            {
                var owner = Window.GetWindow(element);
                owner?.Activate();
                if (owner != null)
                    SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(owner).Handle);
                // A real click is the only reliable way to become the foreground window (SendInput
                // targets the foreground window): click the drawn nav bar title, which does nothing.
                if (owner != null)
                    owner.Topmost = true; // above whatever launched us, so the click lands on this window
                await Task.Delay(200);
                var screen = element.PointToScreen(new WpfPoint(element.ActualWidth / 2, 20));
                SetCursorPos((int)screen.X, (int)screen.Y);
                mouse_event(0x0002, 0, 0, 0, 0);
                mouse_event(0x0004, 0, 0, 0, 0);
                await Task.Delay(300);
                element.Focus();
                System.Windows.Input.Keyboard.Focus(element);
                await Task.Delay(300);
                var fg = GetForegroundWindow();
                var title = new System.Text.StringBuilder(256);
                GetWindowText(fg, title, 256);
                Console.WriteLine($"[DevSnapshot] keyboard: active={owner?.IsActive} foreground='{title}' focused={System.Windows.Input.Keyboard.FocusedElement?.GetType().Name}");
                _hwnd = owner != null ? new System.Windows.Interop.WindowInteropHelper(owner).Handle : 0;
                // HELLOWPF_TYPE may interleave text and keys: "abc{Enter}de{Up}X"
                if (!string.IsNullOrEmpty(type))
                {
                    for (var i = 0; i < type.Length; i++)
                    {
                        var close = type[i] == '{' ? type.IndexOf('}', i) : -1;
                        if (close > i)
                        {
                            await Task.Delay(150); // let the editor lay the text out before a line move
                            PressCombo(type.Substring(i + 1, close - i - 1));
                            await Task.Delay(150);
                            i = close;
                            continue;
                        }

                        SendUnicode(type[i]);
                        await Task.Delay(30);
                    }
                }

                if (!string.IsNullOrEmpty(keys))
                {
                    foreach (var combo in keys.Split(','))
                    {
                        PressCombo(combo.Trim());
                        await Task.Delay(120);
                    }
                }

                await Task.Delay(800);
                if (owner != null)
                    owner.Topmost = false;
            }

            Capture(element, path);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HELLOWPF_EXIT")))
                Application.Current.Shutdown();
        };
        timer.Start();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct KeyInput
    {
        public uint Type;
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
        public long Padding; // INPUT is a union sized for MOUSEINPUT
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint SendInput(uint count, KeyInput[] inputs, int size);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hwnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nint extra);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, nint extra);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetWindowText(nint hwnd, System.Text.StringBuilder text, int max);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);

    private static nint _hwnd;

    // Messages are posted to our own window: SendInput would need the foreground window, which a
    // process started from a script never gets. WPF's keyboard provider reads them from the queue.
    private static void SendUnicode(char ch) => PostMessage(_hwnd, 0x0102, ch, 1); // WM_CHAR

    private static readonly Dictionary<string, byte> VirtualKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Shift"] = 0x10, ["Ctrl"] = 0x11, ["Alt"] = 0x12, ["Enter"] = 0x0D, ["Back"] = 0x08, ["Tab"] = 0x09,
        ["Delete"] = 0x2E, ["Home"] = 0x24, ["End"] = 0x23, ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27,
        ["Down"] = 0x28, ["Escape"] = 0x1B, ["Space"] = 0x20, ["F5"] = 0x74,
    };

    private static void PressCombo(string combo)
    {
        var parts = combo.Split('+');
        var codes = new List<byte>();
        foreach (var part in parts)
        {
            if (VirtualKeys.TryGetValue(part, out var vk))
                codes.Add(vk);
            else if (part.Length == 1)
                codes.Add((byte)char.ToUpperInvariant(part[0]));
        }

        // Everything as WM_KEYDOWN / WM_KEYUP to our window. Posted modifiers reach KeyboardManager
        // but not the thread key-state table behind Keyboard.Modifiers, so Shift+arrow selection in
        // the editor is a manual check.
        foreach (var vk in codes)
            PostMessage(_hwnd, 0x0100, vk, 1);
        for (var i = codes.Count - 1; i >= 0; i--)
            PostMessage(_hwnd, 0x0101, codes[i], (nint)(1 | (1L << 30) | (1L << 31)));
    }

    private static void Capture(FrameworkElement element, string path)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            return;

        var dpi = VisualTreeHelper.GetDpi(element);
        var target = new RenderTargetBitmap(
            (int)(element.ActualWidth * dpi.DpiScaleX),
            (int)(element.ActualHeight * dpi.DpiScaleY),
            96 * dpi.DpiScaleX,
            96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32);

        target.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = File.Create(path);
        encoder.Save(stream);

        Console.WriteLine($"[snapshot] {path}");
    }
}
