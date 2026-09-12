using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Views;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>One post in the uneven feed.</summary>
public record FeedItem(int Id, string Title, string Body, string Color);

/// <summary>
/// Uneven recycled cell: body text wraps to 1..6 lines, so every row has its own height —
/// MeasureVisible territory. Ported from the React demo's FeedCell.ts.
/// </summary>
public class FeedCell : SkiaDynamicDrawnCell
{
    private SkiaShape _stripe;
    private SkiaLabel _title;
    private SkiaLabel _body;
    private SkiaLabel _footer;

    /// <summary>Builds the cell visuals.</summary>
    public FeedCell()
    {
        Type = LayoutType.Absolute;
        // A base SkiaLayout does not fill; without this the cell auto-sizes to the stripe.
        HorizontalOptions = LayoutOptions.Fill;
        Padding = new Thickness(16, 12, 16, 12);
        BackgroundColor = Color.Parse("#111827");
        UseCache = SkiaCacheType.ImageDoubleBuffered; // previous bitmap shown while a cell is re-recorded

        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                Type = ShapeType.Rectangle,
                CornerRadius = 3,
                WidthRequest = 6,
                VerticalOptions = LayoutOptions.Fill,
            }.Assign(out _stripe),

            new SkiaStack
            {
                Spacing = 6,
                Margin = new Thickness(18, 0, 0, 0),
                Children = new List<SkiaControl>
                {
                    new SkiaLabel
                    {
                        FontSize = 15,
                        FontFamily = "FontTextBold",
                        TextColor = Colors.White,
                    }.Assign(out _title),
                    new SkiaLabel
                    {
                        FontSize = 13,
                        TextColor = Color.Parse("#CBD5E1"),
                        HorizontalOptions = LayoutOptions.Fill,
                    }.Assign(out _body),
                    new SkiaLabel
                    {
                        FontSize = 11,
                        TextColor = Color.Parse("#64748B"),
                    }.Assign(out _footer),
                },
            },
        };
    }

    /// <inheritdoc/>
    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);

        if (ctx is not FeedItem item)
            return;

        _stripe.BackgroundColor = Color.Parse(item.Color);
        _title.Text = item.Title;
        _body.Text = item.Body;
        _footer.Text = $"#{item.Id} · {item.Body.Length} chars";
    }
}
