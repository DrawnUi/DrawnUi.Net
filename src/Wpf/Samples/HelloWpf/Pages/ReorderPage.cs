using System.Collections.ObjectModel;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// Drag to reorder: a templated list reordered in place while the row being dragged floats over it.
/// Ported from the React demo's ReorderPage.tsx.
/// <para>
/// The grabbed row is lifted into a ghost (a copy above the scroll that follows the pointer) while the
/// real row draws blank, so the gap travelling through the list is the slot the row will land in.
/// Every step is an ObservableCollection.Move, which the layout applies to the cells it already has.
/// On release the ghost glides into the slot and hands the row back. Hold near the top or bottom edge
/// and the list keeps moving.
/// </para>
/// </summary>
public class ReorderPage : SkiaLayer, IDragHost
{
    private static readonly string[] Palette = { "#0D6EFD", "#6610F2", "#D63384", "#FD7E14", "#20C997", "#0DCAF0", "#FFC107" };

    /// <summary>Android's language preferences, in their own name and locale tag. Latin, Cyrillic and Greek only (OpenSans).</summary>
    private static readonly (string Title, string Tag)[] Languages =
    {
        ("English (United States)", "en-US"), ("Español (España)", "es-ES"), ("Français (France)", "fr-FR"),
        ("Deutsch (Deutschland)", "de-DE"), ("Italiano (Italia)", "it-IT"), ("Português (Brasil)", "pt-BR"),
        ("Nederlands (Nederland)", "nl-NL"), ("Svenska (Sverige)", "sv-SE"), ("Norsk bokmål (Norge)", "nb-NO"),
        ("Dansk (Danmark)", "da-DK"), ("Suomi (Suomi)", "fi-FI"), ("Íslenska (Ísland)", "is-IS"),
        ("Polski (Polska)", "pl-PL"), ("Čeština (Česko)", "cs-CZ"), ("Slovenčina (Slovensko)", "sk-SK"),
        ("Magyar (Magyarország)", "hu-HU"), ("Română (România)", "ro-RO"), ("Hrvatski (Hrvatska)", "hr-HR"),
        ("Slovenščina (Slovenija)", "sl-SI"), ("Bosanski (Bosna i Hercegovina)", "bs-BA"), ("Shqip (Shqipëri)", "sq-AL"),
        ("Lietuvių (Lietuva)", "lt-LT"), ("Latviešu (Latvija)", "lv-LV"), ("Eesti (Eesti)", "et-EE"),
        ("Русский (Россия)", "ru-RU"), ("Українська (Україна)", "uk-UA"), ("Беларуская (Беларусь)", "be-BY"),
        ("Български (България)", "bg-BG"), ("Македонски (Македонија)", "mk-MK"), ("Српски (Србија)", "sr-RS"),
        ("Қазақша (Қазақстан)", "kk-KZ"), ("Кыргызча (Кыргызстан)", "ky-KG"), ("Монгол (Монгол)", "mn-MN"),
        ("Ελληνικά (Ελλάδα)", "el-GR"), ("Türkçe (Türkiye)", "tr-TR"), ("Azərbaycan (Azərbaycan)", "az-AZ"),
        ("Oʻzbekcha (Oʻzbekiston)", "uz-UZ"), ("Català (Espanya)", "ca-ES"), ("Galego (España)", "gl-ES"),
        ("Euskara (Espainia)", "eu-ES"), ("Gaeilge (Éire)", "ga-IE"), ("Gàidhlig (Alba)", "gd-GB"),
        ("Cymraeg (Cymru)", "cy-GB"), ("Malti (Malta)", "mt-MT"), ("Bahasa Indonesia (Indonesia)", "id-ID"),
        ("Bahasa Melayu (Malaysia)", "ms-MY"), ("Filipino (Pilipinas)", "fil-PH"), ("Tiếng Việt (Việt Nam)", "vi-VN"),
        ("Kiswahili (Kenya)", "sw-KE"), ("Afrikaans (Suid-Afrika)", "af-ZA"),
    };

    private const float RowSpacing = 6;
    private const float StatusHeight = 58;
    /// <summary>How long the released ghost takes to glide into its slot.</summary>
    private const float DropSeconds = 0.14f;

    private ObservableCollection<ReorderItem> _items;
    private SkiaLabel _status;
    private SkiaScroll _scroll;
    private SkiaLayout _rows;
    private SkiaLayer _overlay;
    private SkiaShape _ghost;
    private SkiaLabel _ghostTitle;
    private SkiaLabel _ghostBadge;
    private ReorderItem _dragging;
    private float _grabOffset;
    private SkiaValueAnimator _drop;
    private int _dropIndex = -1;
    private float _dropFrom;
    private float _dropTo;
    private float _dropProgress;
    private long _dropAt;

    /// <summary>Builds the page.</summary>
    public ReorderPage()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        _items = Initial();

        Children = new List<SkiaControl>
        {
            new SkiaStack
            {
                Spacing = 2,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>
                {
                    new SkiaLabel($"{Languages.Length} languages in order of preference · drag by the grip, hold at an edge to keep going") { FontSize = 13, TextColor = Colors.LightGray, HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center },
                    new SkiaLabel("drag a language by its grip, or use the buttons") { FontSize = 12, TextColor = Color.Parse("#6EA8FE"), HorizontalOptions = LayoutOptions.Fill, HorizontalTextAlignment = DrawTextAlignment.Center }.Assign(out _status),
                },
            },

            new SkiaScroll
            {
                Orientation = ScrollOrientation.Vertical,
                Margin = new Thickness(0, StatusHeight, 0, 44),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = new SkiaStack
                {
                    ItemsSource = _items,
                    ItemTemplate = new DataTemplate(() => new ReorderCell(this)),
                    RecyclingTemplate = RecyclingTemplate.Enabled,
                    MeasureItemsStrategy = MeasuringStrategy.MeasureFirst,
                    Spacing = RowSpacing,
                    Padding = new Thickness(12, 8),
                    HorizontalOptions = LayoutOptions.Fill,
                }.Assign(out _rows),
            }
            .Assign(out _scroll)
            .Adapt(me => me.Scrolled += (_, _) => Report("scrolled")),

            // The lifted row lives here, above the scroll that clips the list, and never eats the drag it is showing.
            new SkiaLayer
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                InputTransparent = true,
                Children = new List<SkiaControl>
                {
                    new SkiaShape
                    {
                        Type = ShapeType.Rectangle,
                        CornerRadius = 8,
                        BackgroundColor = Color.Parse("#1D4ED8"),
                        StrokeWidth = 2,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start,
                        IsVisible = false,
                        Shadows = { new SkiaShadow { Blur = 12, Opacity = 0.5, X = 0, Y = 6, Color = Colors.Black } },
                        Children = new List<SkiaControl>
                        {
                            new SkiaLayer { WidthRequest = 26, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Fill, Margin = new Thickness(10, 0, 0, 0), Children = { ReorderCell.Bars("#BFDBFE") } },
                            new SkiaLabel { FontSize = 14, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(46, 0, 60, 0) }.Assign(out _ghostTitle),
                            new SkiaLabel { FontSize = 12, TextColor = Color.Parse("#BFDBFE"), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 14, 0) }.Assign(out _ghostBadge),
                        },
                    }.Assign(out _ghost),
                },
            }.Assign(out _overlay),

            new SkiaWrap
            {
                Spacing = 6,
                Margin = new Thickness(8, 0, 8, 8),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Children = new List<SkiaControl>
                {
                    new SkiaButton("1st below 10th") { FontSize = 13, BackgroundColor = Color.Parse("#495057") }.OnTapped(me => { Move(0, 9); Report("moved 1st below 10th"); }),
                    new SkiaButton("Reverse") { FontSize = 13, BackgroundColor = Color.Parse("#495057") }.OnTapped(me => { Replace(new ObservableCollection<ReorderItem>(_items.Reverse())); Report("reversed"); }),
                    new SkiaButton("Reset") { FontSize = 13, BackgroundColor = Color.Parse("#495057") }.OnTapped(me => { Replace(Initial()); Report("reset"); }),
                },
            },
        };
    }

    private static ObservableCollection<ReorderItem> Initial() =>
        new(Languages.Select((l, i) => new ReorderItem { Id = i + 1, Title = l.Title, Tag = l.Tag, Color = Palette[i % Palette.Length] }));

    private void Replace(ObservableCollection<ReorderItem> items)
    {
        _items = items;
        _rows.ItemsSource = items;
    }

    private void Report(string what)
    {
        var offset = _scroll?.ViewportOffsetY ?? 0;
        _status.Text = $"{what} · offset {offset:0} pt · {string.Join(", ", _items.Take(5).Select(i => i.Tag))}…";
    }

    /// <summary>Where a row sits on screen right now, in canvas pixels, while that row is realized.</summary>
    private SKRect? RowRect(int index)
    {
        // recycled cells are not in Views: the adapter holds the realized rows
        if (_rows.ChildrenFactory.GetCellInUseOrNull(index) is ReorderCell cell && cell.IsVisibleInViewTree())
            return cell.DrawingRect;

        return null;
    }

    /// <summary>The ghost is hidden between drags, so it is the overlay that always carries the current scale.</summary>
    private float Scale() => _overlay.RenderingScale > 0 ? _overlay.RenderingScale : 1;

    /// <summary>Point coordinates inside the overlay, which is where the ghost is laid out.</summary>
    private float ToGhostSpace(float pixels, bool top) => (pixels - (top ? _overlay.DrawingRect.Top : _overlay.DrawingRect.Left)) / Scale();

    /// <summary>Rebinds nothing, just re-applies each row's look: used when the list did not change but a row's state did.</summary>
    private void RefreshRows()
    {
        foreach (var view in _rows.ChildrenFactory.GetCellsInUse())
            (view as ReorderCell)?.Refresh();
    }

    private void Finish()
    {
        _drop?.Stop();
        _drop = null;
        _dropIndex = -1;
        _dragging = null;
        _ghost.IsVisible = false;
        _ghost.Update();
        RefreshRows(); // the real row draws itself again
        Report("dropped");
    }

    /// <summary>One frame of the settle, eased out so it lands rather than stops. The slot is re-read every frame because the list is still catching up with the last move when the pointer is released.</summary>
    private void Pump()
    {
        var now = Super.GetCurrentTimeNanos();
        _dropProgress += Math.Min(0.05f, (now - _dropAt) / 1_000_000_000f) / DropSeconds;
        _dropAt = now;
        if (_dropProgress >= 1)
        {
            Finish();
            return;
        }

        var rect = RowRect(_dropIndex);
        if (rect.HasValue)
            _dropTo = ToGhostSpace(rect.Value.Top, true);
        _ghost.Top = _dropFrom + (_dropTo - _dropFrom) * (1 - Math.Pow(1 - _dropProgress, 3));
        _ghost.Update();
    }

    #region IDragHost

    /// <inheritdoc/>
    public SkiaScroll Scroll => _scroll;

    /// <inheritdoc/>
    public int IndexOf(ReorderItem item) => _items.IndexOf(item);

    /// <inheritdoc/>
    public bool Move(int from, int to)
    {
        if (to < 0 || to >= _items.Count || from < 0 || from >= _items.Count)
            return false;

        _items.Move(from, to);
        Report("dragging");
        return true;
    }

    /// <inheritdoc/>
    public ReorderItem Dragging => _dragging;

    /// <inheritdoc/>
    public float Spacing => RowSpacing;

    /// <inheritdoc/>
    public void Lift(ReorderItem item, int index, float pointerY)
    {
        var rect = RowRect(index);
        if (!rect.HasValue)
            return;

        _drop?.Stop();
        _drop = null;
        _grabOffset = (pointerY - rect.Value.Top) / Scale();
        _dragging = item;
        _ghostTitle.Text = item.Title;
        _ghostBadge.Text = item.Tag;
        _ghost.StrokeColor = Color.Parse(item.Color);
        _ghost.WidthRequest = rect.Value.Width / Scale();
        _ghost.HeightRequest = rect.Value.Height / Scale();
        _ghost.Left = ToGhostSpace(rect.Value.Left, false);
        _ghost.Top = ToGhostSpace(rect.Value.Top, true);
        _ghost.IsVisible = true;
        _ghost.Update();
        RefreshRows(); // blanks the row the ghost is now standing in for
        Report("picked up");
    }

    /// <inheritdoc/>
    public void Carry(float pointerY)
    {
        if (_dragging == null || _drop != null)
            return;

        _ghost.Top = ToGhostSpace(pointerY, true) - _grabOffset;
        _ghost.Update();
    }

    /// <inheritdoc/>
    public void Drop(int index)
    {
        if (_dragging == null)
        {
            Finish();
            return;
        }

        _dropIndex = index;
        _dropFrom = (float)_ghost.Top;
        _dropTo = _dropFrom;
        _dropProgress = 0;
        _dropAt = Super.GetCurrentTimeNanos();
        _drop = new SkiaValueAnimator(this) { mMinValue = 0, mMaxValue = 1, Speed = 1000, Repeat = -1, OnUpdated = _ => Pump() };
        _drop.Start();
    }

    #endregion
}
