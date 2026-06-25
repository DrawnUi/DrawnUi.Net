namespace DrawnUi.Draw
{
    /// <summary>
    ///  MAUI Grid alternative
    /// </summary>
    public class SkiaGrid : SkiaLayout
    {
        public SkiaGrid()
        {
            Type = LayoutType.Grid;
            HorizontalOptions = LayoutOptions.Fill;
        }
    }
}
