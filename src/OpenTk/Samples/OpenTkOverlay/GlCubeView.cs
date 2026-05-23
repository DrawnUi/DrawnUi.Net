using DrawnUi.Draw;
using DrawnUi.Views;

/// <summary>
/// Full-screen SkiaControl that delegates its paint to a GL draw callback.
/// Placed as the first child of the Canvas so it renders as the 3D background.
/// </summary>
internal sealed class GlCubeView : SkiaControl
{
    private readonly Action<DrawingContext> _renderGl;

    public GlCubeView(Action<DrawingContext> renderGl)
    {
        _renderGl = renderGl;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
    }

    protected override void Paint(DrawingContext ctx)
    {
        _renderGl(ctx);
    }
}
