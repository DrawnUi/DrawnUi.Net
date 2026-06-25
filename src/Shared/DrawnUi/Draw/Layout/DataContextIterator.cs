using System.Collections;

namespace DrawnUi.Draw
{
    public class DataContextIterator : IEnumerator<SkiaControl>
    {
        private readonly ViewsIterator _viewsProvider;
        private int _currentIndex;
        private SkiaControl _view;
        private IEnumerator<SkiaControl> _viewEnumerator;
        private readonly LayoutType? _layoutType;

        // Captured once at enumeration start: an immutable snapshot stays stable for the whole pass even
        // if the UI thread swaps in a newer one mid-enumeration (issue #300).
        private readonly IList _dataContexts;

        //added this to use more that 1 view at a time
        private readonly Queue<SkiaControl> _viewsInUse;

        int GetSizeKey(SkiaControl view)
        {
            if (_layoutType.HasValue)
            {
                int hKey = 0;
                if (_layoutType == LayoutType.Column)
                {
                    hKey = (int)Math.Round(view.MeasuredSize.Pixels.Height);
                }
                else if (_layoutType == LayoutType.Row)
                {
                    hKey = (int)Math.Round(view.MeasuredSize.Pixels.Width);
                }

                return hKey;
            }

            return 0;
        }

        public DataContextIterator(ViewsIterator viewsProvider, LayoutType? layoutType)
        {
            _layoutType = layoutType;
            _viewsProvider = viewsProvider;
            _dataContexts = viewsProvider.DataContexts;
            _currentIndex = -1;
            _viewsInUse = new Queue<SkiaControl>();

            if (!_viewsProvider.IsTemplated)
            {
                _viewEnumerator = _viewsProvider.Views.GetEnumerator();
            }

            _layoutType = layoutType;
        }

        public SkiaControl Current => _view;

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_viewsProvider.IsTemplated)
            {
                // Dequeue and return the oldest view if we're at capacity.
                if (_viewsInUse.Count >= _viewsProvider.TemplatedViewsPool.MaxSize)
                {
                    var oldestView = _viewsInUse.Dequeue();

                    _viewsProvider.TemplatedViewsPool.Return(oldestView, GetSizeKey(oldestView));
                }

                _currentIndex++;

                if (_currentIndex < _dataContexts.Count)
                {
                    _view = _viewsProvider.TemplatedViewsPool.Get();

                    if (_view == null)
                        return false;

                    _viewsInUse.Enqueue(_view); // Keep track of the views in use.

                    var ctx = _dataContexts[_currentIndex];
                    if (ctx != _view.BindingContext && _view.BindingContext != null)
                    {
                        var stop = 1;
                    }

                    _view.ContextIndex = _currentIndex;
                    _view.BindingContext = ctx;
                    return true;
                }

                return false;
            }

            bool hasMore = _viewEnumerator.MoveNext();
            if (hasMore)
            {
                _view = _viewEnumerator.Current;
            }

            return hasMore;
        }

        public void Reset()
        {
            if (_viewsProvider.IsTemplated)
            {
                if (_currentIndex >= 0 && _currentIndex < _dataContexts.Count)
                {
                    _viewsProvider.TemplatedViewsPool.Return(_view, GetSizeKey(_view));
                }

                _currentIndex = -1;
            }
            else
            {
                _viewEnumerator.Reset();
            }
        }

        public void Dispose()
        {
            if (_viewsProvider.IsTemplated && _currentIndex >= 0 && _currentIndex < _dataContexts.Count)
            {
                _viewsProvider.TemplatedViewsPool.Return(_view, GetSizeKey(_view));
            }
        }
    }
}