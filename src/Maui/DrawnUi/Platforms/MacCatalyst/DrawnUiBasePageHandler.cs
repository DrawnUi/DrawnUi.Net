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
                    if (mapped != InputKey.Tab) // Tab comes through the key commands below
                        FindKeyboardCanvas()?.HandleKeyboardNavigation(mapped, KeyboardManager.IsShiftPressed);

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

        // Tab never reaches PressesBegan either: the system focus navigation takes it unless a key command asks for
        // priority. Tab and Shift+Tab walk the drawn accessibility nodes, as on MAUI Windows and WPF.
        private UIKeyCommand[] _tabCommands;

        /// <summary>
        /// Tab and Shift+Tab, ahead of the system focus navigation, when the page tracks the keyboard.
        /// </summary>
        public override UIKeyCommand[] KeyCommands
        {
            get
            {
                if (!TracksKeyboard || !OperatingSystem.IsMacCatalystVersionAtLeast(15))
                    return base.KeyCommands;

                if (_tabCommands == null)
                {
                    var forward = UIKeyCommand.Create((NSString)"\t", 0, new ObjCRuntime.Selector("drawnUiTab:"));
                    var backward = UIKeyCommand.Create((NSString)"\t", UIKeyModifierFlags.Shift,
                        new ObjCRuntime.Selector("drawnUiShiftTab:"));
                    forward.WantsPriorityOverSystemBehavior = true;
                    backward.WantsPriorityOverSystemBehavior = true;
                    _tabCommands = base.KeyCommands is { Length: > 0 } own ? [..own, forward, backward] : [forward, backward];
                }

                return _tabCommands;
            }
        }

        [Export("drawnUiTab:")]
        void OnTabCommand(UIKeyCommand command) => OnTab(false);

        [Export("drawnUiShiftTab:")]
        void OnShiftTabCommand(UIKeyCommand command) => OnTab(true);

        void OnTab(bool shift)
        {
            KeyboardManager.KeyboardPressed(InputKey.Tab); // the app still gets Tab, as on the other platforms
            KeyboardManager.KeyboardReleased(InputKey.Tab);
            FindKeyboardCanvas()?.HandleKeyboardNavigation(InputKey.Tab, shift);
        }

        /// <summary>
        /// The canvas keyboard navigation works on: the one holding keyboard focus, else the one holding a focused node,
        /// else the first visible canvas of the page. The walk stops at each canvas, it never enters the drawn tree.
        /// </summary>
        DrawnView FindKeyboardCanvas()
        {
            DrawnView withFocus = null, first = null;
            Collect(_page as Microsoft.Maui.IVisualTreeElement);
            return withFocus ?? first;

            bool Collect(Microsoft.Maui.IVisualTreeElement element)
            {
                if (element is DrawnView canvas)
                {
                    if (!canvas.IsVisible)
                        return false;
                    if (canvas.KeyboardFocusNode != null)
                    {
                        withFocus = canvas;
                        return true;
                    }
                    if (canvas.AccessibilityManager.FocusedNode != null)
                        withFocus ??= canvas;
                    first ??= canvas;
                    return false;
                }

                if (element == null)
                    return false;
                foreach (var child in element.GetVisualChildren())
                {
                    if (Collect(child))
                        return true;
                }
                return false;
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

        private readonly IView _page;

        public CustomView(IView page, IMauiContext mauiContext) : base(page, mauiContext)
        {
            _page = page;
        }
    }
}
