using DrawnUi.Draw;
using DrawnUi.Views;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DrawnUi.Views;

public class AspectLockedCanvas : Canvas
{
    [Parameter]
    public float LogicalWidth { get; set; }

    [Parameter]
    public float LogicalHeight { get; set; }

    protected override bool CenterSurface => true;

    protected override bool KeepRequestedCanvasSizeInBrowserFullscreen => true;

    protected override bool UseRequestedSizeForHostLayout => false;

    [JSInvokable]
    public override async Task OnHostResized(double width, double height)
    {
        if (LogicalWidth > 0 && LogicalHeight > 0 && width > 0 && height > 0)
        {
            var fitScale = Math.Min(width / LogicalWidth, height / LogicalHeight);
            WidthRequest = LogicalWidth * fitScale;
            HeightRequest = LogicalHeight * fitScale;
        }
        await base.OnHostResized(width, height);
    }

    protected override DrawingContext CreateContentContext(DrawingContext context)
    {
        if (LogicalWidth <= 0 || context.Destination.Width <= 0)
            return context;

        var fitScale = context.Destination.Width / (LogicalWidth * context.Scale);
        return Math.Abs(fitScale - 1f) < 0.001f
            ? context
            : context.WithScale(context.Scale * fitScale);
    }
}
