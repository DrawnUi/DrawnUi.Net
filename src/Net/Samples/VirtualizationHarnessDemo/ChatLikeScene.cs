using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Testing;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace VirtualizationHarnessDemo;

/// <summary>
/// Headless reconstruction of ChatPage's DEFINING conditions (the ones the static VirtualizationScene
/// does NOT cover and where the Managed-planes approach broke): INVERTED scroll (Rotation=180 +
/// ReverseGestures), a WINDOWED ItemsSource (1000 virtual rows, max 150 resident, newest-first),
/// bidirectional LoadMore (LoadOlder/LoadNewer), VARIABLE-height cells, on the NORMAL
/// Virtualisation.Enabled path (NOT Managed). Used to validate any draw-time plane cache against the
/// real chat mechanics: bounce, no empty space, correct content, LoadMore continuity.
/// </summary>
public sealed class ChatLikeScene : IDisposable
{
    public sealed class ChatRow
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Lines { get; set; } = 1;
        public bool Outgoing { get; set; }
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

        protected override void SetContent(object ctx)
        {
            if (ctx is ChatRow row)
            {
                _label.Text = row.Text;
                _bubble.HorizontalOptions = row.Outgoing ? LayoutOptions.End : LayoutOptions.Start;
                _bubble.BackgroundColor = row.Outgoing ? Color.FromArgb("#2B5278") : Color.FromArgb("#2A2A2A");
            }
        }
    }

    // Mirrors the app's CellsStack: suppresses auto-LoadMore during a programmatic jump and while an
    // ordered ScrollToIndex is settling, so a LoadOlder/LoadNewer can't shift _items mid-jump.
    private sealed class HarnessStack : SkiaLayout
    {
        public bool SuppressLoadMore;
        public Action OnAdded;

        public override bool ShouldTriggerLoadMore(ScaledRect viewport, LoadMoreDirection direction)
        {
            if (SuppressLoadMore)
                return false;
            if (Parent is SkiaScroll scroll && (scroll.OrderedScrollToIndexIsSet || scroll.OrderedScrollTo.IsValid))
                return false;
            return base.ShouldTriggerLoadMore(viewport, direction);
        }

        protected override void ApplyBackgroundMeasurementChange(StructureChange change)
        {
            base.ApplyBackgroundMeasurementChange(change);
            OnAdded?.Invoke();
        }

        protected override void OnHeadInsertCommitted()
        {
            base.OnHeadInsertCommitted();
            OnAdded?.Invoke();
        }

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

    public string DumpRows(int max = 60) => _stack.DumpRows(max);

    private readonly List<ChatRow> _all = new();
    private readonly ObservableRangeCollection<ChatRow> _items = new();
    private int _windowStart;
    private int _windowEnd;
    private const int LoadBatch = 50;
    private const int MaxResident = 150;
    private readonly HarnessStack _stack;

    public int LoadOlderCalls;
    public int LoadNewerCalls;

    public HeadlessCanvasHost Host { get; }
    public SkiaScroll Scroll { get; }
    public SkiaLayout List { get; }
    public int ResidentCount => _items.Count;
    public int WindowStart => _windowStart;
    public int WindowEnd => _windowEnd;

    public ChatLikeScene(int total = 1000, int width = 430, int height = 720, int measurementCacheCapacity = 1000)
    {
        for (int i = 0; i < total; i++)
        {
            int lines = 1 + (i * 7 % 5);   // 1..5 lines -> variable heights
            _all.Add(new ChatRow
            {
                Index = i,
                Lines = lines,
                Outgoing = (i % 3) == 0,
                Text = $"Message {i}" + string.Concat(Enumerable.Repeat("\nlorem ipsum dolor", lines - 1)),
            });
        }

        _windowEnd = _all.Count;
        _windowStart = Math.Max(0, _windowEnd - LoadBatch);
        _items.AddRange(ReversedRange(_windowStart, _windowEnd - _windowStart));

        Host = new HeadlessCanvasHost(width, height, scale: 1f, background: Colors.Black);

        List = _stack = new HarnessStack
        {
            Type = LayoutType.Column,
            Spacing = 4,
            Padding = new Thickness(0, 8),
            ItemsSource = _items,
            ItemTemplateType = typeof(ChatRowCell),
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
            VirtualisationInflated = 100,
            MeasurementCacheCapacity = measurementCacheCapacity,
            UseCache = SkiaCacheType.None,   // mirror CellsStack (it owns its own draw/cache)
            FastMeasurement = true,
            HorizontalOptions = LayoutOptions.Fill,
            // Virtualisation left at default (Enabled) — the working chat path, NOT Managed.
        };

        Scroll = new SkiaScroll
        {
            Orientation = ScrollOrientation.Vertical,
            ResetScrollPositionOnContentSizeChanged = false,
            Rotation = 180,
            ReverseGestures = true,
            TrackIndexPosition = RelativePositionType.Start,
            LoadMoreCommand = new Command(LoadOlder),
            LoadMoreOffset = 800,
            LoadMoreTopCommand = new Command(LoadNewer),
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
    }

    private List<ChatRow> ReversedRange(int from, int count)
    {
        var batch = new List<ChatRow>(count);
        for (int i = from + count - 1; i >= from; i--)
            batch.Add(_all[i]);
        return batch;
    }

    // bottom trigger = visually scrolling UP = load history (append older at list end)
    private void LoadOlder()
    {
        LoadOlderCalls++;
        if (_windowStart <= 0) return;
        int n = Math.Min(LoadBatch, _windowStart);
        int over = _items.Count + n - MaxResident;
        if (over > 0) { _items.RemoveRange(0, over); _windowEnd -= over; }
        _windowStart -= n;
        _items.AddRange(ReversedRange(_windowStart, n));
    }

    // top trigger = visually scrolling DOWN = reload trimmed newer part (head-insert)
    private void LoadNewer()
    {
        LoadNewerCalls++;
        if (_windowEnd >= _all.Count) return;
        int n = Math.Min(LoadBatch, _all.Count - _windowEnd);
        int over = _items.Count + n - MaxResident;
        if (over > 0) { _items.RemoveRange(_items.Count - over, over); _windowStart += over; }
        _items.InsertRange(0, ReversedRange(_windowEnd, n));
        _windowEnd += n;
    }

    // ---- jump buttons (mirror ChatPage.ScrollToOldest / ScrollToNewest) ----

    public bool AtPresent => _windowEnd == _all.Count;
    public bool AtOldest => _windowStart == 0;

    /// <summary>Mirror of ScrollToOldest: suppress LoadMore, rebase the window to history start via
    /// ReplaceRange (structure-preserving), then ordered-scroll to the visual top (oldest = last resident).</summary>
    public void JumpToOldest()
    {
        _stack.SuppressLoadMore = true;
        _windowStart = 0;
        _windowEnd = Math.Min(LoadBatch, _all.Count);
        _items.ReplaceRange(ReversedRange(_windowStart, _windowEnd - _windowStart));
        Scroll.ScrollToIndex(_items.Count - 1, false, RelativePositionType.Start, true);
    }

    /// <summary>Mirror of ScrollToNewest: if detached, rebase to the present via ReplaceRange + instant snap
    /// to content start (offset 0 = newest); else ordered-scroll to index 0.</summary>
    public void JumpToNewest()
    {
        _stack.SuppressLoadMore = true;
        if (!AtPresent)
        {
            _windowEnd = _all.Count;
            _windowStart = Math.Max(0, _windowEnd - LoadBatch);
            _items.ReplaceRange(ReversedRange(_windowStart, _windowEnd - _windowStart));
            Scroll.ScrollTo(0, 0, 0, false);
        }
        else
        {
            Scroll.ScrollToIndex(0, false, RelativePositionType.Start, true);
        }
    }

    /// <summary>Release the jump's LoadMore block (the app does this in OnChatScrolled once the target lands).</summary>
    public void ReleaseSuppress() => _stack.SuppressLoadMore = false;

    public void Warmup(int frames = 10, int sleepMs = 15)
    {
        for (int i = 0; i < frames; i++) { Host.RenderFrame(16); if (sleepMs > 0) Thread.Sleep(sleepMs); }
    }

    public float OffsetY => Scroll.ViewportOffsetY;
    public float ContentHeight => Scroll.ContentSize.Pixels.Height;
    public int VisibleCount => Scroll.Content is SkiaLayout l ? l.ChildrenFactory?.GetChildrenCount() ?? 0 : 0;

    public void Dispose() => Host.Dispose();
}
