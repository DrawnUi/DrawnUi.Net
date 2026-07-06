namespace DrawnUi.Draw;

public interface ICanRenderOnCanvas
{
    /// <summary>
    /// Renders effect overlay to canvas, return true if has drawn something and rendering needs to be applied.
    /// </summary>
    /// <param name="control"></param>
    /// <param name="context"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
    bool Render(DrawingContext context, IDrawnBase control);
}

public interface IOverlayEffect : ICanRenderOnCanvas, ISkiaAnimator
{

}
