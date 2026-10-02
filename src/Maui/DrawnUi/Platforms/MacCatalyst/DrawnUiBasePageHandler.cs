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
                    consumed = true;
                    if (mapped == InputKey.Tab)
                    {
                        Console.WriteLine("[A11yTab] PressesBegan Tab"); //todo TEMP remove
                        var shift = press.Key?.ModifierFlags.HasFlag(UIKeyModifierFlags.Shift) ?? KeyboardManager.IsShiftPressed;
                        if (TakeTab())
                        {
                            KeyboardManager.KeyboardPressed(mapped);
                            NavigateTab(shift);
                        }
                        continue;
                    }

                    KeyboardManager.KeyboardPressed(mapped);
                    FindKeyboardCanvas()?.HandleKeyboardNavigation(mapped, KeyboardManager.IsShiftPressed);
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

        // Tab and Shift+Tab walk the drawn accessibility nodes, as on MAUI Windows and WPF. With Full Keyboard Access on,
        // the system focus navigation takes Tab before PressesBegan unless a key command asks for priority; with it off,
        // Tab reaches PressesBegan. Both paths are taken, one Tab is used once.
        private UIKeyCommand[] _tabCommands;
        private long _lastTabMs = -1000;

        /// <summary>
        /// True for the first delivery of a Tab press: the key command and PressesBegan can both deliver the same one,
        /// within the same run loop pass, while key repeat comes 30 ms apart at the fastest.
        /// </summary>
        bool TakeTab()
        {
            var now = Environment.TickCount64;
            if (now - _lastTabMs < 15)
                return false;
            _lastTabMs = now;
            return true;
        }

        void NavigateTab(bool shift)
        {
            var canvas = FindKeyboardCanvas();
            var used = canvas?.HandleKeyboardNavigation(InputKey.Tab, shift);
            Console.WriteLine($"[A11yTab] canvas={(canvas == null ? "NULL" : "ok")} used={used} focus={canvas?.KeyboardFocusNode?.AccessibilityLabel}"); //todo TEMP remove
        }

        /// <summary>
        /// Tab and Shift+Tab, ahead of the system focus navigation, when the page tracks the keyboard.
        /// </summary>
        public override UIKeyCommand[] KeyCommands
        {
            get
            {
                Console.WriteLine("[A11yTab] KeyCommands queried"); //todo TEMP remove
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
            Console.WriteLine("[A11yTab] key command"); //todo TEMP remove
            if (!TakeTab())
                return;
            KeyboardManager.KeyboardPressed(InputKey.Tab); // the app still gets Tab, as on the other platforms
            KeyboardManager.KeyboardReleased(InputKey.Tab);
            NavigateTab(shift);
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
