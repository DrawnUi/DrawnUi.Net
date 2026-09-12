using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUi.Controls;
using Color = DrawnUi.Color;

namespace HelloWpf.Pages;

/// <summary>
/// Recycled cell, the DrawnUI way: visuals built once in the constructor, <see cref="SetContent"/>
/// runs on every rebind. Ported from the React demo's ContactCell.ts.
/// </summary>
public class ContactCell : SkiaDynamicDrawnCell
{
    private SkiaLabel _initials;
    private SkiaLabel _title;
    private SkiaLabel _subtitle;

    /// <summary>Builds the cell visuals.</summary>
    /// <param name="onTap">Called with the bound item when the cell is tapped.</param>
    public ContactCell(Action<int> onTap)
    {
        // Absolute layout: avatar at the start, text column fills the remaining width. A Row would give
        // the column an infinite width and MaxLines could never truncate.
        Type = LayoutType.Absolute;
        // A base SkiaLayout does not fill (only the aliases do), so without this the cell auto-sizes
        // to the avatar and the Fill text column collapses to nothing.
        HorizontalOptions = LayoutOptions.Fill;
        Padding = new Thickness(12, 10);
        BackgroundColor = Color.Parse("#111827");
        UseCache = SkiaCacheType.Image; // one bitmap per cell, blitted while scrolling
        AnimationTapped = SkiaTouchAnimation.Ripple;

        Children = new List<SkiaControl>
        {
            new SkiaShape
            {
                Type = ShapeType.Circle,
                WidthRequest = 42,
                LockRatio = 1,
                VerticalOptions = LayoutOptions.Center,
                BackgroundColor = Color.Parse("#1F2937"),
                Children = new List<SkiaControl>
                {
                    new SkiaLabel
                    {
                        FontSize = 14,
                        TextColor = Color.Parse("#67E8F9"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                    }.Assign(out _initials),
                },
            },
            new SkiaStack
            {
                Spacing = 3,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill, // labels get the remaining width so MaxLines can truncate
                Margin = new Thickness(54, 0, 0, 0),    // avatar 42 + gap 12
                Children = new List<SkiaControl>
                {
                    new SkiaLabel
                    {
                        FontSize = 15,
                        TextColor = Colors.White,
                    }.Assign(out _title),
                    new SkiaLabel
                    {
                        FontSize = 12,
                        TextColor = Color.Parse("#94A3B8"),
                        MaxLines = 1, // ellipsis on narrow screens
                    }.Assign(out _subtitle),
                },
            },
        };

        this.OnTapped(me =>
        {
            if (me.BindingContext is int item)
                onTap(item);
        });
    }

    /// <inheritdoc/>
    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);

        if (ctx is not int i)
            return;

        _initials.Text = $"{i % 100}";
        _title.Text = $"Contact {i}";
        _subtitle.Text = $"Recycled drawn cell #{i} — scroll me fast";
    }
}
