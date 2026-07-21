using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DrawnUi.Draw
{
    public partial class SkiaControl
    {
        #region EFFECTS

        private static void EffectsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl control)
            {

                var skiaEffects = (IEnumerable<SkiaEffect>)newvalue;

                if (oldvalue != null)
                {
                    if (oldvalue is INotifyCollectionChanged oldCollection)
                    {
                        oldCollection.CollectionChanged -= control.EffectsCollectionChanged;
                    }

                    if (oldvalue is IEnumerable<SkiaEffect> oldList)
                    {
                        foreach (var skiaEffect in oldList)
                        {
                            skiaEffect.Dettach();
                        }
                    }
                }

                foreach (var shade in skiaEffects)
                {
                    shade.Attach(control);
                }

                if (newvalue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged -= control.EffectsCollectionChanged;
                    newCollection.CollectionChanged += control.EffectsCollectionChanged;
                }

                control.OnVisualEffectsChanged();
            }
        }

        protected void AttachEffects()
        {
            foreach (var content in this.VisualEffects)
            {
                content.Attach(this);
            }
        }

        private void EffectsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (SkiaEffect newItem in e.NewItems)
                    {
                        newItem.Attach(this);
                    }

                    break;

                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Remove:
                    foreach (SkiaEffect oldItem in e.OldItems ?? new SkiaEffect[0])
                    {
                        oldItem.Dettach();
                    }

                    break;
            }

            OnVisualEffectsChanged();
        }

        protected virtual void OnVisualEffectsChanged()
        {
            if (VisualEffects != null)
            {
                EffectsGestureProcessors = VisualEffects.OfType<ISkiaGestureProcessor>().ToList();
                EffectColorFilter = VisualEffects.OfType<IColorEffect>().FirstOrDefault();
                EffectImageFilter = VisualEffects.OfType<IImageEffect>().FirstOrDefault();
                EffectRenderers = VisualEffects.OfType<IRenderEffect>().ToList();
                EffectsState = VisualEffects.OfType<IStateEffect>().ToList();
                EffectPostRenderers = VisualEffects.OfType<IPostRendererEffect>().ToList();
            }

            InvalidateEffectsMargin();
            Update();
        }

        private Thickness _effectsMarginPixels = Thickness.Zero;
        private bool _effectsMarginValid;
        private float _effectsMarginScale = -1;

        /// <summary>
        /// Marks the cached effects margin as stale. Called when effects or their parameters change.
        /// Also invalidates the rendering cache because the cache surface bounds depend on the margin.
        /// </summary>
        internal void InvalidateEffectsMargin()
        {
            _effectsMarginValid = false;
            InvalidateAggregatedEffectsMargin();
            InvalidateCache();
        }

        private Thickness _aggregatedMarginPixels = Thickness.Zero;
        private bool _aggregatedMarginValid;
        private float _aggregatedMarginScale = -1;

        /// <summary>
        /// Own <see cref="EffectsMarginPixels"/> UNION the aggregated margins of all children,
        /// recursive and cached. A cached ancestor sizes its surface/clip/dirty region with this,
        /// so a shadow or glow on a descendant survives every cache boundary above it.
        /// Position-agnostic by design: a child's overflow expands every side of this control —
        /// costs a few extra pixels of cache surface, avoids re-aggregating on every layout change.
        /// </summary>
        public Thickness AggregatedEffectsMarginPixels
        {
            get
            {
                var scale = RenderingScale;
                if (!_aggregatedMarginValid || _aggregatedMarginScale != scale)
                {
                    var own = EffectsMarginPixels;
                    double l = own.Left, t = own.Top, r = own.Right, b = own.Bottom;

                    var children = Views;
                    for (int i = 0; i < children.Count; i++)
                    {
                        var m = children[i].AggregatedEffectsMarginPixels;
                        if (m.Left > l) l = m.Left;
                        if (m.Top > t) t = m.Top;
                        if (m.Right > r) r = m.Right;
                        if (m.Bottom > b) b = m.Bottom;
                    }

                    _aggregatedMarginPixels = new Thickness(l, t, r, b);
                    _aggregatedMarginScale = scale;
                    _aggregatedMarginValid = true;
                }

                return _aggregatedMarginPixels;
            }
        }

        /// <summary>
        /// Invalidates the aggregated margin cache on this control and every ancestor,
        /// so cache surfaces above a changed shadow/effect re-expand on the next draw.
        /// Called when own effects change and when children are added/removed.
        /// </summary>
        internal void InvalidateAggregatedEffectsMargin()
        {
            var control = this;
            while (control != null)
            {
                control._aggregatedMarginValid = false;
                control = control.Parent as SkiaControl;
            }
        }

        /// <summary>
        /// Aggregated extra space in PIXELS that all attached VisualEffects paint beyond DrawingRect
        /// (drop shadows, glow). Cached; recomputed only when effects or rendering scale change.
        /// </summary>
        public Thickness EffectsMarginPixels
        {
            get
            {
                var scale = RenderingScale;
                if (!_effectsMarginValid || _effectsMarginScale != scale)
                {
                    _effectsMarginPixels = ComputeEffectsMargin(scale);
                    _effectsMarginScale = scale;
                    _effectsMarginValid = true;
                }

                return _effectsMarginPixels;
            }
        }

        protected virtual Thickness ComputeEffectsMargin(float scale)
        {
            double l = 0, t = 0, r = 0, b = 0;

            if (!DisableEffects && VisualEffects is { Count: > 0 } effects)
            {
                foreach (var effect in effects)
                {
                    var m = effect.GetEffectMargin(scale);
                    if (m.Left > l) l = m.Left;
                    if (m.Top > t) t = m.Top;
                    if (m.Right > r) r = m.Right;
                    if (m.Bottom > b) b = m.Bottom;
                }
            }

            // MAUI Shadow (PlatformShadow) paints beyond DrawingRect the same way effects do
            MergeShadowMargin(ref l, ref t, ref r, ref b, PlatformShadow, scale);

            if (l == 0 && t == 0 && r == 0 && b == 0)
                return Thickness.Zero;

            return new Thickness(l, t, r, b);
        }

        /// <summary>
        /// Expands per-side margins (PIXELS) to cover a legacy <see cref="SkiaShadow"/>.
        /// Mirrors CreateShadow filter math: sigma = Blur * scale, offsets scaled to pixels;
        /// 3 * sigma bounds the gaussian (same rule DropShadowEffect uses).
        /// </summary>
        protected static void MergeShadowMargin(ref double left, ref double top, ref double right, ref double bottom,
            SkiaShadow shadow, float scale)
        {
            if (shadow == null)
                return;

            var spread = 3.0 * shadow.Blur * scale;
            var dx = shadow.X * scale;
            var dy = shadow.Y * scale;

            if (spread - dx > left) left = spread - dx;
            if (spread - dy > top) top = spread - dy;
            if (spread + dx > right) right = spread + dx;
            if (spread + dy > bottom) bottom = spread + dy;
        }

        /// <summary>
        /// Total expansion in PIXELS applied to the cache surface, the clip and the dirty region:
        /// the per-side maximum of the manual ExpandDirtyRegion (scaled to pixels) and the auto
        /// effects margin. Returns Thickness.Zero when nothing expands beyond bounds.
        /// </summary>
        protected Thickness GetRenderingExpandPixels()
        {
            var fx = AggregatedEffectsMarginPixels;
            var expand = ExpandDirtyRegion;
            if (expand == Thickness.Zero)
                return fx;

            var scale = RenderingScale;
            return new Thickness(
                Math.Max(fx.Left, expand.Left * scale),
                Math.Max(fx.Top, expand.Top * scale),
                Math.Max(fx.Right, expand.Right * scale),
                Math.Max(fx.Bottom, expand.Bottom * scale));
        }

        protected List<ISkiaGestureProcessor> EffectsGestureProcessors = new();
        protected List<IStateEffect> EffectsState = new();
        protected List<IRenderEffect> EffectRenderers = new();
        protected IImageEffect EffectImageFilter;
        protected IColorEffect EffectColorFilter;
        public List<IPostRendererEffect> EffectPostRenderers = new();

        public static readonly BindableProperty VisualEffectsProperty = BindableProperty.Create(
            nameof(VisualEffects),
            typeof(IList<SkiaEffect>),
            typeof(SkiaControl),
            defaultValueCreator: (instance) =>
            {
                var created = new ObservableAttachedItemsCollection<SkiaEffect>();
                created.CollectionChanged += ((SkiaControl)instance).EffectsCollectionChanged;
                return created;
            },
            validateValue: (bo, v) => v is IList<SkiaEffect>,
            propertyChanged: EffectsPropertyChanged,
            coerceValue: CoerceVisualEffects);



        public IList<SkiaEffect> VisualEffects
        {
            get => (IList<SkiaEffect>)GetValue(VisualEffectsProperty);
            set => SetValue(VisualEffectsProperty, value);
        }

        private static object CoerceVisualEffects(BindableObject bindable, object value)
        {
            if (!(value is ReadOnlyCollection<SkiaEffect> readonlyCollection))
            {
                return value;
            }
            return new ReadOnlyCollection<SkiaEffect>(
                readonlyCollection.ToList());
        }

        public static readonly BindableProperty DisableEffectsProperty = BindableProperty.Create(nameof(DisableEffects),
            typeof(bool),
            typeof(SkiaControl),
            false, propertyChanged: NeedDraw);
        public bool DisableEffects
        {
            get { return (bool)GetValue(DisableEffectsProperty); }
            set { SetValue(DisableEffectsProperty, value); }
        }

        #endregion







    }
}
