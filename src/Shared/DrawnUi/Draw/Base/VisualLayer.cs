namespace DrawnUi.Draw
{
    public class VisualLayer
    {
        private bool _isValid;
        private SKPoint scaleTotal;
        private SKPoint translationTotal;
        private double rotationTotal;

        void Recalculate()
        {
            if (!_isValid)
            {
                DecomposeMatrix(TransformsTotal, out var combinedScale, out var rotation, out var translation);
                ScaleTotal = combinedScale;
                RotationTotal = rotation;
                TranslationTotal = translation;
                _isValid = true;
            }
        }

        /// <summary>
        /// Child of a cached object
        /// </summary>
        public bool IsFrozen { get; set; }

        /// <summary>
        /// The SkiaControl this layer represents
        /// </summary>
        public SkiaControl Control { get; set; }

        /// <summary>
        /// Child layers contained within this layer
        /// </summary>
        public List<VisualLayer> Children { get; set; }

        /// <summary>
        /// Cached rendering object, null means render control directly
        /// </summary>
        public CachedObject Cache { get; set; }

        /// <summary>
        /// Type of caching applied to this layer
        /// </summary>
        public SkiaCacheType Cached { get; set; }

        /// <summary>
        /// Layout bounds in local coordinates
        /// </summary>
        public SKRect Destination { get; set; }

        /// <summary>
        /// Layout bounds in local coordinates at time of creation
        /// </summary>
        public SKRect Origin { get; }

        /// <summary>
        /// Local transformation matrix applied to this layer
        /// </summary>
        public SKMatrix Transforms { get; protected set; }

        /// <summary>
        /// Combined transformation matrix including all parent transforms
        /// </summary>
        public SKMatrix TransformsTotal { get; protected set; }

        /// <summary>
        /// Combined opacity including all parent opacities
        /// </summary>
        public double OpacityTotal { get; set; }

        /// <summary>
        /// Total rotation in degrees extracted from transform matrix
        /// </summary>
        public double RotationTotal
        {
            get
            {
                Recalculate();
                return rotationTotal;
            }
            set => rotationTotal = value;
        }

        /// <summary>
        /// Total scale factors extracted from transform matrix
        /// </summary>
        public SKPoint ScaleTotal   
        {
            get
            {
                Recalculate();
                return scaleTotal;
            }
            set => scaleTotal = value;
        }

        /// <summary>
        /// Total translation extracted from transform matrix
        /// </summary>
        public SKPoint TranslationTotal
        {
            get
            {
                Recalculate();
                return translationTotal;
            }
            set => translationTotal = value;
        }

        /// <summary>
        /// A hitbox rather for internal use, because it is same as LastDrawnAt. For exact position on canvas use HitBoxWithTransforms.
        /// </summary>
        public ScaledRect HitBox { get; set; }

        /// <summary>
        /// Exact position on canvas use HitBoxWithTransforms, all matrix transforms and Left, Top offset applied.
        /// </summary>
        public ScaledRect HitBoxWithTransforms { get; set; }

        public void Render(DrawingContext context)
        {
            //todo draw cache or direct
            if (Control == null)
            {
                throw new ApplicationException("VisualNode SkiaControl is not set");
            }

            if (Cache != null)
            {
                Control.DrawRenderObjectInternal(context.WithDestination(Cache.RecordingArea), Cache);
            }
            else
            {
                Control.DrawDirectInternal(context, Destination);
            }
        }

        #region VisualTransforms

        public static void DecomposeMatrix(SKMatrix m, out SKPoint scale, out float rotation, out SKPoint translation)
        {
            // Extract translation
            translation = new SKPoint(m.TransX, m.TransY);

            // Extract scale
            float scaleX = (float)Math.Sqrt(m.ScaleX * m.ScaleX + m.SkewY * m.SkewY);
            float scaleY = (float)Math.Sqrt(m.ScaleY * m.ScaleY + m.SkewX * m.SkewX);
            scale = new SKPoint(scaleX, scaleY);

            // Extract rotation (in radians)
            float rotationRad = (float)Math.Atan2(m.SkewY, m.ScaleX);
            rotation = rotationRad * (180f / (float)Math.PI);
        }

        /// <summary>
        /// Attaches cached children and relocates them to account for new parent position
        /// </summary>
        /// <param name="cache">Cached object containing children to attach</param>
        public void AttachFromCache(CachedObject cache)
        {
            if (Control==null || cache.Children == null || cache.Children.Count == 0)
                return;

            Children = cache.Children;

            // Use cache.Bounds as the original parent destination
            // Calculate position delta: where we are now vs where cache was created
            var deltaX = this.Destination.Left - cache.Bounds.Left;
            var deltaY = this.Destination.Top - cache.Bounds.Top;

            VisualLayer actualParent = null;
            if (this.Control.Parent is SkiaControl parentControl)
            {
                actualParent = parentControl.VisualLayer;
            }

            // Pass the actual parent, not null
            ApplyTransformsToChildren(actualParent, true, deltaX, deltaY);

        }

        /// <summary>
        /// Combined method: relocates positions AND updates transforms in single recursive pass
        /// </summary>
        /// <param name="node">The node to update</param>
        /// <param name="parent">Parent node for transform calculations</param>
        /// <param name="frozen">Whether children should be marked as frozen</param>
        /// <param name="deltaX">X offset to apply for relocation</param>
        /// <param name="deltaY">Y offset to apply for relocation</param>
        private void ApplyTransformsToChildren(VisualLayer parent, bool frozen, float deltaX, float deltaY)
        {
            // Step 1: Update destination from Origin + delta
            var newDestination = this.Origin;
            newDestination.Offset(deltaX, deltaY);
            this.Destination = newDestination;

            // Step 2: Update hit box from Origin + delta
            this.HitBox = ScaledRect.FromPixels(newDestination, this.HitBox.Scale);

            // Step 3: Update transforms (same as your existing logic)
            if (parent != null)
            {
                // Create a translation matrix for the delta
                var deltaTransform = SKMatrix.CreateTranslation(deltaX, deltaY);

                // Apply: parent transforms → delta → local transforms
                this.TransformsTotal = parent.TransformsTotal
                    .PostConcat(deltaTransform)
                    .PostConcat(this.Transforms);

                this.HitBoxWithTransforms = ScaledRect.FromPixels(
                    TransformRect(this.HitBox.Pixels, this.TransformsTotal),
                    this.HitBox.Scale);
                this.OpacityTotal = parent.OpacityTotal * (this.Control?.Opacity ?? 1.0);
            }
            else
            {
                var deltaTransform = SKMatrix.CreateTranslation(deltaX, deltaY);
                this.TransformsTotal = deltaTransform.PostConcat(this.Transforms);
                this.HitBoxWithTransforms = ScaledRect.FromPixels(
                    TransformRect(this.HitBox.Pixels, this.TransformsTotal),
                    this.HitBox.Scale);
            }

            _isValid = false;

            foreach (var child in this.Children)
            {
                child.IsFrozen = frozen;
                child.ApplyTransformsToChildren(this, frozen, deltaX, deltaY);
            }
        }

        #endregion

        /// <summary>
        /// Create this ONLY when DrawingRect and RenderTransformMatrix are ready
        /// </summary>
        /// <param name="control"></param>
        /// <param name="parent"></param>
        /// <param name="destination"></param>
        /// <param name="scale"></param>
        /// <param name="tag"></param>
        public VisualLayer(SkiaControl control, VisualLayer parent, SKRect destination, float scale)
        {
            Control = control;
            Children = new();
            Transforms = control.RenderTransformMatrix;

            TransformsTotal = Transforms;
            Destination = destination;
            Origin = destination;

            if (Control is SkiaControl skiaControl)
            {
                Cached = control.UsingCacheType;
                HitBox = ScaledRect.FromPixels(destination, scale);

                // Don't add Left/Top offset if destination already includes it
                var rectToTransform = destination; // Use destination directly

                if (parent != null)
                {
                    TransformsTotal = parent.TransformsTotal.PostConcat(Transforms);
                    HitBoxWithTransforms =
                        ScaledRect.FromPixels(TransformRect(rectToTransform, TransformsTotal), scale);
                    OpacityTotal = (parent?.OpacityTotal ?? 1f) * (Control?.Opacity ?? 1f);
                }
                else
                {
                    OpacityTotal = (Control?.Opacity ?? 1f);
                    HitBoxWithTransforms = ScaledRect.FromPixels(TransformRect(rectToTransform, Transforms), scale);
                }

                DecomposeMatrix(TransformsTotal, out var combinedScale, out var rotation, out var translation);

                ScaleTotal = combinedScale;
                RotationTotal = rotation;
                TranslationTotal = translation;
            }
        }

        protected VisualLayer()
        {
            Children = new();
        }

        public static VisualLayer CreateEmpty()
        {
            return new VisualLayer();
        }

        /*

        /// <summary>
        /// Creates a deep clone of this rendered node tree and updates its positions based on new destination
        /// </summary>
        /// <param name="newDestination">The new destination rectangle</param>
        /// <param name="scale">The scale factor</param>
        /// <param name="parentNode">Optional parent rendered node for transform calculations</param>
        /// <returns>A new VisualNode with updated positions</returns>
        public VisualLayerDraft CloneAndUpdateTree(SKRect newDestination, float scale, VisualLayerDraft parentNode = null)
        {
            // Clone the node
            VisualLayerDraft clone = new VisualLayerDraft(
                Control,
                null, // Parent will be set through parentNode parameter
                newDestination,
                scale, this.Tag
            );

            // Copy essential properties
            clone.Transforms = Transforms; // Local transforms stay the same

            // Calculate the delta between original and new destination
            float deltaX = newDestination.Left - Destination.Left;
            float deltaY = newDestination.Top - Destination.Top;

            // Update hit boxes
            var updatedHitBox = HitBox.Pixels;
            updatedHitBox.Offset(deltaX, deltaY);
            clone.HitBox = ScaledRect.FromPixels(updatedHitBox, scale);

            // Recalculate transforms total and hit box with transforms
            if (parentNode != null)
            {
                clone.TransformsTotal = clone.Transforms;
                clone.TransformsTotal = parentNode.TransformsTotal.PostConcat(clone.TransformsTotal);

                var mappedHitBox = clone.HitBox.Pixels;
                clone.HitBoxWithTransforms = ScaledRect.FromPixels(
                    TransformRect(mappedHitBox, clone.TransformsTotal),
                    scale);
            }
            else if (Control?.Parent is SkiaControl parent && parent.VisualLayerPreparing != null)
            {
                // Use parent's rendered node if available
                clone.TransformsTotal = clone.Transforms;
                clone.TransformsTotal = parent.VisualLayerPreparing.TransformsTotal.PostConcat(clone.TransformsTotal);

                var mappedHitBox = clone.HitBox.Pixels;
                clone.HitBoxWithTransforms = ScaledRect.FromPixels(
                    TransformRect(mappedHitBox, clone.TransformsTotal),
                    scale);
            }
            else
            {
                // Root node case
                clone.HitBoxWithTransforms = ScaledRect.FromPixels(
                    TransformRect(clone.HitBox.Pixels, clone.Transforms),
                    scale);
            }

            // Recursively process children
            clone.Children = new List<VisualLayerDraft>();
            foreach (var child in Children)
            {
                // Calculate new child destination by applying the same delta
                var childDestination = child.Destination;
                childDestination.Offset(deltaX, deltaY);

                var childClone = child.CloneAndUpdateTree(childDestination, scale, clone);
                clone.Children.Add(childClone);
            }

            return clone;
        }

        */

        /// <summary>
        /// Helper to transform a rectangle using a matrix
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="matrix"></param>
        /// <returns></returns>
        public static SKRect TransformRect(SKRect rect, SKMatrix matrix)
        {
            // Transform all four corners of the rectangle
            SKPoint[] corners = new SKPoint[4]
            {
                new SKPoint(rect.Left, rect.Top), new SKPoint(rect.Right, rect.Top),
                new SKPoint(rect.Right, rect.Bottom), new SKPoint(rect.Left, rect.Bottom)
            };

            for (int i = 0; i < 4; i++)
            {
                corners[i] = matrix.MapPoint(corners[i]);
            }

            return new SKRect(
                Math.Min(Math.Min(corners[0].X, corners[1].X), Math.Min(corners[2].X, corners[3].X)),
                Math.Min(Math.Min(corners[0].Y, corners[1].Y), Math.Min(corners[2].Y, corners[3].Y)),
                Math.Max(Math.Max(corners[0].X, corners[1].X), Math.Max(corners[2].X, corners[3].X)),
                Math.Max(Math.Max(corners[0].Y, corners[1].Y), Math.Max(corners[2].Y, corners[3].Y))
            );
        }
    }

 

}
