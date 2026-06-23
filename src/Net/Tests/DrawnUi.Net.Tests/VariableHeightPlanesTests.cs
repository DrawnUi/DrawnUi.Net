using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;
using Color = DrawnUi.Color;

namespace DrawnUi.Net.Tests;

// Repro for variable-height cells under Managed (planes) virtualization: build the per-plane window
// structure for consecutive content bands and assert cell positions are continuous (no gaps/overlaps)
// and that heights actually vary.
public class VariableHeightPlanesTests
{
    private readonly ITestOutputHelper _out;
    public VariableHeightPlanesTests(ITestOutputHelper output) => _out = output;

    public sealed class VarRow : BindableObject
    {
        public string Text { get; set; } = string.Empty;
    }

    // Mirrors DrawnUiReproIS RowCell: SkiaRichLabel (markdown + link) inside a SkiaShape inside a
    // cached SkiaDynamicDrawnCell. The per-cell cache is the part plain-SkiaLabel repros missed.
    public static int LastTappedIndex = -1;
    public static int LastCommandIndex = -1;

    private sealed class RecordCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object> _run;
        public RecordCommand(Action<object> run) => _run = run;
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _run(parameter);
    }

    public sealed class VarRowCell : SkiaDynamicDrawnCell
    {
        private readonly SkiaRichLabel _label;
        private readonly SkiaShape _shape;

        public VarRowCell()
        {
            HorizontalOptions = LayoutOptions.Fill;
            this.OnTapped(me =>
            {
                if (me is SkiaControl c)
                    LastTappedIndex = c.ContextIndex;
            });
            Children = new List<SkiaControl>
            {
                new SkiaShape
                {
                    Type = ShapeType.Rectangle,
                    CornerRadius = 8,
                    Padding = new Thickness(18, 10),
                    Margin = new Thickness(8, 0),
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaRichLabel { FontSize = 15, TextColor = Colors.White }.Assign(out _label)
                    }
                }.Assign(out _shape)
            };
        }

        protected override void SetContent(object ctx)
        {
            if (ctx is VarRow row)
            {
                _label.Text = row.Text;
                _shape.BackgroundColor = Color.FromArgb("#2A2A2A");
            }
        }
    }

    private static readonly string[] Words =
    {
        "lorem","ipsum","dolor","sit","amet","consectetur","adipiscing","elit","sed","do","eiusmod",
        "tempor","incididunt","labore","magna","aliqua","enim","minim","veniam","quis","nostrud",
    };

    private static string MakeText(int index)
    {
        // Deterministic per index, 1-3 lines (mirrors SimpleModel.BuildExtra).
        var rnd = new Random(index);
        var sb = new System.Text.StringBuilder($"Message {index} — tap this link: <https://drawnui.net> [0]");
        int extraLines = rnd.Next(0, 3);
        for (int l = 0; l < extraLines; l++)
        {
            sb.Append('\n');
            int words = rnd.Next(4, 11);
            for (int w = 0; w < words; w++)
            {
                if (w > 0) sb.Append(' ');
                sb.Append(Words[rnd.Next(Words.Length)]);
            }
        }
        return sb.ToString();
    }

    [Fact]
    public void Tap_Hits_Correct_Variable_Height_Row()
    {
        int width = 430, height = 640;
        using var host = new HeadlessCanvasHost(width, height, scale: 1f, background: Colors.Black);

        var items = new ObservableCollection<VarRow>();
        for (int i = 0; i < 300; i++)
            items.Add(new VarRow { Text = MakeText(i) });

        var list = new SkiaLayout
        {
            Type = LayoutType.Column,
            Spacing = 2,
            ItemsSource = items,
            ItemTemplateType = typeof(VarRowCell),
            Virtualisation = VirtualisationType.Managed,
            ItemTemplatePoolSize = 0,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
            HorizontalOptions = LayoutOptions.Fill,
            CommandChildTapped = new RecordCommand(o => LastCommandIndex = (o as SkiaControl)?.ContextIndex ?? -1),
        };
        var scroll = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            Virtualisation = VirtualisationType.Managed,
            ResetScrollPositionOnContentSizeChanged = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = list,
        };
        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { scroll }
        };

        var robot = new GestureRobot(host);
        for (int i = 0; i < 20; i++) { host.RenderFrame(16); System.Threading.Thread.Sleep(3); }
        for (int round = 0; round < 4; round++)
        {
            robot.Pan(215, 520, 215, 220, durationMs: 90, steps: 8);
            for (int i = 0; i < 6; i++) { host.RenderFrame(16); System.Threading.Thread.Sleep(3); }
        }
        for (int i = 0; i < 40; i++) { host.RenderFrame(16); System.Threading.Thread.Sleep(4); }

        float scale = host.Scale;
        float scrollTopPx = -(float)scroll.ViewportOffsetY * scale;
        float drawTop = scroll.DrawingRect.Top;
        float avg = list.GetAverageItemHeightPixels(scale);

        // For every fully-visible LIVE row (the rows the user actually taps on screen), tap its UPPER and its
        // LOWER portion; both must hit THAT row. The old uniform-slot mapping sent the lower part of a tall row
        // to the next row — this asserts it no longer does.
        int tested = 0, tallTested = 0;
        foreach (var c in new List<ControlInStack>(list.GetStackStructure().GetChildren()))
        {
            if (c == null) continue;
            float screenTop = drawTop + c.Destination.Top - scrollTopPx;
            float screenBot = drawTop + c.Destination.Bottom - scrollTopPx;
            if (screenTop < 20 || screenBot > height - 20) continue; // fully visible only
            float h = c.Destination.Height;
            if (h < 8) continue;
            if (list.ChildrenFactory.GetCellInUseOrNull(c.ControlIndex) == null) continue; // only live rows

            foreach (var frac in new[] { 0.2f, 0.8f })
            {
                LastTappedIndex = -1;
                LastCommandIndex = -1;
                float tapYpx = screenTop + h * frac;
                robot.Tap(215, tapYpx / scale);
                host.RenderFrame(16);
                _out.WriteLine($"row {c.ControlIndex} h={h:0} frac={frac} tapY={tapYpx:0} -> resolved {LastCommandIndex}");
                // The fix: tap resolves to the correct VARIABLE-height row (upper AND lower half), not a
                // uniform-slot guess. Verified via the resolved-cell command path.
                Assert.Equal(c.ControlIndex, LastCommandIndex);
            }
            tested++;
            if (h > avg * 1.3f) tallTested++;
        }

        _out.WriteLine($"tested {tested} live rows, {tallTested} tall");
        Assert.True(tested >= 3, $"expected to test several live rows, tested {tested}");
        Assert.True(tallTested >= 1, "expected at least one tall row (lower-half tap is the regression)");
    }

    [Fact]
    public void PlaneWindow_Has_Continuous_Variable_Heights_Across_Bands()
    {
        int width = 430, height = 640;
        using var host = new HeadlessCanvasHost(width, height, scale: 1f, background: Colors.Black);

        var items = new ObservableCollection<VarRow>();
        for (int i = 0; i < 300; i++)
            items.Add(new VarRow { Text = MakeText(i) });

        var list = new SkiaLayout
        {
            Type = LayoutType.Column,
            Spacing = 2,
            Padding = new Thickness(0, 8),
            ItemsSource = items,
            ItemTemplateType = typeof(VarRowCell),
            Virtualisation = VirtualisationType.Managed,
            ItemTemplatePoolSize = 64,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
            HorizontalOptions = LayoutOptions.Fill,
        };

        var scroll = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            Virtualisation = VirtualisationType.Managed,
            ResetScrollPositionOnContentSizeChanged = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = list,
        };

        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { scroll }
        };

        var robot = new GestureRobot(host);

        // Warm up, then drive the list DOWN by gesture so the background measurement frontier advances
        // past several planes (this is what real scrolling does).
        for (int i = 0; i < 20; i++) { host.RenderFrame(16); System.Threading.Thread.Sleep(3); }
        for (int round = 0; round < 40 && list.LastMeasuredIndex < items.Count - 1; round++)
        {
            robot.Pan(215, 520, 215, 160, durationMs: 90, steps: 8);
            for (int i = 0; i < 8; i++) { host.RenderFrame(16); System.Threading.Thread.Sleep(3); }
        }

        float scale = host.Scale;
        float spacing = (float)(list.Spacing * scale);
        int measured = list.LastMeasuredIndex;
        _out.WriteLine($"LastMeasuredIndex={measured} scale={scale} spacing={spacing}");

        // Build two consecutive plane bands (planeHeight = 2 viewports) and merge their cells by index.
        float planeH = height * 2f;
        var byIndex = new SortedDictionary<int, SKRect>();

        void Collect(double top, double bottom)
        {
            var st = list.BuildPlaneWindowStructure(top, bottom, scale, width);
            foreach (var c in st.GetChildrenAsSpans())
            {
                if (c == null) continue;
                byIndex[c.ControlIndex] = c.Destination;
            }
        }

        for (int b = 0; b < 8; b++)
            Collect(planeH * b, planeH * (b + 1));

        _out.WriteLine($"collected indices {byIndex.Keys.Min()}..{byIndex.Keys.Max()} count={byIndex.Count}");

        var heights = new HashSet<int>();
        int gaps = 0;
        int prevIndex = -1;
        SKRect prev = default;
        foreach (var kv in byIndex)
        {
            int idx = kv.Key;
            var r = kv.Value;
            heights.Add((int)Math.Round(r.Height));

            if (prevIndex >= 0 && prevIndex == idx - 1 && idx <= measured)
            {
                float expectedTop = prev.Bottom + spacing;
                float delta = r.Top - expectedTop;
                if (Math.Abs(delta) > 1.5f)
                {
                    gaps++;
                    if (gaps <= 20)
                        _out.WriteLine($"  GAP idx {prevIndex}->{idx}: prevBottom={prev.Bottom:0.0} expTop={expectedTop:0.0} top={r.Top:0.0} delta={delta:0.0} h={r.Height:0.0}");
                }
            }

            prevIndex = idx;
            prev = r;
        }

        _out.WriteLine($"distinct heights={string.Join(",", heights.OrderBy(x => x))} gaps={gaps}");

        Assert.True(heights.Count >= 2, "expected variable heights but cells came out uniform");
        Assert.True(gaps == 0, $"found {gaps} position gaps/overlaps across plane bands");
    }
}
