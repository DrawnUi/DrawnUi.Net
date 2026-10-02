using Foundation;
using Microsoft.Maui.Platform;
using UIKit;
using ContentView = Microsoft.Maui.Platform.ContentView;

namespace DrawnUi.Controls;

public class DrawnUiBasePageHandler : Microsoft.Maui.Handlers.PageHandler
{

    protected override ContentView CreatePlatformView()
    {
        //return base.CreatePlatformView();;
        if (ViewController == null)
            ViewController = new CustomView(VirtualView, MauiContext);

        if (ViewController is PageViewController pc && pc.CurrentPlatformView is ContentView pv)
            return pv;

        if (ViewController.View is ContentView cv)
            return cv;

        throw new InvalidOperationException($"PageViewController.View must be a {nameof(ContentView)}");
    }

    public class CustomView : PageViewController
    {
        public bool TracksKeyboard => DrawnExtensions.StartupSettings != null &&
                                      DrawnExtensions.StartupSettings.UseDesktopKeyboard;

        public override bool CanBecomeFirstResponder
        {
            get
            {
                if (TracksKeyboard)
                    return true;
                return base.CanBecomeFirstResponder;
            }
        }

        public override void DidUpdateFocus(UIFocusUpdateContext context, UIFocusAnimationCoordinator coordinator)
        {
            base.DidUpdateFocus(context, coordinator);

            if (TracksKeyboard && context.NextFocusedItem == null) Super.RequestMainResponder(this);
        }

        public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
        {
            if (TracksKeyboard)
            {
                var consumed = false;
                foreach (UIPress press in presses)
                {
                    var mapped = KeyboardManager.MapToMaui((int)press.Type);
                    KeyboardManager.KeyboardPressed(mapped);

                    consumed = true;
                }

                if (consumed) return;
            }

            base.PressesBegan(presses, evt);
        }

        public override void PressesEnded(NSSet<UIPress> presses, UIPressesEvent evt)
        {
            base.PressesEnded(presses, evt);

            if (TracksKeyboard)
                foreach (UIPress press in presses)
                {
                    var mapped = KeyboardManager.MapToMaui((int)press.Type);
                    KeyboardManager.KeyboardReleased(mapped);
                    //Trace.WriteLine($"[KEY] {press.Type}/{(int)press.Type} => {mapped}");
                }
        }

        // Cmd+C and Cmd+A never reach PressesBegan on a Mac: the Edit menu takes them and sends copy: / selectAll:
        // down the responder chain. They go on to KeyboardManager as the key combination the other platforms send.

        /// <summary>
        /// Enables the Edit menu's Copy and Select All (Cmd+C, Cmd+A) when the page tracks the keyboard.
        /// </summary>
        public override bool CanPerform(ObjCRuntime.Selector action, NSObject withSender)
        {
            if (TracksKeyboard && (action.Name == "copy:" || action.Name == "selectAll:"))
                return true;
            return base.CanPerform(action, withSender);
        }

        /// <summary>
        /// Cmd+C: sent to <see cref="KeyboardManager"/> as Meta+C.
        /// </summary>
        public override void Copy(NSObject sender)
        {
            if (TracksKeyboard)
                SendCommandShortcut(InputKey.KeyC);
            else
                base.Copy(sender);
        }

        /// <summary>
        /// Cmd+A: sent to <see cref="KeyboardManager"/> as Meta+A.
        /// </summary>
        public override void SelectAll(NSObject sender)
        {
            if (TracksKeyboard)
                SendCommandShortcut(InputKey.KeyA);
            else
                base.SelectAll(sender);
        }

        static void SendCommandShortcut(InputKey key)
        {
            KeyboardManager.KeyboardPressed(InputKey.MetaLeft);
            KeyboardManager.KeyboardPressed(key);
            KeyboardManager.KeyboardReleased(key);
            KeyboardManager.KeyboardReleased(InputKey.MetaLeft);
        }

        public CustomView(IView page, IMauiContext mauiContext) : base(page, mauiContext)
        { }
    }
}
