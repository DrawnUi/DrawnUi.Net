namespace DrawnUi.Draw;

public partial class Super
{
#if ANDROID
    static Android.Views.View _keyboardDecor;

    /// <summary>
    /// Follows the activity window's IME insets: the keyboard's top edge in points, NaN when it is hidden.
    /// </summary>
    static void TrackKeyboard(Android.App.Activity activity)
    {
        var decor = activity?.Window?.DecorView;
        if (decor == null || ReferenceEquals(decor, _keyboardDecor))
            return;
        _keyboardDecor = decor;
        decor.ViewTreeObserver.GlobalLayout += (s, e) => UpdateKeyboardFromWindow();
    }

    internal static void UpdateKeyboardFromWindow()
    {
        var decor = _keyboardDecor;
        if (decor == null)
            return;
        double top = double.NaN;
        var insets = AndroidX.Core.View.ViewCompat.GetRootWindowInsets(decor);
        if (insets != null)
        {
            var ime = insets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.Ime()).Bottom;
            if (ime > 0)
            {
                var loc = new int[2];
                decor.GetLocationOnScreen(loc);
                var density = decor.Resources?.DisplayMetrics?.Density ?? 1f;
                top = (loc[1] + decor.Height - ime) / density;
            }
        }
        SetKeyboardTop(top);
    }
#endif

#if IOS
    static Foundation.NSObject _keyboardFrameObserver, _keyboardHideObserver;

    /// <summary>
    /// Follows the system keyboard notifications: the keyboard's top edge in points, NaN when it is hidden.
    /// </summary>
    static void TrackKeyboard()
    {
        if (_keyboardFrameObserver != null)
            return;
        _keyboardFrameObserver = UIKit.UIKeyboard.Notifications.ObserveWillChangeFrame((s, a) =>
        {
            var frame = a.FrameEnd;
            var screen = UIKit.UIScreen.MainScreen.Bounds.Height;
            SetKeyboardTop(frame.Y >= screen ? double.NaN : (double)frame.Y);
        });
        _keyboardHideObserver = UIKit.UIKeyboard.Notifications.ObserveWillHide((s, a) => SetKeyboardTop(double.NaN));
    }
#endif
}
