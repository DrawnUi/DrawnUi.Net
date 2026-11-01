namespace DrawnUi.Draw;

/// <summary>
/// Look and timing of the tooltips a canvas shows for controls with a <see cref="SkiaControl.Tooltip"/>. Set app-wide
/// with <see cref="Super.TooltipStyle"/>, or per canvas with <c>DrawnView.TooltipStyle</c>.
/// </summary>
public class TooltipStyle
{
    /// <summary>Background of the tooltip.</summary>
    public Color BackgroundColor { get; set; } = Color.FromArgb("#F2202124");

    /// <summary>Color of the tooltip text.</summary>
    public Color TextColor { get; set; } = Colors.White;

    /// <summary>Font family of the tooltip text, null for the default font.</summary>
    public string FontFamily { get; set; }

    /// <summary>Font size of the tooltip text, in points.</summary>
    public double FontSize { get; set; } = 13;

    /// <summary>Corner radius of the tooltip, in points.</summary>
    public double CornerRadius { get; set; } = 6;

    /// <summary>Space around the text, in points.</summary>
    public Thickness Padding { get; set; } = new(8, 5);

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
    /// Builds the tooltip content for a text instead of the default rounded label. The control is measured and drawn
    /// on its own, outside the visual tree: it takes no input and screen readers do not see it.
    /// </summary>
    public Func<string, SkiaControl> CreateTooltip { get; set; }

    /// <summary>The default tooltip content: the text on a rounded background.</summary>
    public SkiaControl CreateDefault(string text)
    {
        return new SkiaShape
        {
            CornerRadius = CornerRadius,
            BackgroundColor = BackgroundColor,
            Padding = Padding,
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
    }
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
