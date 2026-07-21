using System.ComponentModel;
using System.Reflection;
using DrawnUi.Draw;
using DrawnUi.Testing;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Double-buffer STALE-PLANE CONTENT regression — the bug class that forced UseDoubleBuffering=false
/// as the SkiaCachedStack default: a cell's content changes (image bake lands / streaming text grows a
/// line) while a plane bake is in flight, and the retained/published plane still holds the PREVIOUS
/// pixels — blitting it REGRESSES what a live frame already presented (device: image-load flicker,
/// "stack jumps while AI is typing").
/// A fast dev machine hides the window: the bake lands inside the render thread's _bakeDone.Wait(16)
/// and the stale plane never serves. BakeStall (sleeps ONLY during a plane bake pass) forces every bake
/// past that wait — the device-timing window, deterministically.
/// Probe: the pixel band BELOW the growing cell may only move DOWN as lines are appended above it; any
/// UPWARD shift or up/down oscillation = old-world pixels served after newer were already shown.
/// </summary>
public static class StalePlaneContentRepro
{
    class RowItem : INotifyPropertyChanged
    {
        string _text = "";
        public string Text
        {
            get => _text;
            set
            {
                if (_text == value) return;
                _text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Sleeps ONLY inside a plane bake pass (reflection read of the internal thread-static
    /// SkiaLayout.IsPlaneBakePass happens ON the bake thread) — live draws, sync records and cell
    /// prep bakes stay fast; only the async band bake outlives the 16ms render-thread wait.
    /// </summary>
    class BakeStall : SkiaControl
    {
        static readonly FieldInfo BakePassField =
            typeof(SkiaLayout).GetField("IsPlaneBakePass", BindingFlags.NonPublic | BindingFlags.Static);

        protected override void Paint(DrawingContext ctx)
        {
            base.Paint(ctx);
            if (BakePassField?.GetValue(null) is true)
                Thread.Sleep(3);
        }
    }

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("========= STALE PLANE ON CONTENT CHANGE (UseDoubleBuffering=true, slow bake) =========");
        try { RunCore(); }
        catch (Exception ex) { Console.WriteLine($"  CRASH: {ex}"); }
        Console.WriteLine("======================================================================================");
    }

    static void RunCore()
    {
        var bg = Colors.Black;
        using var host = new HeadlessCanvasHost(440, 920, scale: 1f, background: bg);

        var items = Enumerable.Range(0, 60)
            .Select(i => new RowItem { Text = $"Row {i} — alpha beta gamma delta" })
            .ToList();

        SkiaCachedStack stack = null;
        host.Canvas.Content = new SkiaScroll
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaCachedStack
            {
                AutoDoubleBuffering = true, // this repro targets the double-buffer path (on while scrolling)
                RecyclingTemplate = RecyclingTemplate.Enabled,
                Spacing = 8,
                Padding = new Thickness(12, 8),
                ItemsSource = items,
                ItemTemplate = new DataTemplate(() =>
                {
                    SkiaLabel label = null;
                    return new SkiaLayout
                        {
                            Type = LayoutType.Column,
                            Padding = new Thickness(14, 10),
                            HorizontalOptions = LayoutOptions.Fill,
                            BackgroundColor = Color.FromArgb("#223044"),
                            // NO cell cache: a cached cell is BLITTED by the bake pass (children never
                            // painted) so BakeStall would never run — the bake must paint cells live here.
                            UseCache = SkiaCacheType.None,
                            Children = new List<SkiaControl>
                            {
                                new BakeStall { WidthRequest = 1, HeightRequest = 1 },
                                new SkiaLabel
                                {
                                    FontSize = 15,
                                    TextColor = Colors.White,
                                }.Assign(out label),
                            }
                        }
                        .ObserveBindingContext<SkiaLayout, RowItem>((me, vm, prop) => { label.Text = vm.Text; });
                }),
            }.Assign(out stack)
        };

        // settle: cells visible + first plane recorded
        for (int i = 0; i < 500 && (stack.LastVisibleIndex < 0 || stack.ForegroundPlane == null); i++)
        {
            host.RenderFrame(16);
            Thread.Sleep(4);
        }
        for (int i = 0; i < 40; i++) { host.RenderFrame(16); Thread.Sleep(4); }
        Console.WriteLine($"  start: vis=[{stack.FirstVisibleIndex}..{stack.LastVisibleIndex}] " +
                          $"plane={(stack.ForegroundPlane != null)} fill={host.NonBackgroundFraction(bg):0.00}");

        // stream lines into row 2 (visible near the top); probe the band of rows BELOW it
        var grow = items[2];
        const int bandTop = 520, bandBot = 860;
        long[] prevSig = host.RowSignature(bandTop, bandBot);
        int negShifts = 0, oscillations = 0, blanks = 0, lastShift = 0, worstNeg = 0;
        float totalDown = 0;
        int growCount = 0;

        for (int frame = 0; frame < 620; frame++)
        {
            if (frame % 40 == 0 && growCount < 12)
                grow.Text += $"\nline {++growCount} — more streamed text arriving here";

            host.RenderFrame(16);
            Thread.Sleep(6);

            double fill = host.NonBackgroundFraction(bg);
            if (fill < 0.15) blanks++;

            var sig = host.RowSignature(bandTop, bandBot);
            int shift = HeadlessCanvasHost.EstimateVerticalShift(prevSig, sig, 40);
            prevSig = sig;

            if (shift > 0) totalDown += shift;
            if (shift < -1)
            {
                negShifts++;
                if (shift < worstNeg) worstNeg = shift;
                if (negShifts <= 10)
                    Console.WriteLine($"   f{frame,3} PIXEL band moved UP {shift}px (old-world served)");
            }
            if (shift != 0 && lastShift != 0 && Math.Sign(shift) != Math.Sign(lastShift))
            {
                oscillations++;
                if (oscillations <= 10)
                    Console.WriteLine($"   f{frame,3} PIXEL oscillation {lastShift} -> {shift}");
            }
            if (shift != 0) lastShift = shift;
        }

        Console.WriteLine($"  grew {growCount} lines, band moved down total {totalDown:0}px");
        Console.WriteLine($"  negShifts={negShifts} worstNeg={worstNeg}px oscillations={oscillations} blankFrames={blanks}");
        if (totalDown < 20)
            Console.WriteLine("=> INCONCLUSIVE (growth never moved the probe band — scene broken)");
        else
            Console.WriteLine(negShifts == 0 && oscillations == 0 && blanks == 0
                ? "=> PASS (content grows forward only: no old-world pixels served)"
                : "=> FAIL (stale plane served old content after newer was presented — reproduced)");
    }
}
