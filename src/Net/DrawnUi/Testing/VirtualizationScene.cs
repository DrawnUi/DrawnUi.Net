using System.Collections.Concurrent;
using AppoMobi.Specials;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace DrawnUi.Testing;

/// <summary>
/// Headless reconstruction of the LoadMoreRepro scene: a vertical <see cref="SkiaScroll"/> with a
/// templated, recycling <see cref="SkiaLayout"/> column of <paramref name="ItemsCount"/> rows using
/// Managed (planes) virtualization and MeasureVisible. Used to test that item measurement advances
/// in the background as the list is scrolled.
/// </summary>
public sealed class VirtualizationScene : IDisposable
{
    public sealed class Row : BindableObject
    {
        public string Text { get; set; } = string.Empty;
        public Color Color { get; set; } = Colors.White;
    }

    /// <summary>Records every cell bind as (item index, ranOnThreadPool). Cleared via <see cref="DrainBinds"/>.</summary>
    public static readonly ConcurrentQueue<(int index, bool background)> Binds = new();

    /// <summary>
    /// A list layout that records every actual cell MEASUREMENT (not bind) with the thread it ran on.
    /// <c>background == true</c> means a threadpool thread (background plane prep / incremental measure);
    /// <c>false</c> means the render/UI thread (foreground = a fluidity hazard while scrolling).
    /// </summary>
    public sealed class ProbeLayout : SkiaLayout
    {
        public readonly ConcurrentQueue<(int index, bool background)> Measures = new();

        protected override ScaledSize MeasureAndArrangeCell(SKRect destination, ControlInStack cell,
            SkiaControl child, SKRect rectForChildrenPixels, float scale)
        {
            Measures.Enqueue((cell.ControlIndex, Thread.CurrentThread.IsThreadPoolThread));
            return base.MeasureAndArrangeCell(destination, cell, child, rectForChildrenPixels, scale);
        }
    }

    /// <summary>Total <see cref="RowCell"/> instances ever created (for recycling assertions).</summary>
    public static int CellInstances;

    /// <summary>Records any cell whose bound text did not match its item index (binding desync).</summary>
    public static readonly ConcurrentQueue<(int index, string text)> BindMismatches = new();

    /// <summary>Highest item index that has actually been bound/rendered into a cell so far.</summary>
    public static int MaxBoundIndex = -1;

    public sealed class RowCell : SkiaDynamicDrawnCell
    {
        private readonly SkiaLabel _label;

        public RowCell()
        {
            Interlocked.Increment(ref CellInstances);
            HorizontalOptions = LayoutOptions.Fill;
            _label = new SkiaLabel
            {
                TextColor = Colors.White,
                FontSize = 15,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
            };
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
                    Children = { _label }
                }
            };
        }

        protected override void SetContent(object ctx)
        {
            if (ctx is Row row)
            {
                _label.Text = row.Text;
                var idx = ContextIndex;
                Binds.Enqueue((idx, Thread.CurrentThread.IsThreadPoolThread));
                if (idx > Volatile.Read(ref MaxBoundIndex))
                    Volatile.Write(ref MaxBoundIndex, idx);
                // The bound row text must match this cell's item index — catches recycling/index desync.
                if (idx >= 0 && row.Text != $"Message {idx}")
                    BindMismatches.Enqueue((idx, row.Text));
            }
        }
    }

    public HeadlessCanvasHost Host { get; }
    public SkiaScroll Scroll { get; }
    public ProbeLayout List { get; }
    public ObservableRangeCollection<Row> Items { get; } = new();

    public VirtualizationScene(int itemsCount = 1000, int width = 430, int height = 640)
    {
        while (Binds.TryDequeue(out _)) { }
        while (BindMismatches.TryDequeue(out _)) { }
        CellInstances = 0;
        MaxBoundIndex = -1;

        for (int i = 0; i < itemsCount; i++)
            Items.Add(new Row { Text = $"Message {i}" });

        Host = new HeadlessCanvasHost(width, height, scale: 1f, background: Colors.Black);

        List = new ProbeLayout
        {
            Type = LayoutType.Column,
            Spacing = 2,
            Padding = new Thickness(0, 8),
            ItemsSource = Items,
            ItemTemplateType = typeof(RowCell),
            Virtualisation = VirtualisationType.Managed,
            ItemTemplatePoolSize = 64,
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
            HorizontalOptions = LayoutOptions.Fill,
        };

        Scroll = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            Virtualisation = VirtualisationType.Managed,
            ResetScrollPositionOnContentSizeChanged = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = List,
        };

        Host.Canvas.Content = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { Scroll }
        };
    }

    /// <summary>Renders warmup frames so initial layout and the first plane settle.</summary>
    public void Warmup(int frames = 8, int sleepMs = 15)
    {
        for (int i = 0; i < frames; i++)
        {
            Host.RenderFrame(16);
            if (sleepMs > 0) Thread.Sleep(sleepMs);
        }
    }

    public (int foreground, int background) DrainBinds()
    {
        int fg = 0, bg = 0;
        while (Binds.TryDequeue(out var b))
        {
            if (b.background) bg++; else fg++;
        }
        return (fg, bg);
    }

    /// <summary>Total RowCell instances created so far (recycling should keep this far below item count).</summary>
    public int CellInstanceCount => CellInstances;

    /// <summary>Highest item index actually bound/rendered into a cell (how far rendering has reached).</summary>
    public int MaxBoundItemIndex => Volatile.Read(ref MaxBoundIndex);

    /// <summary>Returns and clears any binding desync events (bound text not matching item index).</summary>
    public List<(int index, string text)> DrainMismatches()
    {
        var list = new List<(int, string)>();
        while (BindMismatches.TryDequeue(out var m)) list.Add(m);
        return list;
    }

    /// <summary>Drains recorded cell MEASUREMENTS since last drain, split by thread.</summary>
    public (int foreground, int background) DrainMeasures()
    {
        int fg = 0, bg = 0;
        while (List.Measures.TryDequeue(out var m))
        {
            if (m.background) bg++; else fg++;
        }
        return (fg, bg);
    }

    public void Dispose() => Host.Dispose();
}
