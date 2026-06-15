using System.Net.Http;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.OpenTk;
using DrawnUi.Views;
using OpenTkChatPlanes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SkiaSharp;


Super.UseDrawnUi()
    .Build();

// OpenTk has no DI host, so Super.Services is null and SkiaImageManager.GetHttpClient() returns null
// -> remote (URL) images never load (blank/gray banners). Register a minimal provider that hands out
// a shared HttpClient so SkiaImageManager can fetch https image sources.
Super.Services = new SimpleHttpServiceProvider();

var scene = new ChatPlanesScene();

var canvas = scene.BuildCanvas();

var gameSettings = new GameWindowSettings
{
    UpdateFrequency = 0
};

var nativeSettings = new NativeWindowSettings
{
    ClientSize = new Vector2i(440, 820),
    Title = "DrawnUi Chat Planes (GPU)",
    API = ContextAPI.OpenGL,
    APIVersion = new Version(3, 3),
    Profile = ContextProfile.Core,
    WindowState = WindowState.Normal,
};

using var window = new ChatPlanesWindow(gameSettings, nativeSettings, canvas, scene);

window.Run();

namespace OpenTkChatPlanes
{
    /// <summary>
    /// Minimal IServiceProvider for the OpenTk sample: hands SkiaImageManager a shared HttpClient so
    /// remote (URL) image sources load. Avoids pulling in the full DI container package.
    /// </summary>
    internal sealed class SimpleHttpServiceProvider : IServiceProvider
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
        public object GetService(Type serviceType) => serviceType == typeof(HttpClient) ? _http : null;
    }

    /// <summary>
    /// Drives the chat scroll with a scripted, repeating pan flick (a GPU "gesture robot" using the
    /// canvas pointer API) so LoadMore + the 150-cap trim fire automatically; logs scroll/window state
    /// to the console each flick and captures a few GL framebuffer PNGs. Auto-closes when done.
    /// </summary>
    public sealed class ChatPlanesWindow : DrawnUiWindow
    {
        private readonly Canvas _canvas;
        private readonly ChatPlanesScene _scene;
        private int _f;          // frame counter
        private int _flicks;     // completed flicks
        private bool _reversed;  // false = into history (down-drag), true = back toward newest
        private readonly string _outDir;

        private const int FlickPeriod = 14;   // frames per flick (drag + settle)
        private const int FlicksPhase1 = 30;  // into history
        private const int FlicksTotal = 60;   // then back toward newest

        public ChatPlanesWindow(GameWindowSettings g, NativeWindowSettings n, Canvas canvas, ChatPlanesScene scene)
            : base(g, n, canvas)
        {
            _canvas = canvas;
            _scene = scene;
            _outDir = AppContext.BaseDirectory;
        }

        private bool _bounceDone;
        private int _bf; // bounce-phase frame counter

        private long _lastFrameTicks;
        private double _maxFrameMs;   // worst frame since last flick log
        private int _frameSamples;
        private double _sumFrameMs; 

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            if (ClientSize.X <= 0 || ClientSize.Y <= 0)
                return;

            // frame-time tracking (lag = spikes). Excludes the very first frame.
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_lastFrameTicks != 0)
            {
                double ms = (now - _lastFrameTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (ms > _maxFrameMs) _maxFrameMs = ms;
                _sumFrameMs += ms;
                _frameSamples++;
            }
            _lastFrameTicks = now;

            return; //uncomment for user manual testing  (comment out to run WalkAndDump probe)

            // PHASE 0: overscroll the BOTTOM (newest) edge and watch the spring-back every frame.
            // At rest the inverted chat sits at offY≈0 (newest). Dragging UP (bottom->top) pulls past the
            // edge -> offY should go POSITIVE then bounce back to 0. If it sticks (no return to 0) = no bounce.
            if (!_bounceDone)
            {
                float cx0 = ClientSize.X / 2f;
                if (_bf == 0) _canvas.HandleDesktopPointerDown(cx0, 600, ClientSize.X, ClientSize.Y);
                else if (_bf >= 1 && _bf <= 6) _canvas.HandleDesktopPointerMove(cx0, 600 - (440f * _bf / 6f), true, ClientSize.X, ClientSize.Y);
                else if (_bf == 7) _canvas.HandleDesktopPointerUp(cx0, 160, ClientSize.X, ClientSize.Y);

                if (_bf >= 1)
                    Console.WriteLine($"bounce f{_bf,2} offY={_scene.MainScroll.ViewportOffsetY,7:0.0} contentH={_scene.MainScroll.ContentSize.Pixels.Height:0}");

                if (_bf == 6) Capture("bounce-peak.png");   // peak overscroll — content should be pushed down
                if (_bf == 13) Capture("bounce-rest.png");  // settled back at newest

                _bf++;
                if (_bf >= 40)
                {
                    _bounceDone = true;
                    Console.WriteLine(">>> bounce phase done, scrolling into history");
                }
                return;
            }

            //ScenarioTest(); // uncomment to run 4-scenario tap verification
            //DriveAutoPan();
            //DayChipBugTest();
            WalkAndDump();

            _f++;

            _scene.MaybeTrimDeferred(); // deferred far-side trim: only fires on settled frames
            CheckStructureIntegrity();

            // capture a few frames across the run for visual inspection of plane content / trim
            if (_flicks is 6 or 16 or 26 or 40 or 52 && _f % FlickPeriod == FlickPeriod - 1)
                Capture($"chat-gpu-flick{_flicks}.png");

            if (_flicks >= FlicksTotal)
            {
                Console.WriteLine($"done. violations={_violations} older={_scene.LoadOlderCalls} newer={_scene.LoadNewerCalls} trim={_scene.TrimEvents}");
                Close();
            }
        }

        private float _tapY = 60;
        private int _tapPhase;
        private bool _tapScrolledToImage;
        private int _tapSettle;
        private int _violations;

        // Reads the layout's gesture RenderTree each frame and asserts structural integrity of the visible
        // cells: unique local index, contiguous indices, monotonic non-overlapping rects, and height that
        // matches the bound message TYPE (image cells tall, text short). Dumps the exact corruption signature
        // when violated — the runtime evidence for the structure race.
        private void CheckStructureIntegrity()
        {
            var tree = _scene.ChatStack?.RenderTree;
            if (tree == null || tree.Count < 2)
                return;

            var items = new List<(int idx, int msg, float top, float bottom)>();
            foreach (var t in tree)
            {
                if (t.FreezeBindingContext is DrawnUiRepro.ChatMessage m)
                    items.Add((t.FreezeIndex, m.Index, t.HitRect.Top, t.HitRect.Bottom));
            }
            if (items.Count < 2)
                return;

            items.Sort((a, b) => a.idx.CompareTo(b.idx)); // sort by local cell index

            var problems = new List<string>();
            var seen = new HashSet<int>();
            foreach (var it in items)
                if (!seen.Add(it.idx))
                    problems.Add($"DUP-idx={it.idx}(msg{it.msg})"); // one pooled view bound to two indices

            for (int i = 1; i < items.Count; i++)
            {
                var a = items[i - 1];
                var b = items[i];
                if (b.idx - a.idx != 1)
                {
                    problems.Add($"IDXGAP {a.idx}->{b.idx}");          // skipped/duplicated local slot
                    continue;
                }
                // consecutive local indices must map to consecutive messages (inverted window => step -1)
                if (b.msg - a.msg != -1)
                    problems.Add($"MSGBIND idx{a.idx}=msg{a.msg} idx{b.idx}=msg{b.msg}"); // wrong message bound
                // geometry (HitRect): higher local idx tiles DOWNWARD (bigger Top). Adjacent cells must
                // touch within Spacing — not overlap, not leave a big gap.
                float gap = b.top - a.bottom; // next top - prev bottom ~= spacing (~4px)
                if (gap < -2f) problems.Add($"OVERLAP idx{a.idx}(b{a.bottom:0})>idx{b.idx}(t{b.top:0})");
                else if (gap > 60f) problems.Add($"GAP {gap:0} idx{a.idx}->idx{b.idx}");
            }

            if (problems.Count > 0)
            {
                _violations++;
                if (_violations <= 12)
                {
                    Console.WriteLine($"[INTEGRITY f{_f} flick{_flicks} older{_scene.LoadOlderCalls} newer{_scene.LoadNewerCalls} trim{_scene.TrimEvents} resident{_scene.ResidentCount} win{_scene.WindowStart}..{_scene.WindowEnd}] "
                        + string.Join(" | ", problems.GetRange(0, Math.Min(8, problems.Count))));
                }
            }
        }

        // Deterministic: scroll a known IMAGE cell to the top, let it settle, then tap a small region over
        // it (both columns) — does the inner image hit-rect register the tap (ShowImageFullscreen) after a
        // scroll? child= cell-level hit, image= inner-image hit.
        private void TapSweep()
        {
            if (!_tapScrolledToImage)
            {
                int li = _scene.MiddleImageLocal();
                if (li >= 0) _scene.MainScroll.ScrollToIndex(li, false, DrawnUi.Draw.RelativePositionType.Start, false);
                _tapScrolledToImage = true;
                _tapSettle = 12;
                Console.WriteLine($"scrolled image local={li} to top");
                return;
            }
            if (_tapSettle-- > 0) return;

            float tx = (_tapY % 80 < 40) ? ClientSize.X * 0.30f : ClientSize.X * 0.70f; // alternate columns
            if (_tapPhase == 0)
            {
                _scene.LastChildIndex = -1; _scene.LastImageIndex = -1;
                _canvas.HandleDesktopPointerDown(tx, _tapY, ClientSize.X, ClientSize.Y);
                _tapPhase = 1;
            }
            else if (_tapPhase == 1)
            {
                _canvas.HandleDesktopPointerUp(tx, _tapY, ClientSize.X, ClientSize.Y);
                _tapPhase = 2;
            }
            else
            {
                var l = _scene.ChatStack;
                Console.WriteLine($"tap y={_tapY,4:0} x={tx,4:0} vis=[{l.FirstVisibleIndex}..{l.LastVisibleIndex}] " +
                    $"child={_scene.LastChildIndex} image={_scene.LastImageIndex}");
                _tapY += 20;
                _tapPhase = 0;
                if (_tapY > 780) { Console.WriteLine("done."); Close(); }
            }
        }

        // 4-scenario tap test:
        //  S1: tap a cell at initial position
        //  S2: scroll ~half screen, tap same cell
        //  S3: scroll 10 screens, find image cell, tap first half
        //  S4: tap second half of same image cell
        private int _stPhase;
        private int _stSettle, _stScrollStep, _stFlick;
        private float _stTapX, _stTapY;
        private int _stS1child, _stS2child, _stS3img, _stS4img;
        private float _stS1tapY, _stS2tapY, _stS3tapY, _stS4tapY;
        // image cell sweep state
        private float _stSweepY;
        private float _stImgFirstY = -1, _stImgLastY = -1; // Y range of image cell found in sweep
        private int _stSweepPhase; // 0=down 1=up 2=check
        private bool _stSweepDone;

        // Emit a single tap and return true when the up event fires (takes 2 frames)
        private int _tapPhase2;
        private bool QuickTap(float x, float y)
        {
            if (_tapPhase2 == 0) { _canvas.HandleDesktopPointerDown(x, y, ClientSize.X, ClientSize.Y); _tapPhase2 = 1; return false; }
            if (_tapPhase2 == 1) { _canvas.HandleDesktopPointerUp(x, y, ClientSize.X, ClientSize.Y); _tapPhase2 = 0; return true; }
            return false;
        }

        // Perform one scroll flick (drag from fromY to toY over 10 frames); returns true when done
        private int _flkStep;
        private bool QuickFlick(float fromY, float toY)
        {
            float cx = ClientSize.X / 2f;
            if (_flkStep == 0) { _canvas.HandleDesktopPointerDown(cx, fromY, ClientSize.X, ClientSize.Y); _flkStep++; return false; }
            if (_flkStep >= 1 && _flkStep <= 8) { float t = _flkStep / 8f; _canvas.HandleDesktopPointerMove(cx, fromY + (toY - fromY) * t, true, ClientSize.X, ClientSize.Y); _flkStep++; return false; }
            if (_flkStep == 9) { _canvas.HandleDesktopPointerUp(cx, toY, ClientSize.X, ClientSize.Y); _flkStep++; return false; }
            if (_flkStep >= 10 && ++_stSettle >= 15) { _flkStep = 0; _stSettle = 0; return true; }
            return false;
        }

        private void ScenarioTest()
        {
            float cx = ClientSize.X / 2f;
            float vpH = ClientSize.Y;

            switch (_stPhase)
            {
                // ── settle after bounce ──────────────────────────────────────────────────
                case 0:
                    if (++_stSettle >= 20)
                    {
                        _stSettle = 0;
                        _stTapY = vpH / 3f; _stTapX = cx;
                        _scene.LastChildIndex = -1;
                        _stPhase = 1;
                        Console.WriteLine($"[S1] tapping at Y={_stTapY:0}");
                    }
                    break;

                // ── S1: tap at initial position ──────────────────────────────────────────
                case 1:
                    if (QuickTap(_stTapX, _stTapY)) _stPhase = 2;
                    break;
                case 2:
                    _stS1child = _scene.LastChildIndex; _stS1tapY = _stTapY;
                    Console.WriteLine($"[S1] child={_stS1child} y={_stS1tapY:0} -> {(_stS1child >= 0 ? "PASS" : "FAIL")}");
                    _scene.LastChildIndex = -1; _stPhase = 3;
                    break;

                // ── half-screen scroll ───────────────────────────────────────────────────
                case 3:
                    // drag upward half-screen (into history)
                    if (QuickFlick(vpH * 0.8f, vpH * 0.3f)) { Console.WriteLine($"[S2] tapping at Y={_stTapY:0} after half-screen scroll"); _stPhase = 4; }
                    break;

                // ── S2: tap same position after scroll ──────────────────────────────────
                case 4:
                    if (QuickTap(_stTapX, _stTapY)) _stPhase = 5;
                    break;
                case 5:
                    _stS2child = _scene.LastChildIndex; _stS2tapY = _stTapY;
                    Console.WriteLine($"[S2] child={_stS2child} y={_stS2tapY:0} -> {(_stS2child >= 0 ? "PASS" : "FAIL")}");
                    _scene.LastChildIndex = -1; _scene.LastImageIndex = -1;
                    _stFlick = 0; _stPhase = 6;
                    Console.WriteLine("[S3/S4] scrolling 10 flicks into history...");
                    break;

                // ── 10 more flicks into history ──────────────────────────────────────────
                case 6:
                    if (QuickFlick(vpH * 0.85f, vpH * 0.15f)) // full-ish drag each flick
                    {
                        if (++_stFlick >= 10) { _stSettle = 0; _stPhase = 7; Console.WriteLine("[S3/S4] settling..."); }
                    }
                    break;

                // ── settle, then sweep to find image cell ────────────────────────────────
                case 7:
                    if (++_stSettle >= 25)
                    {
                        _stSettle = 0; _stSweepY = 40; _stImgFirstY = -1; _stImgLastY = -1; _stSweepDone = false;
                        _stPhase = 8;
                        Console.WriteLine("[S3/S4] sweeping Y to find image cell...");
                    }
                    break;

                // ── sweep Y 40..780 in 20px steps to locate image cell ───────────────────
                case 8:
                    if (_stSweepDone) { _stPhase = 9; break; }
                    _scene.LastChildIndex = -1; _scene.LastImageIndex = -1;
                    if (QuickTap(cx, _stSweepY))
                    {
                        if (_scene.LastImageIndex >= 0)
                        {
                            if (_stImgFirstY < 0) _stImgFirstY = _stSweepY;
                            _stImgLastY = _stSweepY;
                        }
                        _stSweepY += 20;
                        if (_stSweepY > 780) _stSweepDone = true;
                    }
                    break;

                // ── report sweep, compute S3/S4 taps ────────────────────────────────────
                case 9:
                    Console.WriteLine($"[sweep] image cell Y range: [{_stImgFirstY:0}..{_stImgLastY:0}]");
                    if (_stImgFirstY < 0) { Console.WriteLine("  no image cell found - ABORT"); Close(); break; }
                    float imgMid = (_stImgFirstY + _stImgLastY) / 2f;
                    _stS3tapY = _stImgFirstY + (_stImgFirstY < imgMid ? 10 : 0);    // near first-hit Y
                    _stS4tapY = _stImgLastY  - (_stImgLastY  > imgMid ? 10 : 0);    // near last-hit Y
                    // use same x that worked during sweep (cx), already tested above
                    _scene.LastChildIndex = -1; _scene.LastImageIndex = -1;
                    _stPhase = 10;
                    Console.WriteLine($"[S3] tapping first-half at Y={_stS3tapY:0}");
                    break;

                // ── S3: tap first half ───────────────────────────────────────────────────
                case 10:
                    if (QuickTap(cx, _stS3tapY)) _stPhase = 11;
                    break;
                case 11:
                    _stS3img = _scene.LastImageIndex;
                    Console.WriteLine($"[S3] image={_stS3img} child={_scene.LastChildIndex} y={_stS3tapY:0} -> {(_stS3img >= 0 ? "PASS" : "FAIL")}");
                    _scene.LastChildIndex = -1; _scene.LastImageIndex = -1;
                    _stPhase = 12;
                    Console.WriteLine($"[S4] tapping second-half at Y={_stS4tapY:0}");
                    break;

                // ── S4: tap second half ──────────────────────────────────────────────────
                case 12:
                    if (QuickTap(cx, _stS4tapY)) _stPhase = 13;
                    break;
                case 13:
                    _stS4img = _scene.LastImageIndex;
                    Console.WriteLine($"[S4] image={_stS4img} child={_scene.LastChildIndex} y={_stS4tapY:0} -> {(_stS4img >= 0 ? "PASS" : "FAIL")}");

                    bool allPass = _stS1child >= 0 && _stS2child >= 0 && _stS3img >= 0 && _stS4img >= 0;
                    Console.WriteLine($"\n=== RESULTS ===");
                    Console.WriteLine($"S1 cell tap (initial pos) y={_stS1tapY:0}:          {(_stS1child >= 0 ? "PASS" : "FAIL")}  child={_stS1child}");
                    Console.WriteLine($"S2 same cell after half-scroll y={_stS2tapY:0}:     {(_stS2child >= 0 ? "PASS" : "FAIL")}  child={_stS2child}");
                    Console.WriteLine($"S3 image first-half y={_stS3tapY:0}:               {(_stS3img >= 0 ? "PASS" : "FAIL")}  image={_stS3img}");
                    Console.WriteLine($"S4 image second-half y={_stS4tapY:0}:              {(_stS4img >= 0 ? "PASS" : "FAIL")}  image={_stS4img}");
                    Console.WriteLine($"OVERALL: {(allPass ? "ALL PASS ✓" : "FAIL ✗")}");

                    _stPhase = 99;
                    Close();
                    break;
            }
        }

        // DETERMINISTIC day-chip overlap probe. Autopan oscillates in place and never travels into
        // history, so it never recycles across day boundaries. Instead: (1) ScrollToIndex to the oldest
        // resident cell repeatedly so LoadOlder grows the window deep into history; (2) walk the resident
        // range via ScrollToIndex, and at each stop dump RenderTree geometry around FIRST-DAY cells
        // (msg.Index % 10 == 0, day chip on -> taller cell). Flags any adjacent-cell OVERLAP (gap < -2),
        // which is the user's bug: a day-chip cell grew on recycle but the cell below kept its old top.
        private int _wPhase, _wSettle, _wStep;
        private int _wLoads;

        private void WalkAndDump()
        {
            float vpH = ClientSize.Y;

            switch (_wPhase)
            {
                case 0: // settle after bounce
                    if (++_wSettle >= 15) { _wSettle = 0; _wPhase = 1; Console.WriteLine("[WALK] go INTO history first (away from item 0)..."); }
                    break;

                case 1: // away from item 0 (into history) to build a deep window so there's room to scroll back down
                    if (QuickFlick(vpH * 0.15f, vpH * 0.80f))
                    {
                        _wLoads++;
                        LogWalk("INTO-HIST");
                        DumpDayChipAdjacency();
                        if (_wLoads >= 40) { _wPhase = 2; Console.WriteLine("[WALK] now scroll DOWN toward item 0 (user's path) - watch OVERLAP..."); }
                    }
                    break;

                case 2: // DOWN toward item 0 (newest) — the user's gesture; local index decreases; watch for OVERLAP
                    if (QuickFlick(vpH * 0.80f, vpH * 0.15f))
                    {
                        _wUp++;
                        LogWalk("DOWN->0");
                        DumpDayChipAdjacency();
                        if (_wUp >= 40) { Console.WriteLine($"[WALK] done. overlaps={_wOverlaps}"); Close(); }
                    }
                    break;
            }
        }

        private int _wOverlaps;
        private int _wUp;
        private string _wLastSig = "";

        private void LogWalk(string tag)
        {
            int first = _scene.ChatStack.FirstVisibleIndex;
            int last = _scene.ChatStack.LastVisibleIndex;
            float contentH = _scene.MainScroll.ContentSize.Pixels.Height;
            float offY = _scene.MainScroll.ViewportOffsetY;
            Console.WriteLine($"[WALK] {tag} win=[{_scene.WindowStart}..{_scene.WindowEnd}] resident={_scene.ResidentCount} older={_scene.LoadOlderCalls} newer={_scene.LoadNewerCalls} vis=[{first}..{last}] contentH={contentH:0} offY={offY:0}");
        }

        private void DumpDayChipAdjacency()
        {
            var tree = _scene.ChatStack?.RenderTree;
            if (tree == null || tree.Count < 2) return;

            var items = new List<(int idx, int msg, bool day, float top, float bot)>();
            foreach (var t in tree)
                if (t.FreezeBindingContext is DrawnUiRepro.ChatMessage m)
                    items.Add((t.FreezeIndex, m.Index, m.IsFirstDay, t.HitRect.Top, t.HitRect.Bottom));
            if (items.Count < 2) return;
            items.Sort((a, b) => a.idx.CompareTo(b.idx));

            for (int i = 1; i < items.Count; i++)
            {
                var a = items[i - 1];
                var b = items[i];
                if (b.idx - a.idx != 1) continue; // gaps handled by the integrity checker
                float gap = b.top - a.bot; // ~Spacing(4) when correct
                bool overlap = gap < -2f;
                if (overlap || a.day)
                {
                    string sig = $"{a.idx}:{a.msg}:{overlap}";
                    if (sig == _wLastSig) continue;
                    _wLastSig = sig;
                    if (overlap) _wOverlaps++;
                    Console.WriteLine($"  idx{a.idx}(msg{a.msg}{(a.day ? " DAY h=" + (a.bot - a.top).ToString("0") : "")}) bot={a.bot:0} -> idx{b.idx}(msg{b.msg}) top={b.top:0} gap={gap:0}{(overlap ? "  <<< OVERLAP" : "")}");
                }
            }
        }

        // Reproduces the "bottom 15px miss on Image+DayChip cell" bug.
        // Message 830: 830%11==5 (Image) AND 830%10==0 (IsFirstDay).
        // Repro: natural small flicks into history (cells recycle along the way), watch for
        // 830 appearing on screen each fling, scan immediately when it first shows up.
        private int _dcPhase, _dcSettle, _dcFlick;
        private float _dcSweepY;
        private float _dcCellTop = -1, _dcCellBot = -1;
        private int _dc830Local = -1;

        private void DayChipBugTest()
        {
            const int TargetMsg = 830;
            float cx = ClientSize.X / 2f;
            float vpH = ClientSize.Y;

            switch (_dcPhase)
            {
                // ── settle after bounce ──────────────────────────────────────────────────
                case 0:
                    if (++_dcSettle >= 20) { _dcSettle = 0; _dcFlick = 0; _dcPhase = 1; Console.WriteLine("[DayChip] natural scroll into history..."); }
                    break;

                // ── natural small flicks DOWN (top→bottom = into history in inverted scroll)
                // After each fling + settle: check if 830 is in the loaded window.
                // When it is, snap it to top (ScrollToIndex, no animation) so the cell is
                // at a known stable position — recycling already happened during the flings.
                case 1:
                    if (QuickFlick(vpH * 0.15f, vpH * 0.75f)) // ~490px downward drag
                    {
                        ++_dcFlick;
                        _dc830Local = _scene.WindowEnd - 1 - TargetMsg;
                        int first = _scene.ChatStack.FirstVisibleIndex;
                        int last  = _scene.ChatStack.LastVisibleIndex;
                        Console.WriteLine($"[DayChip] fling{_dcFlick} win=[{_scene.WindowStart}..{_scene.WindowEnd}] local830={_dc830Local} vis=[{first}..{last}]");

                        // Require the cell to be visible (not just in the resident window) so
                        // ScrollToIndex finds a measured cell and the sweep lands on it.
                        bool inVisible = _dc830Local >= first && _dc830Local <= last;
                        if (inVisible)
                        {
                            // Cell recycling already happened. Snap 830 to top so sweep is stable.
                            _scene.MainScroll.ScrollToIndex(_dc830Local, false, DrawnUi.Draw.RelativePositionType.Start, false);
                            Console.WriteLine($"[DayChip] 830 visible after natural scroll. Snap to top, scan immediately.");
                            _dcSweepY = 20; _dcCellTop = -1; _dcCellBot = -1;
                            _dcPhase = 2;
                        }
                        else if (_dcFlick >= 40) { Console.WriteLine("[DayChip] 830 never visible — ABORT"); Close(); }
                    }
                    break;

                // ── sweep Y 20..vpH to locate cell 830 on screen ─────────────────────
                case 2:
                    _scene.LastChildIndex = -1; _scene.LastImageIndex = -1;
                    if (QuickTap(cx, _dcSweepY))
                    {
                        if (_scene.LastChildIndex == _dc830Local)
                        {
                            if (_dcCellTop < 0) _dcCellTop = _dcSweepY;
                            _dcCellBot = _dcSweepY;
                        }
                        _dcSweepY += 20;
                        if (_dcSweepY > vpH - 10)
                        {
                            Console.WriteLine($"[DayChip] cell 830 at Y=[{_dcCellTop:0}..{_dcCellBot:0}]");
                            if (_dcCellTop < 0) { Console.WriteLine("[DayChip] 830 not on screen in sweep — ABORT"); Close(); break; }
                            _dcPhase = 3; _dcSweepY = _dcCellTop;
                        }
                    }
                    break;

                // ── scan cellTop → cellBot+25 in 2px steps ───────────────────────────
                case 3:
                    _scene.LastChildIndex = -1; _scene.LastImageIndex = -1;
                    if (QuickTap(cx, _dcSweepY))
                    {
                        string kind = _scene.LastImageIndex >= 0 ? "IMAGE" : (_scene.LastChildIndex >= 0 ? "cell-only" : "miss");
                        Console.WriteLine($"[DayChip830] y={_dcSweepY,4:0} child={_scene.LastChildIndex,4} image={_scene.LastImageIndex,4} {kind}");
                        _dcSweepY += 2;
                        if (_dcSweepY > _dcCellBot + 25) { Console.WriteLine("[DayChip] scan complete."); Close(); }
                    }
                    break;
            }
        }

        private void DriveAutoPan()
        {
            float cx = ClientSize.X / 2f;
            float top = 160, bottom = 600;
            // inverted + ReverseGestures: drag DOWN (top->bottom) scrolls into history (LoadOlder);
            // drag UP (bottom->top) returns toward newest (LoadNewer + top bounce).
            float fromY = _reversed ? bottom : top;
            float toY = _reversed ? top : bottom;

            int local = _f % FlickPeriod;

            if (local == 0)
            {
                _canvas.HandleDesktopPointerDown(cx, fromY, ClientSize.X, ClientSize.Y);
            }
            else if (local >= 1 && local <= 8)
            {
                float t = local / 8f;
                float y = fromY + (toY - fromY) * t;
                _canvas.HandleDesktopPointerMove(cx, y, true, ClientSize.X, ClientSize.Y);
            }
            else if (local == 9)
            {
                _canvas.HandleDesktopPointerUp(cx, toY, ClientSize.X, ClientSize.Y);
            }
            else if (local == FlickPeriod - 1)
            {
                _flicks++;
                LogState();
                if (!_reversed && _flicks >= FlicksPhase1)
                {
                    _reversed = true;
                    Console.WriteLine(">>> reversing: scrolling back toward newest (LoadNewer + top bounce)");
                }
            }
        }

        private void LogState()
        {
            var s = _scene.MainScroll;
            var l = _scene.ChatStack;
            float offY = s.ViewportOffsetY;
            float contentH = s.ContentSize.Pixels.Height;
            float vpH = s.Viewport.Pixels.Height;
            double avgMs = _frameSamples > 0 ? _sumFrameMs / _frameSamples : 0;
            Console.WriteLine(
                $"flick{_flicks,2} {(_reversed ? "UP " : "DN ")} frameMs avg={avgMs,5:0.0} MAX={_maxFrameMs,6:0.0} " +
                $"older={_scene.LoadOlderCalls} newer={_scene.LoadNewerCalls} trim={_scene.TrimEvents}");
            _maxFrameMs = 0; _sumFrameMs = 0; _frameSamples = 0;
        }

        private void Capture(string name)
        {
            try
            {
                int w = ClientSize.X, h = ClientSize.Y;
                var pixels = new byte[w * h * 4];
                GL.ReadBuffer(ReadBufferMode.Front);
                GL.ReadPixels(0, 0, w, h, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

                // GL origin is bottom-left -> flip vertically into an SKBitmap
                using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
                var dst = bmp.GetPixels();
                int stride = w * 4;
                unsafe
                {
                    byte* d = (byte*)dst.ToPointer();
                    for (int y = 0; y < h; y++)
                    {
                        int srcRow = (h - 1 - y) * stride;
                        for (int x = 0; x < stride; x++)
                            d[y * stride + x] = pixels[srcRow + x];
                    }
                }

                var path = Path.Combine(_outDir, name);
                using var img = SKImage.FromBitmap(bmp);
                using var data = img.Encode(SKEncodedImageFormat.Png, 90);
                using var fs = File.OpenWrite(path);
                data.SaveTo(fs);
                Console.WriteLine($"  captured {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  capture failed: {ex.Message}");
            }
        }
    }
}
