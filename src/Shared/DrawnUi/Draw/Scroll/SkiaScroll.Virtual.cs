namespace DrawnUi.Draw
{
    public partial class SkiaScroll
    {
        /// <summary>
        /// When true the scroll does not lay out a real Content tree; a subclass draws its own
        /// content directly via <see cref="DrawVirtual"/> (e.g. <see cref="VirtualScroll"/> / wheel pickers).
        /// Base scrolls return false.
        /// </summary>
        public virtual bool UseVirtual => false;

        /// <summary>
        /// Draws virtual content directly while scrolling. Overridden by virtual scrolls that
        /// generate/draw their cells on demand instead of using a measured Content tree.
        /// </summary>
        public virtual void DrawVirtual(DrawingContext context)
        {
        }
    }
}
