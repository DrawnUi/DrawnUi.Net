using DrawnUi.Draw;
using DrawnUi.Views;
using Microsoft.AspNetCore.Components;
using SkiaSharp;

namespace BlazorSandbox.Pages;

public class AspectLockedCanvas : Canvas
{
    [Parameter]
    public float LogicalWidth { get; set; }

    [Parameter]
    public float LogicalHeight { get; set; }

    protected override void Draw(DrawingContext context)
    {
        if (Content == null)
        {
            base.Draw(context);
            return;
        }

        var availableWidth = DrawingRect.Width;
        var availableHeight = DrawingRect.Height;
        if (availableWidth <= 0 || availableHeight <= 0 || LogicalWidth <= 0 || LogicalHeight <= 0)
        {
            base.Draw(context);
            return;
        }

        var wantedWidth = LogicalWidth * context.Scale;
        var wantedHeight = LogicalHeight * context.Scale;
        var scale = Math.Min(availableWidth / wantedWidth, availableHeight / wantedHeight);
        if (scale <= 0)
        {
            scale = 1f;
        }

        if (scale < 1f)
        {
            var fittedWidth = wantedWidth * scale;
            var fittedHeight = wantedHeight * scale;
            var offsetX = context.Destination.Left + (availableWidth - fittedWidth) * 0.5f;
            var offsetY = context.Destination.Top + (availableHeight - fittedHeight) * 0.5f;
            var logicalDestination = new SKRect(0, 0, wantedWidth, wantedHeight);

            context.Context.Canvas.Save();

            try
            {
                context.Context.Canvas.Translate(offsetX, offsetY);
                context.Context.Canvas.Scale(scale, scale);
                base.Draw(context.WithDestination(logicalDestination).WithScale(context.Scale));
            }
            finally
            {
                context.Context.Canvas.Restore();
            }

            return;
        }

        var originalScale = context.Scale;
        context.Scale *= scale;
        base.Draw(context);
        context.Scale = originalScale;
    }
}