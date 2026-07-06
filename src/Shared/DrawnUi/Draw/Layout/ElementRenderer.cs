namespace DrawnUi.Draw
{
    public class ElementRenderer
    {
        public ElementRenderer(SkiaControl control)
        {
            Element = control;
            PaintWithOpacity = new SKPaint();
        }

        public void Dispose()
        {
            PaintWithOpacity?.Dispose();
        }

        /// <summary>
        /// Must not be used while rendering, only for createing render object
        /// </summary>
        protected readonly SkiaControl Element;

        /// <summary>
        /// Can be reused for drawing, single threaded only
        /// </summary>
        public SKPaint PaintWithOpacity { get; protected set; }

        public virtual RenderObject CreateRenderObject(DrawingContext ctx)
        {
            var ret = new RenderObject()
            {
                IsDistorted = Element.IsDistorted,
                WillClipBounds = Element.WillClipBounds,
                EffectPostRenderers = Element.EffectPostRenderers,
                ShouldClipAntialiased = Element.ShouldClipAntialiased
            };

            //todo add clipping

            if (Element.UsingCacheType != SkiaCacheType.None)
            {
                Element.DrawUsingRenderObject(ctx,
                    Element.SizeRequest.Width, Element.SizeRequest.Height);

                ret.Cache = Element.RenderObject;
            }

            return ret;
        }

     
    }
}