namespace DrawnUi.Draw
{
    /// <summary>
    /// Alias for SkiaShape type Rectangle
    /// </summary>
    public partial class SkiaFrame : SkiaShape
    {
        public SkiaFrame()
        {
            Type = ShapeType.Rectangle;
        }

        /// <inheritdoc />
        protected override bool ClipDependsOnlyOnShape => GetType() == typeof(SkiaFrame);
    }
}
