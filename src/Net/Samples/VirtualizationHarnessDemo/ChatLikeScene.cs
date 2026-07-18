using System.Windows.Input;
using DrawnUi;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Headless reconstruction of ChatPage's DEFINING conditions: INVERTED scroll (Rotation=180 +
/// ReverseGestures), a WINDOWED ItemsSource (1000 virtual rows, max 150 resident, newest-first),
/// bidirectional LoadMore, VARIABLE-height cells, on the NORMAL Virtualisation.Enabled path.
///
/// Now drives the LIBRARY pieces directly: <see cref="WindowedSource{T}"/> over a plain
/// <see cref="SkiaLayout"/> wired by the built-in <see cref="SkiaScrollWindowHost"/> — no behavior
/// subclass (base provides SuppressLoadMore + ordered-scroll LoadMore gating + MeasurementApplied).
/// </summary>
public sealed class ChatLikeScene : IDisposable
{
    public sealed class ChatRow
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Lines { get; set; } = 1;
        public bool Outgoing { get; set; }
        public bool IsImage { get; set; }
    }

    public sealed class ChatRowCell : SkiaDynamicDrawnCell
    {
        private readonly SkiaLabel _label;
        private readonly SkiaShape _bubble;

        public ChatRowCell()
        {
            HorizontalOptions = LayoutOptions.Fill;
            Rotation = 180;                 // scroll is inverted; rotate the cell back upright
            IsParentIndependent = true;
            UseCache = SkiaCacheType.Image;  // MeasureVisible rule

            _label = new SkiaLabel
            {
                TextColor = Colors.White,
                FontSize = 15,
                Margin = new Thickness(12, 8),
                HorizontalOptions = LayoutOptions.Fill,
            };

            Children = new List<SkiaControl>
            {
                (_bubble = new SkiaShape
                {
                    UseCache = SkiaCacheType.Image,
                    Type = ShapeType.Rectangle,
                    CornerRadius = 12,
                    Margin = new Thickness(8, 3),
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = { _label }
                })
            };
        }

        // When true, "image" rows arrive EMPTY and upgrade their content+height asynchronously after bind
        // (models the real cell's async GPU-image source upgrade posted to the main thread).
        public static bool AsyncImageUpgrade = false;
        private long _bindToken;

        protected override void SetContent(object ctx)
        {
            if (ctx is ChatRow row)
            {
                long token = ++_bindToken; // invalidate any in-flight upgrade for a previous binding
                _bubble.HorizontalOptions = row.Outgoing ? LayoutOptions.End : LayoutOptions.Start;
                _bubble.BackgroundColor = row.Outgoing ? Color.FromArgb("#2B5278") : Color.FromArgb("#2A2A2A");

                if (AsyncImageUpgrade && row.IsImage)
                {
                    // arrive blank/short, then upgrade LATE — after the cached band may have already recorded
                    _label.Text = string.Empty;
                    Task.Run(async () =>
                    {
                        await Task.Delay(70);
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (_bindToken == token) _label.Text = row.Text;
                        });
                    });
                }
                else
                {
                    _label.Text = row.Text;
                }
            }
        }
    }

    // Test-only structure peek. NO behavior overrides — the base SkiaLayout already provides the
    // windowing primitives (SuppressLoadMore, ordered-scroll LoadMore gating, MeasurementApplied).
    private sealed class PeekStack : SkiaLayout
    {
        public string DumpRows(int max = 60)
        {
            var s = StackStructure;
            if (s == null) return "structure=null";
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var c in s.GetChildren())
            {
                if (n++ >= max) break;
                sb.Append($"[{c.ControlIndex}]t{c.Destination.Top:0}/b{c.Destination.Bottom:0}/h{c.Measured.Pixels.Height:0} ");
            }
            return sb.ToString();
        }
    }

    // Simulated remote API: owns the full list (as a server would), serves ascending ranges with latency.
    private sealed class MockSource : IWindowDataSource<ChatRow>
    {
        private readonly List<ChatRow> _all;
        private readonly int _latencyMs;
        public MockSource(List<ChatRow> all, int latencyMs) { _all = all; _latencyMs = latencyMs; }

        public async Task<int> GetCountAsync(CancellationToken cancel = default)
        {
            if (_latencyMs > 0) await Task.Delay(_latencyMs, cancel);
            return _all.Count;
        }

        public async Task<IReadOnlyList<ChatRow>> GetRangeAsync(int from, int count, CancellationToken cancel = default)
        {
            if (_latencyMs > 0) await Task.Delay(_latencyMs, cancel);
            return _all.GetRange(from, count); // ascending global order
        }
    }

    private const int LoadBatch = 50;
    private const int MaxResident = 150;
    private readonly WindowedSource<ChatRow> _window = new(LoadBatch, MaxResident, limitMemory: true);

    public HeadlessCanvasHost Host { get; }
    public SkiaScroll Scroll { get; }
    public SkiaLayout List { get; }
    public int ResidentCount => _window.Items.Count;
    public int WindowStart => _window.WindowStart;
    public int WindowEnd => _window.WindowEnd;
    public bool AtPresent => _window.AtPresent;

    public ChatLikeScene(int total = 1000, int width = 430, int height = 720,
        int measurementCacheCapacity = 1000, int latencyMs = 10, bool cachedPlanes = false)
    {
        var all = new List<ChatRow>(total);
        for (int i = 0; i < total; i++)
        {
            int lines = 1 + (i * 7 % 5);   // 1..5 lines -> variable heights
            all.Add(new ChatRow
            {
                Index = i,
                Lines = lines,
                Outgoing = (i % 3) == 0,
                IsImage = (i % 11) == 5,   // sparse "image" rows -> async content upgrade
                Text = $"Message {i}" + string.Concat(Enumerable.Repeat("\nlorem ipsum dolor", lines - 1)),
            });
        }

        ViewportHeight = height;
        Host = new HeadlessCanvasHost(width, height, scale: 1f, background: Colors.Black);

        List = cachedPlanes ? new DrawnChatList.ChatMessagesStack() : new PeekStack();
        List.Type = LayoutType.Column;
        List.Spacing = 4;
        List.Padding = new Thickness(0, 8);
        List.ItemsSource = _window.Items;
        List.ItemTemplate = new DataTemplate(() => new ChatRowCell());
        List.RecyclingTemplate = RecyclingTemplate.Enabled;
        List.MeasureItemsStrategy = MeasuringStrategy.MeasureVisible;
        List.VirtualisationInflated = 100;
        List.MeasurementCacheCapacity = measurementCacheCapacity;
        List.UseCache = SkiaCacheType.None;
        List.FastMeasurement = true;
        List.HorizontalOptions = LayoutOptions.Fill;

        Scroll = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            ResetScrollPositionOnContentSizeChanged = false,
            Rotation = 180,
            ReverseGestures = true,
            TrackIndexPosition = RelativePositionType.Start,
            LoadMoreCommand = new Command(_window.LoadOlder),
            LoadMoreOffset = 800,
            LoadMoreTopCommand = new Command(_window.LoadNewer),
            LoadMoreTopOffset = 800,
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

        _window.SetHost(new SkiaScrollWindowHost(Scroll, List));
        _window.SetDataSource(new MockSource(all, latencyMs));
        _ = _window.InitializeAsync();
    }

    // ---- jump buttons: atomic nav on the lib WindowedSource (replace + scroll in one turn) ----

    public void JumpToOldest() => _ = _window.ScrollToOldest(false);
    public void JumpToNewest() => _ = _window.ScrollToNewest(false);

    public void ReleaseSuppress() => List.SuppressLoadMore = false;

    // ---- LoadMore spinner loading state (latency lives in the data source) ----

    public bool IsLoadingOlder => _window.IsLoadingOlder;
    public bool IsLoadingNewer => _window.IsLoadingNewer;
    public bool IsLoadingJump => _window.IsLoadingJump;

    public void TriggerLoadOlder() => _window.LoadOlder();
    public void TriggerLoadNewer() => _window.LoadNewer();
    public void JumpToIndex(int global, bool animate = false) => _ = _window.ScrollToIndex(global, RelativePositionType.Center, animate);

    public string DumpRows(int max = 60) => List is PeekStack p ? p.DumpRows(max) : "(real AppMessagesStack)";

    public int ViewportHeight { get; }

    /// <summary>Locate the cell bound to global row <paramref name="globalIndex"/> in the gesture RenderTree
    /// and return its on-screen Y. (Global fill only proves non-blank; this proves the TARGET is present.)</summary>
    public (bool found, float top, float bottom) TargetCell(int globalIndex)
    {
        var tree = List.RenderTree;
        if (tree != null)
            foreach (var t in tree)
                if (t.FreezeBindingContext is ChatRow r && r.Index == globalIndex)
                    return (true, t.HitRect.Top, t.HitRect.Bottom);
        return (false, float.NaN, float.NaN);
    }

    /// <summary>Pump frames until the async InitializeAsync has materialized the first window.</summary>
    public void WaitReady(int maxFrames = 240)
    {
        for (int i = 0; i < maxFrames && ResidentCount == 0; i++)
        {
            Host.RenderFrame(16);
            Thread.Sleep(4);
        }
    }

    public void Warmup(int frames = 10, int sleepMs = 15)
    {
        WaitReady();
        for (int i = 0; i < frames; i++) { Host.RenderFrame(16); if (sleepMs > 0) Thread.Sleep(sleepMs); }
    }

    public float OffsetY => Scroll.ViewportOffsetY;
    public float ContentHeight => Scroll.ContentSize.Pixels.Height;

    public void Dispose() => Host.Dispose();
}
