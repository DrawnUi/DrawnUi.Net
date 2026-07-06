using System.Collections;

namespace DrawnUi.Draw
{
    /// <summary>
    /// To iterate over virtual views
    /// </summary>
    public class ViewsIterator : IEnumerable<SkiaControl>, IDisposable
    {
        private TemplatedViewsPool _templatedViewsPool;
        private volatile IList _dataContexts;
        private IEnumerable<SkiaControl> _views;
        private DataContextIterator _iterator;
        private readonly LayoutType? _layoutType;

        public bool IsTemplated { get; }

        public TemplatedViewsPool TemplatedViewsPool => _templatedViewsPool;
        public IList DataContexts => _dataContexts;
        public IEnumerable<SkiaControl> Views => _views;

        public ViewsIterator(TemplatedViewsPool templatedViewsPool, IList dataContexts, LayoutType? layoutType)
        {
            _templatedViewsPool = templatedViewsPool;
            _dataContexts = dataContexts;
            _layoutType = layoutType;
            IsTemplated = true;
        }

        public void SetViews(IEnumerable<SkiaControl> views)
        {
            _views = views;
        }

        /// <summary>
        /// Swaps in the latest immutable data-contexts snapshot. Called on the UI thread when the bound
        /// collection changes so a cached per-thread iterator stops pointing at a stale snapshot.
        /// </summary>
        public void SetDataContexts(IList dataContexts)
        {
            _dataContexts = dataContexts;
        }

        public ViewsIterator(IEnumerable<SkiaControl> views)
        {
            _views = views;
            IsTemplated = false;
        }

        public IEnumerator<SkiaControl> GetEnumerator()
        {
            _iterator = new DataContextIterator(this, _layoutType);
            return _iterator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            if (_iterator != null)
            {
                _iterator.Dispose();
                _iterator = null;
            }
        }
    }
}