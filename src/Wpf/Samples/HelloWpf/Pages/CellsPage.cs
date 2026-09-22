using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// Recycled cells: the templated layout is the scroll's ONLY content, the DrawnUI way.
/// 100 000 items, only the visible cells exist. Ported from the React demo's CellsPage.tsx.
/// </summary>
public class CellsPage : SkiaLayer
{
    private const double StatusHeight = 36;

    // Huge data source, like the "Cells" fiddle: 100 000 items.
    private static readonly List<int> Items = Enumerable.Range(1, 100_000).ToList();

    private SkiaScroll _scroll;
    private SkiaLayout _feed;
    private SkiaLabel _status;
    private SkiaLabel _debug;
    private int _lastTapped;

    /// <summary>Builds the page.</summary>
    public CellsPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            new SkiaLabel("100 000 recycled cells · last tapped: -")
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
                Content = new SkiaStack
                {
                    ItemsSource = Items,
                    ItemTemplate = new DataTemplate(() => new ContactCell(OnCellTapped)),
                    RecyclingTemplate = RecyclingTemplate.Enabled,
                    MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                    Spacing = 8,
                    Padding = new Thickness(16, 8),
                }.Assign(out _feed),
            }.Fill().Assign(out _scroll),

            // jump toolbar: wraps on narrow windows
            new SkiaWrap
            {
                Spacing = 6,
                Margin = new Thickness(8, 0, 8, 36),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Children = new List<SkiaControl>
                {
                    JumpButton("HOME", () => Jump(0)),
                    JumpButton("BACKWARD", () => Jump(_feed.FirstVisibleIndex - 5)),
                    JumpButton("MIDDLE", () => Jump(Items.Count / 2)),
                    JumpButton("FORWARD", () => Jump(_feed.FirstVisibleIndex + 5)),
                    JumpButton("END", () => Jump(Items.Count, RelativePositionType.End)),
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

        _scroll.Scrolled += (_, _) => UpdateDebug();

    }

    private static SkiaButton JumpButton(string caption, Action action) =>
        new SkiaButton(caption)
        {
            FontSize = 12,
            BackgroundColor = Color.Parse("#0D6EFD"),
            WidthRequest = 104,
        }.OnTapped(me => action());

    private void Jump(int index, RelativePositionType option = RelativePositionType.Start)
    {
        _scroll.ScrollToIndex(Math.Clamp(index, 0, Items.Count - 1), true, option);
    }

    private void OnCellTapped(int item)
    {
        _lastTapped = item;
        _status.Text = $"100 000 recycled cells · last tapped: {_lastTapped}";
    }

    private void UpdateDebug()
    {
        var view = Superview;

        // DrawnView.FrameTime is the timestamp the frame started at, not its duration, so the
        // per-frame cost is derived from the measured FPS instead.
        var fps = view?.FPS ?? 0;
        var frameCost = fps > 0 ? $"{1000.0 / fps:0.0} ms" : "?";

        _debug.Text = $"{_feed.DebugString} · {frameCost} · {fps:0} fps";
    }
}
