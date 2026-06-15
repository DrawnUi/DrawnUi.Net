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

    private readonly List<ChatRow> _all = new();
    private readonly ObservableRangeCollection<ChatRow> _items = new();
    private int _windowStart;
    private int _windowEnd;
    private const int LoadBatch = 50;
    private const int MaxResident = 150;

    public int LoadOlderCalls;
    public int LoadNewerCalls;

    public HeadlessCanvasHost Host { get; }
    public SkiaScroll Scroll { get; }
    public SkiaLayout List { get; }
    public int ResidentCount => _items.Count;
    public int WindowStart => _windowStart;
    public int WindowEnd => _windowEnd;

    public ChatLikeScene(int total = 1000, int width = 430, int height = 720)
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

        List = new SkiaLayout
        {
            Type = LayoutType.Column,
            Spacing = 4,
            Padding = new Thickness(0, 8),
            ItemsSource = _items,
            ItemTemplateType = typeof(ChatRowCell),
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
            VirtualisationInflated = 100,
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

    public void Warmup(int frames = 10, int sleepMs = 15)
    {
        for (int i = 0; i < frames; i++) { Host.RenderFrame(16); if (sleepMs > 0) Thread.Sleep(sleepMs); }
    }

    public float OffsetY => Scroll.ViewportOffsetY;
    public float ContentHeight => Scroll.ContentSize.Pixels.Height;
    public int VisibleCount => Scroll.Content is SkiaLayout l ? l.ChildrenFactory?.GetChildrenCount() ?? 0 : 0;

    public void Dispose() => Host.Dispose();
}
