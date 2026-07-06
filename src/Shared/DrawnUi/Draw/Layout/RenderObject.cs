namespace DrawnUi.Draw
{
    public class RenderObject
    {

        public void Dispose()
        {
            //todo dispose cache etc?
        }

        public bool ShouldClipAntialiased { get; set; }
        public SKPath ClippingPath { get; set; }
        public CachedObject Cache { get; set; }
        public bool IsDistorted { get; set; }
        public bool WillClipBounds { get; set; }
        public List<IPostRendererEffect> EffectPostRenderers { get; set; }

        public Action<DrawingContext, CachedObject> DelegateDrawCache { get; set; }

        public virtual void DrawRenderObject(
            DrawingContext ctx,
            CachedObject cache)
        {

        }

    }
}