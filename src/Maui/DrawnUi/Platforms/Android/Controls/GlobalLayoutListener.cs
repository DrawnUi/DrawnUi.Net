using Android.Views;

namespace DrawnUi.Controls
{
    public class GlobalLayoutListener<T> : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
    {
        public T Control;

        public global::Android.Views.View View;

        public GlobalLayoutListener(global::Android.Views.View view, T control)
        {
            if (control == null)
            {
                throw new InvalidOperationException("Control cannot be null");
            }
            View = view;
            Control = control;
            _observer = View.ViewTreeObserver;
            _observer?.AddOnGlobalLayoutListener(this);
        }

        // The observer we registered with: once the view is detached from its window, View.ViewTreeObserver
        // returns a new floating observer, removing from that one is a no-op and the window keeps the listener
        // (and everything it references) alive forever.
        ViewTreeObserver _observer;

        public void Release()
        {
            if (_observer != null && _observer.IsAlive)
            {
                _observer.RemoveOnGlobalLayoutListener(this);
            }
            _observer = null;
            View?.ViewTreeObserver?.RemoveOnGlobalLayoutListener(this);
            View = null;
            Control = default;
        }

        public virtual void OnGlobalLayout()
        {
 
        }
    }
}
