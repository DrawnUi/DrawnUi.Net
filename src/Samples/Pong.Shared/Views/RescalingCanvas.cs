using DrawnUi.Draw;
using DrawnUi.Views;

namespace Pong.Game;

/// <summary>
/// Canvas that scales its content so a fixed logical viewport (LogicalWidth x LogicalHeight)
/// fits the available space while maintaining aspect ratio.
/// </summary>
public class RescalingCanvas : Canvas
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
