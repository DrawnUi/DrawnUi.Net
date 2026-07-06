namespace DrawnUi.Draw
{
    public interface IRenderObject
    {
        bool UseRenderingObject(RenderDrawingContext context,
            SKRect destination,
            float scale);
    }
}