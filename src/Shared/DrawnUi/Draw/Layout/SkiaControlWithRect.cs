namespace DrawnUi.Draw
{

    /// <summary>
    /// Used inside RenderingTree. Rect is real drawing position
    /// </summary>
    /// <param name="Control"></param>
    /// <param name="Rect"></param>
    /// <param name="HitRect"></param>
    /// <param name="Index"></param>
    /// <param name="FreezeIndex"></param>
    /// <param name="FreezeBindingContext"></param>
    public record SkiaControlWithRect(SkiaControl Control,
        SKRect Rect,
        SKRect HitRect,
        int Index,
        int FreezeIndex,
        object FreezeBindingContext);
}
