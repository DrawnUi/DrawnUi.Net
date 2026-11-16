namespace DrawnUi.Views
{
    public partial class DrawnView
    {
        /// <summary>
        /// Points of this canvas, measured up from its bottom edge, that the on-screen keyboard covers (0 when none):
        /// where this canvas is on screen against <see cref="Super.KeyboardTop"/>.
        /// </summary>
        public double KeyboardCovered { get; private set; }

        void WatchKeyboard(bool watch)
        {
            Super.KeyboardChanged -= OnKeyboardChanged;
            FocusedItemChanged -= OnFocusedItemChangedForKeyboard;
            if (!watch)
                return;
            Super.KeyboardChanged += OnKeyboardChanged;
            FocusedItemChanged += OnFocusedItemChangedForKeyboard;
        }

        void OnKeyboardChanged(object sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(ApplyKeyboard);

        void OnFocusedItemChangedForKeyboard(object sender, FocusedItemChangedArgs e)
        {
            if (!double.IsNaN(Super.KeyboardTop))
                MainThread.BeginInvokeOnMainThread(ApplyKeyboard);
        }

        /// <summary>
        /// Measures the covered part and, when the focused editor sits in a scroll with
        /// <see cref="SkiaScroll.AdaptToKeyboardAuto"/>, hands that scroll the field and the covered height.
        /// </summary>
        void ApplyKeyboard()
        {
            var top = Super.KeyboardTop;
            double covered = 0;
            if (!double.IsNaN(top))
            {
                var bottom = GetBottomOnScreen();
                if (!double.IsNaN(bottom))
                    covered = Math.Max(0, bottom - top);
            }
            KeyboardCovered = covered;

            SkiaScroll scroll = null;
            if (covered > 0 && FocusedChild is SkiaEditor { IsFocused: true } editor)
            {
                scroll = FindKeyboardScroll(editor);
                if (scroll != null)
                {
                    scroll.AdaptToKeyboardFor = editor;
                    scroll.AdaptToKeyboardSize = covered;
                }
            }

            // keyboard up, no focused field (between two fields): the adapted scroll keeps its room
            if (covered > 0 && scroll == null)
                return;

            // the keyboard closed, or the field moved to another scroll: the scroll this canvas adapted lets go
            if (_keyboardScroll != null && _keyboardScroll != scroll)
            {
                _keyboardScroll.AdaptToKeyboardFor = null;
                _keyboardScroll.AdaptToKeyboardSize = 0;
            }
            _keyboardScroll = scroll;
        }

        SkiaScroll _keyboardScroll;

        static SkiaScroll FindKeyboardScroll(SkiaControl control)
        {
            var parent = control.Parent;
            while (parent is SkiaControl p)
            {
                if (p is SkiaScroll scroll && scroll.AdaptToKeyboardAuto)
                    return scroll;
                parent = p.Parent;
            }
            return null;
        }

        /// <summary>Bottom edge of this canvas on screen, in points; NaN when it is not on screen.</summary>
        double GetBottomOnScreen()
        {
#if ANDROID
            if (Handler?.PlatformView is Android.Views.View view && view.Height > 0)
            {
                var loc = new int[2];
                view.GetLocationOnScreen(loc);
                var density = view.Resources?.DisplayMetrics?.Density ?? 1f;
                return (loc[1] + view.Height) / density;
            }
#elif IOS
            if (Handler?.PlatformView is UIKit.UIView view && view.Window != null)
            {
                var r = view.ConvertRectToView(view.Bounds, null);
                return (double)(r.Y + r.Height);
            }
#endif
            return double.NaN;
        }
    }
}
