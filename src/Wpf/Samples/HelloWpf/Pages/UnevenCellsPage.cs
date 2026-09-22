using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// Uneven rows: <c>MeasureItemsStrategy = MeasureVisible</c> — visible cells measured on demand, the
/// rest estimated and measured in idle time, with LoadMore at both ends.
/// Ported from the React demo's UnevenCellsPage.tsx.
/// </summary>
public class UnevenCellsPage : SkiaLayer
{
    private const double StatusHeight = 36;

    private static readonly string[] Words =
        ("drawn ui renders every pixel itself skia canvas recycled cells measure visible estimates the rest " +
         "and refines in idle time uneven rows news feed social timeline product catalog").Split(' ');

    private static readonly string[] Palette =
        { "#0D6EFD", "#6610F2", "#D63384", "#FD7E14", "#20C997", "#0DCAF0", "#FFC107" };

    private readonly ObservableRangeCollection<FeedItem> _items = new();

    private SkiaScroll _scroll;
    private SkiaLayout _feed;
    private SkiaLabel _status;
    private SkiaLabel _debug;
    private string _loading = string.Empty;
    private bool _busy;

    /// <summary>Builds the page.</summary>
    public UnevenCellsPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        _items.AddRange(Range(1, 200));

        Children = new List<SkiaControl>
        {
            new SkiaLabel
            {
                FontSize = 13,
                TextColor = Colors.LightGray,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 10, 0, 0),
            }.Assign(out _status),

            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Margin = new Thickness(0, StatusHeight, 0, 0),
                LoadMoreOffset = 300,
                LoadMoreTopOffset = 100,
                Content = new SkiaStack
                {
                    ItemsSource = _items,
                    ItemTemplate = new DataTemplate(() => new FeedCell()),
                    RecyclingTemplate = RecyclingTemplate.Enabled,
                    MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                    Spacing = 8,
                    Padding = new Thickness(16, 8),
                }.Assign(out _feed),
            }.Fill().Assign(out _scroll),

            new SkiaWrap
            {
                Spacing = 6,
                Margin = new Thickness(8, 0, 8, 36),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Children = new List<SkiaControl>
                {
                    JumpButton("HOME", "#0D6EFD", () => Jump(0)),
                    JumpButton("MIDDLE", "#0D6EFD", () => Jump(_items.Count / 2)),
                    JumpButton("END", "#0D6EFD", () => Jump(_items.Count - 1, RelativePositionType.End)),
                    JumpButton("STATS", "#20C997", UpdateDebug),
                },
            },

            new SkiaLabel
            {
                FontSize = 11,
                TextColor = Color.Parse("#00FF00"),
                BackgroundColor = Color.Parse("#DD000000"),
                InputTransparent = true,
                Margin = new Thickness(8, 4),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                MaxLines = 1,
            }.Assign(out _debug),
        };

        _scroll.LoadMoreCommand = new Command(() => _ = LoadMoreAsync(top: false));
        _scroll.LoadMoreTopCommand = new Command(() => _ = LoadMoreAsync(top: true));
        _scroll.Scrolled += (_, _) => UpdateDebug();

        UpdateStatus();
    }

    /// <summary>Deterministic pseudo-random body, so a given id always renders the same height.</summary>
    private static FeedItem MakeItem(int id)
    {
        var seed = (uint)((id + 100_000) * 2654435761);

        uint Next()
        {
            seed = seed * 1664525 + 1013904223;
            return seed;
        }

        double Rnd() => Next() / 4294967296.0;

        var count = 4 + (int)(Rnd() * 56);
        var body = string.Join(' ', Enumerable.Range(0, count).Select(_ => Words[(int)(Rnd() * Words.Length)]));
        body = char.ToUpperInvariant(body[0]) + body[1..] + ".";

        var color = Palette[((id % Palette.Length) + Palette.Length) % Palette.Length];
        return new FeedItem(id, $"Post {id}", body, color);
    }

    private static List<FeedItem> Range(int from, int count) =>
        Enumerable.Range(from, count).Select(MakeItem).ToList();

    private static SkiaButton JumpButton(string caption, string color, Action action) =>
        new SkiaButton(caption)
        {
            FontSize = 12,
            BackgroundColor = Color.Parse(color),
            WidthRequest = 104,
        }.OnTapped(me => action());

    private void Jump(int index, RelativePositionType option = RelativePositionType.Start) =>
        _scroll.ScrollToIndex(Math.Clamp(index, 0, Math.Max(0, _items.Count - 1)), true, option);

    /// <summary>
    /// Appending keeps every measured height; prepending keeps the visible rows in place — the
    /// framework's head-insert pipeline rebases the structure and pins the viewport.
    /// </summary>
    private async Task LoadMoreAsync(bool top)
    {
        if (_busy)
            return;

        _busy = true;
        _loading = top ? "loading history…" : "loading next page…";
        UpdateStatus();

        await Task.Delay(400);

        if (top)
        {
            var first = _items[0].Id;
            _items.InsertRange(0, Range(first - 30, 30));
        }
        else
        {
            var last = _items[^1].Id;
            _items.AddRange(Range(last + 1, 100));
        }

        _loading = string.Empty;
        UpdateStatus();
        _busy = false;
    }

    private void UpdateStatus() =>
        _status.Text = $"{_items.Count} uneven cells · MeasureVisible · LoadMore at both ends" +
                       (string.IsNullOrEmpty(_loading) ? string.Empty : $" · {_loading}");

    private void UpdateDebug()
    {
        var fps = Superview?.FPS ?? 0;
        var frameCost = fps > 0 ? $"{1000.0 / fps:0.0} ms" : "?";
        _debug.Text = $"{_feed.DebugString} · {frameCost} · {fps:0} fps";
    }
}
