namespace DrawnUi.Draw;

/// <summary>
/// Look and timing of the tooltips a canvas shows for controls with a <see cref="SkiaControl.Tooltip"/>. Set app-wide
/// with <see cref="Super.TooltipStyle"/>, or per canvas with <c>DrawnView.TooltipStyle</c>.
/// </summary>
public class TooltipStyle
{
    /// <summary>Background of the tooltip: dark, a step lighter than dark app surfaces.</summary>
    public Color BackgroundColor { get; set; } = Color.FromArgb("#2B2D31");

    /// <summary>Color of the tooltip text.</summary>
    public Color TextColor { get; set; } = Color.FromArgb("#F2F3F5");

    /// <summary>Color of the 1 pt border, lifts the tooltip off dark surfaces. Null or transparent for none.</summary>
    public Color BorderColor { get; set; } = Color.FromArgb("#24FFFFFF");

    /// <summary>Color of the soft shadow under the tooltip. Null or transparent for none.</summary>
    public Color ShadowColor { get; set; } = Color.FromArgb("#59000000");

    /// <summary>Font family of the tooltip text, null for the default font.</summary>
    public string FontFamily { get; set; }

    /// <summary>Font size of the tooltip text, in points.</summary>
    public double FontSize { get; set; } = 13;

    /// <summary>Corner radius of the tooltip, in points.</summary>
    public double CornerRadius { get; set; } = 6;

    /// <summary>Space around the text, in points.</summary>
    public Thickness Padding { get; set; } = new(12, 8);

    /// <summary>Longer text wraps at this width, in points.</summary>
    public double MaxWidth { get; set; } = 320;

    /// <summary>How long the mouse rests on a control before its tooltip shows, in milliseconds.</summary>
    public int ShowDelayMs { get; set; } = 600;

    /// <summary>
    /// A tooltip that closed less than this many milliseconds ago lets the next one show at once, so moving along a
    /// toolbar shows each tooltip without waiting again.
    /// </summary>
    public int FollowWindowMs { get; set; } = 500;

    /// <summary>
    /// False (default): the tooltip is attached to its control, centered 6 pt above it (below it where there is no
    /// room). True: it opens at the mouse, below and to the right of the pointer where it rested (above it where there
    /// is no room), as the Windows and macOS tooltips do. Either way it stays put while the mouse moves over the control.
    /// </summary>
    public bool AtCursor { get; set; }

    /// <summary>Moves the tooltip right (negative: left) from where it would go, in points.</summary>
    public double OffsetX { get; set; }

    /// <summary>
    /// Moves the tooltip down (negative: up) from where it would go, in points. On the flipped side (below the control
    /// when there is no room above, above the pointer when there is no room below) it moves the other way, so it keeps
    /// its distance from the control or the pointer.
    /// </summary>
    public double OffsetY { get; set; }

    /// <summary>
    /// Builds the tooltip content for a text instead of the default rounded label. The control is measured and drawn
    /// on its own, outside the visual tree: it takes no input and screen readers do not see it.
    /// </summary>
    public Func<string, SkiaControl> CreateTooltip { get; set; }

    /// <summary>The default tooltip content: the text on a rounded background.</summary>
    public SkiaControl CreateDefault(string text)
    {
        var shadow = ShadowColor != null && ShadowColor.Alpha > 0;
        var shape = new SkiaShape
        {
            CornerRadius = CornerRadius,
            BackgroundColor = BackgroundColor,
            StrokeColor = BorderColor ?? Colors.Transparent,
            StrokeWidth = BorderColor != null && BorderColor.Alpha > 0 ? 1 : 0,
            Padding = Padding,
            Margin = shadow ? new Thickness(ShadowRoom) : new Thickness(0), // the shadow is drawn inside the measured size
            MaximumWidthRequest = MaxWidth,
            UseCache = SkiaCacheType.Image,
            Children =
            {
                new SkiaLabel(text)
                {
                    TextColor = TextColor,
                    FontSize = FontSize,
                    FontFamily = FontFamily,
                }
            }
        };
        if (shadow)
            shape.Shadows.Add(new SkiaShadow { X = 0, Y = 2, Blur = 6, Opacity = 1, Color = ShadowColor });
        return shape;
    }

    /// <summary>Room left around the default tooltip for its shadow, in points.</summary>
    const double ShadowRoom = 8;
}

/// <summary>
/// A head that shows tooltips in a window of their own, so they can go past the edge of the app window. Without one
/// the canvas draws the tooltip itself, inside its bounds.
/// </summary>
public interface ITooltipHost
{
    /// <summary>
    /// Shows <paramref name="image"/> with its top-left corner at <paramref name="position"/>, in canvas pixels
    /// (may lie outside the canvas). The image belongs to the host from now on. Called on the drawing thread.
    /// </summary>
    void ShowTooltip(SKImage image, SKPoint position, float scale);

    /// <summary>Closes the tooltip. Called on the drawing thread.</summary>
    void HideTooltip();
}
