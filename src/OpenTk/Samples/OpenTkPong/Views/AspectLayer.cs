using DrawnUi.Draw;
using DrawnUi.Views;

namespace OpenTkPong;

/// <summary>
/// SkiaLayer that scales its children so a fixed logical viewport (LogicalWidth x LogicalHeight)
/// fills the available space while maintaining aspect ratio, centered.
/// Based on the RescalingCanvas pattern from the MAUI GameTemplate sample.
/// </summary>
public class AspectLayer : SkiaLayer
{
    public float LogicalWidth { get; set; }
    public float LogicalHeight { get; set; }

    protected override void Draw(DrawingContext context)
    {
        if (LogicalWidth <= 0 || LogicalHeight <= 0)
        {
            base.Draw(context);
            return;
        }

        var wantedWidth = LogicalWidth * context.Scale;
        var wantedHeight = LogicalHeight * context.Scale;

        var scaleX = DrawingRect.Width / wantedWidth;
        var scaleY = DrawingRect.Height / wantedHeight;
        var gameScale = Math.Min(scaleX, scaleY);

        context.Scale *= (float)gameScale;

        base.Draw(context);
    }
}
