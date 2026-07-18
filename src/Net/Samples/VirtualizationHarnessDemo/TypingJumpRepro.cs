using DrawnChatList;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Repro for "chat junky-jumps while the AI mock answer types": the REAL ChatPage at rest at newest,
/// StartMockAiAnswer() grows one incoming cell's Text word-by-word (~180ms real time).
/// Structure probe (content coords): history anchors' Y must only GROW (they slide up on screen as the
/// cell above them in content order gets taller); offset stays 0; no blanks.
/// PIXEL probe: per-frame RowSignature of the pure-history band (y 120..600 screen) cross-correlated
/// between consecutive frames — the visual shift must be 0 or NEGATIVE (history up); any POSITIVE shift
/// or an up/down oscillation = the reported junky jump (e.g. stale plane blitted at the old position).
/// </summary>
public static class TypingJumpRepro
{
    public static void Run()
    {
        // BOTH plane modes, explicitly (never trust the base default): the sync single-plane path is what
        // ships (default FALSE) and streaming poisons it differently than the async path — an unscoped
        // 2026-07-17 fix made every sync record self-discard during streaming (record+discard+live thrash,
        // "blinking, structure changing every frame") and NO gate covered single-plane + streaming.
        Run(false);
        Run(true);
    }

    public static void Run(bool useDoubleBuffering)
    {
        Console.WriteLine();
        Console.WriteLine($"============== TYPING-GROW JUMP (real ChatPage, UseDoubleBuffering={useDoubleBuffering}) ==============");
        try { RunCore(useDoubleBuffering); }
        catch (Exception ex) { Console.WriteLine($"  CRASH: {ex}"); }
        Console.WriteLine("==============================================================");
    }

    static void RunCore(bool useDoubleBuffering)
    {
        var page = new ChatPage();
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: ChatTheme.Bg);
        host.Canvas.Content = page.CreateCanvasContent();
        page.InitializeList();
        page.ChatStack.UseDoubleBuffering = useDoubleBuffering;

        for (int i = 0; i < 400 && page.ChatStack.LastVisibleIndex < 0; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        for (int i = 0; i < 60; i++) { host.RenderFrame(16); Thread.Sleep(3); }
        Console.WriteLine($"  start: offY={page.MainScroll.ViewportOffsetY:0} vis=[{page.ChatStack.FirstVisibleIndex}..{page.ChatStack.LastVisibleIndex}] fill={host.NonBackgroundFraction(ChatTheme.Bg):0.00}");

        page.ProbeStartAiMock();
        for (int i = 0; i < 40; i++) { host.RenderFrame(16); Thread.Sleep(4); }

        int growIdx = -1;
        foreach (var t in page.ChatStack.RenderTree)
            if (t.FreezeBindingContext is ChatMessage m && m.Index > growIdx) growIdx = m.Index;
        Console.WriteLine($"  growing msg index = {growIdx}");

        var anchors = new List<int>();
        foreach (var t in page.ChatStack.RenderTree)
        {
            if (t.FreezeBindingContext is ChatMessage m && m.Index != growIdx
                && t.HitRect.Top > 100 && t.HitRect.Bottom < 700)
                anchors.Add(m.Index);
            if (anchors.Count == 3) break;
        }

        const int bandTop = 120, bandBot = 600;
        var lastY = new Dictionary<int, float>();
        long[] prevSig = host.RowSignature(bandTop, bandBot);
        float lastOff = page.MainScroll.ViewportOffsetY;
        int reversals = 0, snaps = 0, offMoves = 0, blanks = 0;
        int posShifts = 0, oscillations = 0, lastShift = 0;
        float worstSnap = 0;
        int worstPosShift = 0;

        var until = Environment.TickCount64 + 6000;
        int frame = 0;
        while (Environment.TickCount64 < until)
        {
            host.RenderFrame(16);
            Thread.Sleep(6);
            frame++;

            double fill = host.NonBackgroundFraction(ChatTheme.Bg);
            if (fill < 0.15) blanks++;

            float off = page.MainScroll.ViewportOffsetY;
            if (Math.Abs(off - lastOff) > 1) offMoves++;
            lastOff = off;

            // PIXEL shift of the pure-history band
            var sig = host.RowSignature(bandTop, bandBot);
            int shift = HeadlessCanvasHost.EstimateVerticalShift(prevSig, sig, 40);
            prevSig = sig;
            if (shift > 1)
            {
                posShifts++;
                if (shift > worstPosShift) worstPosShift = shift;
                if (posShifts <= 10)
                    Console.WriteLine($"   f{frame,3} PIXEL history moved DOWN {shift}px");
            }
            if (shift != 0 && lastShift != 0 && Math.Sign(shift) != Math.Sign(lastShift))
            {
                oscillations++;
                if (oscillations <= 10)
                    Console.WriteLine($"   f{frame,3} PIXEL oscillation {lastShift} -> {shift}");
            }
            if (shift != 0) lastShift = shift; // remember last non-zero move for oscillation pairing

            // STRUCTURE (content coords): history anchor Y may only GROW while the newest cell gets taller
            var tree = page.ChatStack.RenderTree;
            if (tree == null) continue;
            foreach (var t in tree)
            {
                if (t.FreezeBindingContext is not ChatMessage m || !anchors.Contains(m.Index)) continue;
                float y = t.HitRect.Top;
                if (lastY.TryGetValue(m.Index, out var prev))
                {
                    float d = y - prev;
                    if (d < -2)
                    {
                        reversals++;
                        if (reversals <= 8)
                            Console.WriteLine($"   f{frame,3} REVERSAL msg{m.Index} contentY {prev:0} -> {y:0} ({d:0})");
                    }
                    if (Math.Abs(d) > 40)
                    {
                        snaps++;
                        if (Math.Abs(d) > worstSnap) worstSnap = Math.Abs(d);
                        if (snaps <= 8)
                            Console.WriteLine($"   f{frame,3} SNAP msg{m.Index} contentY {prev:0} -> {y:0} ({d:+0;-0})");
                    }
                }
                lastY[m.Index] = y;
            }
        }

        page.ProbeStopAiMock();
        for (int i = 0; i < 20; i++) { host.RenderFrame(16); Thread.Sleep(3); }

        Console.WriteLine($"  frames={frame} anchors=[{string.Join(",", anchors)}]");
        Console.WriteLine($"  STRUCTURE: reversals={reversals} snaps(>40px)={snaps} worstSnap={worstSnap:0}px offsetMoves={offMoves} blankFrames={blanks}");
        Console.WriteLine($"  PIXELS:    downShifts={posShifts} worstDown={worstPosShift}px oscillations={oscillations}");
        Console.WriteLine(reversals == 0 && snaps == 0 && blanks == 0 && posShifts == 0 && oscillations == 0
            ? "=> PASS (typing grows smoothly: history slides up only, no pixel wobble)"
            : "=> FAIL (junky typing reproduced)");

        // ---- phase B: user scrolled AWAY reading history while the AI cell grows below ----
        // The reading position must stay GLUED: growth of a cell that sits BEFORE the viewport in
        // content order shifts all offsets — without compensation every word-wrap moves the text the
        // user is reading. Pixel probe = ground truth: the mid-screen band must not move at all.
        var robot = new GestureRobot(host);
        robot.Pan(215, 250, 215, 700, durationMs: 90, steps: 8); // inverted: drag down = into history
        robot.Pan(215, 250, 215, 700, durationMs: 90, steps: 8);
        robot.Pan(215, 250, 215, 700, durationMs: 90, steps: 8);
        float lastOffB = float.NaN;
        for (int f = 0; f < 200; f++)
        {
            host.RenderFrame(16); Thread.Sleep(3);
            if (f > 20 && Math.Abs(page.MainScroll.ViewportOffsetY - lastOffB) < 0.01f) break;
            lastOffB = page.MainScroll.ViewportOffsetY;
        }

        Console.WriteLine($"  phase B: scrolled away, offY={page.MainScroll.ViewportOffsetY:0} vis=[{page.ChatStack.FirstVisibleIndex}..{page.ChatStack.LastVisibleIndex}]");

        page.ProbeStartAiMock();
        for (int i = 0; i < 30; i++) { host.RenderFrame(16); Thread.Sleep(4); }

        prevSig = host.RowSignature(bandTop, bandBot);
        int shiftsB = 0, worstB = 0, framesB = 0;
        var untilB = Environment.TickCount64 + 6000;
        while (Environment.TickCount64 < untilB)
        {
            host.RenderFrame(16); Thread.Sleep(6); framesB++;
            var sig = host.RowSignature(bandTop, bandBot);
            int shift = HeadlessCanvasHost.EstimateVerticalShift(prevSig, sig, 40);
            prevSig = sig;
            if (Math.Abs(shift) > 1)
            {
                shiftsB++;
                if (Math.Abs(shift) > worstB) worstB = Math.Abs(shift);
                if (shiftsB <= 10)
                    Console.WriteLine($"   fB{framesB,3} PIXEL reading band moved {shift:+0;-0}px (offY={page.MainScroll.ViewportOffsetY:0})");
            }
        }

        page.ProbeStopAiMock();
        Console.WriteLine($"  phase B: frames={framesB} bandMoves={shiftsB} worst={worstB}px");
        Console.WriteLine(shiftsB == 0
            ? "=> PASS (reading position glued while AI types below)"
            : "=> FAIL (reading position jumps while AI types — reproduced)");

        // ---- phase C: JUMP CYCLE while the AI streams (device repro 2026-07-12) ----
        // Scroll to oldest -> start the stream -> 500ms -> jump to newest. The transition hold used to
        // blit ForegroundPlane (NOT a frozen copy): the pipeline underneath swapped/disposed/re-anchored
        // it mid-hold -> fully BLANK frames and the pre-jump (oldest) world flashing back AFTER landing.
        // Assertions: zero blank frames through the whole cycle; once landed at newest with the hold
        // released, no frame may show the oldest-band pixels again.
        page.ProbeScrollToOldest(true);
        float lastOffC = float.NaN;
        for (int f = 0; f < 600; f++)
        {
            host.RenderFrame(16); Thread.Sleep(4);
            float o = page.MainScroll.ViewportOffsetY;
            if (f > 40 && Math.Abs(o - lastOffC) < 0.01f && page.ChatStack.LastVisibleIndex >= 0
                && !page.ChatStack.DebugString.Contains("HOLD"))
                break;
            lastOffC = o;
        }
        Console.WriteLine($"  phase C: at oldest offY={page.MainScroll.ViewportOffsetY:0} vis=[{page.ChatStack.FirstVisibleIndex}..{page.ChatStack.LastVisibleIndex}]");
        var sigOldest = host.RowSignature(bandTop, bandBot);

        page.ProbeStartAiMock();
        var startWait = Environment.TickCount64 + 500; // user repro: "wait like 500ms"
        while (Environment.TickCount64 < startWait) { host.RenderFrame(16); Thread.Sleep(6); }

        page.ProbeScrollToNewest(true);
        int blanksC = 0, oldWorld = 0, framesC = 0;
        bool landedC = false;
        // post-landing glue probe (device 2026-07-12: landed ~48px SHORT — newest bubble floated with a
        // background gap for >1s, then the whole band SNAPPED when the extent caught up).
        int snapsC = 0, worstSnapC = 0;
        long[] prevHist = null;
        var untilC = Environment.TickCount64 + 7000; // covers hold + landing + rest of the stream
        while (Environment.TickCount64 < untilC)
        {
            host.RenderFrame(16); Thread.Sleep(6); framesC++;

            double fill = host.NonBackgroundFraction(ChatTheme.Bg);
            if (fill < 0.15)
            {
                blanksC++;
                if (blanksC <= 8)
                    Console.WriteLine($"   fC{framesC,3} BLANK fill={fill:0.00} offY={page.MainScroll.ViewportOffsetY:0}");
            }

            if (!landedC)
            {
                landedC = Math.Abs(page.MainScroll.ViewportOffsetY) < 1
                          && page.ChatStack.FirstVisibleIndex <= 1
                          && !page.ChatStack.DebugString.Contains("HOLD");
                continue;
            }

            if (SignaturesMatch(host.RowSignature(bandTop, bandBot), sigOldest))
            {
                oldWorld++;
                if (oldWorld <= 8)
                    Console.WriteLine($"   fC{framesC,3} OLD-WORLD band (oldest content after landing) offY={page.MainScroll.ViewportOffsetY:0}");
            }

            // SNAP: at rest at the newest edge the HISTORY band may slide smoothly (text grows a line at a
            // time) but a one-frame jump >= half a line height is the visible "scroll jump" (device: landed
            // ~48px short during the stream, floated, then the whole band snapped when the extent caught up).
            var hist = host.RowSignature(200, 620);
            if (prevHist != null)
            {
                int dy = HeadlessCanvasHost.EstimateVerticalShift(prevHist, hist, 60);
                if (Math.Abs(dy) > 24)
                {
                    snapsC++;
                    if (Math.Abs(dy) > worstSnapC) worstSnapC = Math.Abs(dy);
                    if (snapsC <= 8)
                        Console.WriteLine($"   fC{framesC,3} SNAP band jumped {dy:+0;-0}px offY={page.MainScroll.ViewportOffsetY:0} [{page.ChatStack.DebugString}]");
                }
            }
            prevHist = hist;
        }

        page.ProbeStopAiMock();
        Console.WriteLine($"  phase C: frames={framesC} landed={landedC} blankFrames={blanksC} oldWorldFrames={oldWorld} snaps={snapsC} worstSnap={worstSnapC}px");
        Console.WriteLine(landedC && blanksC == 0 && oldWorld == 0 && snapsC == 0
            ? "=> PASS (jump during stream: no blanks, no old world, no snaps)"
            : "=> FAIL (jump-during-stream corruption reproduced)");
    }

    /// <summary>Same band content? Mean per-row |diff| tiny relative to the band's own magnitude.</summary>
    static bool SignaturesMatch(long[] a, long[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return false;
        double diff = 0, mag = 1;
        for (int i = 0; i < a.Length; i++)
        {
            diff += Math.Abs(a[i] - (double)b[i]);
            mag += Math.Abs((double)b[i]);
        }
        return diff / mag < 0.01;
    }
}
