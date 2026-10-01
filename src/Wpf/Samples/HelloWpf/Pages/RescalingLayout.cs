using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace HelloWpf.Pages;

/// <summary>
/// Fits a fixed logical viewport (LogicalWidth x LogicalHeight points) into its own box by changing the
/// RENDERING SCALE of its children, not by a Scale transform: children are measured, arranged and drawn
/// at the fitted scale, so layout, hit-testing and gestures just work. The layout-level twin of
/// Pong.Shared's RescalingCanvas, same as the React demo's pong/RescalingLayout.ts.
/// Keep it without Padding: its own insets are measured at the parent scale.
/// </summary>
public class RescalingLayout : SkiaLayout
{
    /// <summary>Design width in points.</summary>
    public float LogicalWidth { get; set; }

    /// <summary>Design height in points. 0 = fit the width only.</summary>
    public float LogicalHeight { get; set; }

    /// <summary>Rendering scale the children get, 0 until measured.</summary>
    public float ContextScale { get; private set; }

    /// <summary>Creates an absolute layout filling its parent.</summary>
    public RescalingLayout()
    {
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
    }

    /// <inheritdoc/>
    public override ScaledSize MeasureAbsolute(SKRect rectForChildrenPixels, float scale)
    {
        if (LogicalWidth > 0 && float.IsFinite(rectForChildrenPixels.Width) && rectForChildrenPixels.Width > 0)
        {
            var fit = rectForChildrenPixels.Width / (LogicalWidth * scale);
            if (LogicalHeight > 0 && float.IsFinite(rectForChildrenPixels.Height) && rectForChildrenPixels.Height > 0)
                fit = Math.Min(fit, rectForChildrenPixels.Height / (LogicalHeight * scale));

            ContextScale = scale * fit;
            return base.MeasureAbsolute(rectForChildrenPixels, ContextScale);
        }

        ContextScale = 0;
        return base.MeasureAbsolute(rectForChildrenPixels, scale);
    }

    /// <inheritdoc/>
    protected override int DrawViews(DrawingContext context)
    {
        if (ContextScale > 0)
            context.Scale = ContextScale;

        return base.DrawViews(context);
    }
}
