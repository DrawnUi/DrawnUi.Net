using AppoMobi.Specials;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Headless checks for the tiled-planes GESTURE path on a SCROLLED tile (not tile 0):
///  - tap maps to the correct live cell (right ContextIndex),
///  - the cell's content refreshes on cell.Update() (counter),
///  - the tapped cell's inner label sits at SCREEN coords (not band coords) — the root that decides whether
///    a ripple lands on the correct row on a scrolled tile,
///  - a ripple registers on the cell and the overlay paints it (PNG saved for eyeballing).
/// Pixel/perf truth still confirmed in MAUI; this nails the coordinate + state regressions headlessly.
/// </summary>
public static class GestureProbe
{
    public sealed class Model : BindableObject
    {
        public int Index { get; set; }

        private int _counter;
        public int Counter
        {
            get => _counter;
            set { _counter = value; OnPropertyChanged(); OnPropertyChanged(nameof(Markdown)); }
        }

        // Deterministic per-Index extra lines -> UNEVEN rows (1-3 lines), stable across rebinds.
        private string _extra;
        private string Extra => _extra ??= BuildExtra(Index);

        public string Markdown =>
            (Counter > 0 ? $"Message {Index} ({Counter})" : $"Message {Index}") + Extra;

        private static readonly string[] Words =
        {
            "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed",
            "do", "eiusmod", "tempor", "incididunt", "labore", "magna", "aliqua", "enim", "minim",
        };

        private static string BuildExtra(int index)
        {
            var rnd = new Random(index);
            int extraLines = rnd.Next(0, 3);
            if (extraLines == 0) return string.Empty;
            var sb = new System.Text.StringBuilder();
            for (int l = 0; l < extraLines; l++)
            {
                sb.Append('\n');
                int words = rnd.Next(4, 9);
                for (int w = 0; w < words; w++)
                {
                    if (w > 0) sb.Append(' ');
                    sb.Append(Words[rnd.Next(Words.Length)]);
                }
            }
            return sb.ToString();
        }
    }

    // Recorded by the cell on tap so the runner (no access to private label) can assert.
    public static int LastTappedIndex = -1;
    public static string LastLabelText = "";
    public static double LastLabelScreenY = double.NaN;
    public static int LastCellPostAnimators = -1;

    public sealed class RippleRowCell : SkiaDynamicDrawnCell
    {
        private readonly SkiaLabel _label;

        public RippleRowCell()
        {
            HorizontalOptions = LayoutOptions.Fill;

            Children = new List<SkiaControl>
            {
                new SkiaShape
                {
                    UseCache = SkiaCacheType.Image,
                    Type = ShapeType.Rectangle,
                    CornerRadius = 8,
                    Padding = new Thickness(12, 8),
                    Margin = new Thickness(8, 0),
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaLabel
                        {
                            TextColor = Colors.White,
                            FontSize = 15,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Center,
                        }.Assign(out _label)
                    }
                }
            };

            this.OnTapped(me =>
            {
                if (BindingContext is Model model)
                {
                    model.Counter++;
                    this.Update();                                       // must refresh content in planes
                    _label.PlayRippleAnimation(Colors.White, 100, 12);   // ripple rides the LABEL (mirrors span ripple)

                    LastTappedIndex = model.Index;
                    LastLabelText = _label.Text;
                    LastLabelScreenY = _label.Y;
                    LastCellPostAnimators = _label.PostAnimators.Count;
                }
            });
        }

        protected override void SetContent(object ctx)
        {
            if (ctx is Model row)
                _label.Text = row.Markdown;
        }
    }

    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("=== GESTURE PROBE (scrolled-tile tap / counter / coords / ripple) ===");

        const int width = 430, height = 640;
        var items = new ObservableRangeCollection<Model>();
        for (int i = 0; i < 1000; i++)
            items.Add(new Model { Index = i });

        using var host = new HeadlessCanvasHost(width, height, scale: 1f, background: Colors.Black);

        SkiaScroll scroll = null;
        SkiaLayout list = null;

        host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaScroll
                {
                    Orientation = ScrollOrientation.Vertical,
                    Virtualisation = VirtualisationType.Managed,
                    ResetScrollPositionOnContentSizeChanged = false,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaLayout
                    {
                        Type = LayoutType.Column,
                        Spacing = 2,
                        Padding = new Thickness(0, 8),
                        ItemsSource = items,
                        ItemTemplateType = typeof(RippleRowCell),
                        Virtualisation = VirtualisationType.Managed,
                        ItemTemplatePoolSize = 0,
                        RecyclingTemplate = RecyclingTemplate.Enabled,
                        MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                        HorizontalOptions = LayoutOptions.Fill,
                    }.Assign(out list)
                }.Assign(out scroll)
            }
        };

        // warmup
        for (int i = 0; i < 10; i++) { host.RenderFrame(16); Thread.Sleep(15); }

        var robot = new GestureRobot(host);

        // Scroll deep into a non-zero tile (tile 0 ~ idx 0..36; we want ~idx 80+).
        for (int p = 0; p < 12 && scroll.ViewportOffsetY > -3000f; p++)
        {
            robot.Pan(215, 520, 215, 180, durationMs: 90, steps: 8);
            robot.SettleFling(scroll, maxFrames: 120);
            for (int f = 0; f < 4; f++) { host.RenderFrame(16); Thread.Sleep(8); }
        }

        Console.WriteLine($"scrolled to offsetY={scroll.ViewportOffsetY:0}");

        LastTappedIndex = -1;
        LastLabelText = "";
        LastLabelScreenY = float.NaN;
        LastCellPostAnimators = -1;

        // Tap a visible row (screen y=300).
        robot.Tap(215, 300);

        // Render frames so the ripple overlay paints a few times.
        for (int f = 0; f < 6; f++) { host.RenderFrame(16); Thread.Sleep(8); }

        var png = Path.Combine(AppContext.BaseDirectory, "gesture-ripple.png");
        host.SavePng(png);

        Console.WriteLine($"tappedIndex      = {LastTappedIndex}   (expect a deep, non-tile-0 index, >36)");
        Console.WriteLine($"labelText        = '{LastLabelText}'   (expect 'Message N (1)' -> Update refreshed content)");
        Console.WriteLine($"labelScreenY     = {LastLabelScreenY:0.#}   (expect 0..{height} = SCREEN coords, NOT band/large)");
        Console.WriteLine($"cellPostAnimators= {LastCellPostAnimators}   (expect >=1 = ripple registered on cell)");

        bool ok =
            LastTappedIndex > 36 &&
            LastLabelText.Contains("(1)") &&
            LastLabelScreenY >= 0 && LastLabelScreenY <= height &&
            LastCellPostAnimators >= 1;

        Console.WriteLine(ok
            ? "=> GESTURE PROBE PASS: scrolled-tile tap maps right, content refreshed, coords are SCREEN-based, ripple registered."
            : "=> GESTURE PROBE FAIL: see values above.");
        Console.WriteLine($"png: {png}");
    }
}
