using System.Globalization;
using CoreGraphics;
using DrawnUi.Draw;
using Foundation;
using UIKit;

namespace DrawnUi.Views
{
    /// <summary>
    /// VoiceOver (iOS, Mac Catalyst): the canvas view is an accessibility container holding one element per node of the
    /// accessibility snapshot (<see cref="SkiaAccessibilityManager"/>, the same one TalkBack and UI Automation get).
    /// Swipes walk the nodes in reading order, double tap activates (a tap at the node's center), swipe up / down adjusts
    /// a slider, three-finger swipes page the scroll around the node. On iOS the elements exist only while VoiceOver or
    /// Switch Control runs, so the canvas costs nothing otherwise.
    /// </summary>
    public partial class DrawnView
    {
        private static readonly NSString AccessibilityElementsKey = new("accessibilityElements");

        private UIView _a11yView;
        private Dictionary<int, DrawnUiAccessibilityElement> _a11yElements = new();
        private NSObject _a11yStatusObserver;
        private System.Threading.Timer _a11yRefresh;
        private volatile bool _a11yOn;

        partial void OnCanvasViewChangedPlatform()
        {
            TryWireAccessibility();
        }

        private void TryWireAccessibility()
        {
            if (CanvasView is not Microsoft.Maui.Controls.View canvasView)
            {
                ReleaseAccessibility();
                return;
            }

            if (canvasView.Handler?.PlatformView is UIView view)
            {
                WireAccessibility(view);
                return;
            }

            // the canvas gets its handler later: wire it then
            void OnHandlerChanged(object sender, EventArgs e)
            {
                canvasView.HandlerChanged -= OnHandlerChanged;
                if (ReferenceEquals(CanvasView, canvasView) && canvasView.Handler?.PlatformView is UIView platformView)
                    WireAccessibility(platformView);
            }

            canvasView.HandlerChanged += OnHandlerChanged;
        }

        private void WireAccessibility(UIView view)
        {
            if (ReferenceEquals(view, _a11yView))
                return;

            ReleaseAccessibility();

            _a11yView = view;
            view.IsAccessibilityElement = false; // a container: VoiceOver reads its elements, not the view
            _a11yStatusObserver = UIView.Notifications.ObserveVoiceOverStatusDidChange((_, _) => OnA11yStatusChanged());

            AccessibilityManager.Changed += OnA11ySnapshotChanged;
            AccessibilityManager.RebuildSkipped += OnA11yRebuildSkipped;
            AccessibilityManager.LiveRegionUpdated += OnA11yLiveRegionUpdated;
            AccessibilityManager.ReaderRefocusRequested += OnA11yReaderRefocus;

            OnA11yStatusChanged();
        }

        private void ReleaseAccessibility()
        {
            AccessibilityManager.Changed -= OnA11ySnapshotChanged;
            AccessibilityManager.RebuildSkipped -= OnA11yRebuildSkipped;
            AccessibilityManager.LiveRegionUpdated -= OnA11yLiveRegionUpdated;
            AccessibilityManager.ReaderRefocusRequested -= OnA11yReaderRefocus;
            _a11yRefresh?.Dispose();
            _a11yRefresh = null;
            _a11yStatusObserver?.Dispose();
            _a11yStatusObserver = null;
            _a11yOn = false;

            var view = _a11yView;
            _a11yView = null;
            foreach (var element in _a11yElements.Values)
                element.Node = null;
            _a11yElements = new();
            // an empty array, never null: the binding's SetValueForKey throws ArgumentNullException for a null value,
            // which crashed every canvas teardown (GitHub #361)
            if (view != null && view.Handle != IntPtr.Zero)
                view.SetValueForKey(new NSArray(), AccessibilityElementsKey);
        }

        // UI thread. Mac Catalyst: always on (the Mac's VoiceOver, Full Keyboard Access and Voice Control read the elements too)
        private void OnA11yStatusChanged()
        {
            _a11yOn = OperatingSystem.IsMacCatalyst() || UIAccessibility.IsVoiceOverRunning || UIAccessibility.IsSwitchControlRunning;
            if (_a11yOn)
                UpdateA11yElements();
        }

        /// <summary>
        /// UI thread: the elements follow the snapshot. An element keeps its node id across rebuilds, so VoiceOver keeps its
        /// place; an element whose node left gets no node and does nothing if VoiceOver still holds it.
        /// </summary>
        private void UpdateA11yElements()
        {
            var view = _a11yView;
            if (view == null)
                return;

            var snapshot = AccessibilityManager.Snapshot;
            var next = new Dictionary<int, DrawnUiAccessibilityElement>(snapshot.Length);
            var list = new NSObject[snapshot.Length];
            for (var i = 0; i < snapshot.Length; i++)
            {
                var node = snapshot[i];
                if (!_a11yElements.TryGetValue(node.Id, out var element))
                    element = new DrawnUiAccessibilityElement(view, AccessibilityManager);
                element.Update(node);
                next[node.Id] = element;
                list[i] = element;
            }

            foreach (var (id, element) in _a11yElements)
            {
                if (!next.ContainsKey(id))
                    element.Node = null;
            }

            _a11yElements = next;
            view.SetValueForKey(NSArray.FromNSObjects(list), AccessibilityElementsKey);
        }

        // rendering thread: VoiceOver learns the new nodes on the UI thread
        private void OnA11ySnapshotChanged()
        {
            if (!_a11yOn)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_a11yView == null)
                    return;
                UpdateA11yElements();
                UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, null);
            });
        }

        // VoiceOver's node left with its page: move VoiceOver to the first node that says something. Raised after Changed,
        // so the element exists by then. LayoutChanged, not ScreenChanged: that one is for a real screen swap.
        private void OnA11yReaderRefocus(AccessibilityNode next)
        {
            if (!_a11yOn)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_a11yView != null && _a11yElements.TryGetValue(next.Id, out var element))
                    UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, element);
            });
        }

        // a page that just opened may draw no more frames: rebuild the skipped snapshot once the rate limit allows
        private void OnA11yRebuildSkipped(long remainingMs)
        {
            if (!_a11yOn)
                return;

            var due = Math.Max(1, remainingMs) + 16;
            if (_a11yRefresh == null)
                _a11yRefresh = new System.Threading.Timer(_ => AccessibilityManager.RefreshIfStale(RenderingScale), null, due, System.Threading.Timeout.Infinite);
            else
                _a11yRefresh.Change(due, System.Threading.Timeout.Infinite);
        }

        // a live region changed: VoiceOver says its new text at once
        private void OnA11yLiveRegionUpdated(ISkiaAccessibilityNode node)
        {
            if (!_a11yOn)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var text = node.AccessibilityLabel;
                var value = node.GetAccessibilityValue()?.Text;
                if (!string.IsNullOrEmpty(value))
                    text = string.IsNullOrEmpty(text) ? value : $"{text}, {value}";
                if (_a11yView != null && !string.IsNullOrEmpty(text))
                    UIAccessibility.PostNotification(UIAccessibilityPostNotification.Announcement, new NSString(text));
            });
        }
    }

    /// <summary>
    /// One snapshot node for VoiceOver. Label and value are read from the control when VoiceOver asks (the snapshot is
    /// rebuilt at most once a second), so a slider says its new value right after an adjust.
    /// </summary>
    internal sealed class DrawnUiAccessibilityElement : UIAccessibilityElement
    {
        // VoiceOver reads a toggle's "1" / "0" as on / off with it (iOS 17+); older systems get the button trait only
        private static readonly ulong ToggleTrait = OperatingSystem.IsIOSVersionAtLeast(17) || OperatingSystem.IsMacCatalystVersionAtLeast(17)
            ? UIAccessibilityTraits.ToggleButton.GetConstantValue() ?? 0
            : 0;

        private readonly SkiaAccessibilityManager _manager;

        public DrawnUiAccessibilityElement(UIView container, SkiaAccessibilityManager manager) : base(container)
        {
            _manager = manager;
            IsAccessibilityElement = true;
        }

        public AccessibilityNode Node;

        private ISkiaAccessibilityNode Source => Node?.Source;

        public void Update(AccessibilityNode node)
        {
            Node = node;
            var r = node.Rect; // units = points
            AccessibilityFrameInContainerSpace = new CGRect(r.Left, r.Top, r.Width, r.Height);
            AccessibilityHint = string.IsNullOrEmpty(node.Hint) ? null : node.Hint;
            AccessibilityTraits = Traits(node);
        }

        // none when a title text inside says it
        public override string AccessibilityLabel
        {
            get
            {
                var node = Node;
                if (node == null || node.NamedByChild)
                    return null;
                return node.Source?.AccessibilityLabel ?? node.Label;
            }
            set { }
        }

        // a toggle's state ("1" / "0" with the toggle trait), else a range control's spoken text or its number
        public override string AccessibilityValue
        {
            get
            {
                var node = Node;
                if (node == null)
                    return null;

                var pressed = node.Source?.AccessibilityIsPressed ?? node.IsPressed;
                if (pressed.HasValue)
                    return pressed.Value ? "1" : "0";

                if (node.Value is { } snapshotValue)
                {
                    var value = node.Source?.GetAccessibilityValue() ?? snapshotValue;
                    return string.IsNullOrEmpty(value.Text) ? value.Now.ToString("0.###", CultureInfo.CurrentCulture) : value.Text;
                }

                return null;
            }
            set { }
        }

        // double tap: the node's activation (a tap at its center), as UIA Invoke and TalkBack's click
        [Export("accessibilityActivate")]
        public bool Activate() => SkiaAccessibilityManager.Activate(Source);

        // swipe up / down on an adjustable node: one step, the arrow key's path
        public override void AccessibilityIncrement()
        {
            if (Adjustable)
                SkiaAccessibilityManager.Adjust(Source, true);
        }

        public override void AccessibilityDecrement()
        {
            if (Adjustable)
                SkiaAccessibilityManager.Adjust(Source, false);
        }

        private bool Adjustable => Node is { CanInteract: true, Value.Step: > 0 };

        // Three-finger swipes page the nearest scroll around the node that can move that way; NO when none can, so
        // VoiceOver says there is nothing more. The direction is where the scroll bar moves: Down shows what is below.
        public override bool AccessibilityScroll(UIAccessibilityScrollDirection direction)
        {
            var source = Source;
            return direction switch
            {
                UIAccessibilityScrollDirection.Down => SkiaAccessibilityManager.Page(source, true, true),
                UIAccessibilityScrollDirection.Up => SkiaAccessibilityManager.Page(source, true, false),
                UIAccessibilityScrollDirection.Left => SkiaAccessibilityManager.Page(source, false, true),
                UIAccessibilityScrollDirection.Right => SkiaAccessibilityManager.Page(source, false, false),
                UIAccessibilityScrollDirection.Next => SkiaAccessibilityManager.Page(source, true, true) || SkiaAccessibilityManager.Page(source, false, true),
                UIAccessibilityScrollDirection.Previous => SkiaAccessibilityManager.Page(source, true, false) || SkiaAccessibilityManager.Page(source, false, false),
                _ => false,
            };
        }

        // VoiceOver moved here: this is the reader's node, and a node off screen is scrolled into view, as UIKit scroll views do
        public override void AccessibilityElementDidBecomeFocused()
        {
            var source = Source;
            if (source == null)
                return;
            _manager.NotifyReaderFocused(source);
            SkiaAccessibilityManager.ScrollIntoView(source);
        }

        public override void AccessibilityElementDidLoseFocus()
        {
            var source = Source;
            if (source != null && ReferenceEquals(_manager.ReaderNode, source))
                _manager.NotifyReaderFocused(null);
        }

        private static ulong Traits(AccessibilityNode node)
        {
            var traits = node.Role switch
            {
                "button" or "menuitem" or "tab" or "option" or "checkbox" or "menuitemcheckbox" or "switch"
                    or "radio" or "menuitemradio" => UIAccessibilityTrait.Button,
                "link" => UIAccessibilityTrait.Link,
                "img" => UIAccessibilityTrait.Image,
                "text" => UIAccessibilityTrait.StaticText,
                "heading" => UIAccessibilityTrait.Header,
                "slider" or "spinbutton" => UIAccessibilityTrait.Adjustable,
                "searchbox" => UIAccessibilityTrait.SearchField,
                _ => UIAccessibilityTrait.None,
            };

            // a control role that takes no input reads as unavailable (drawnui-cross 6c rule 2)
            if (!node.CanInteract && DrawnUi.Models.Aria.IsInteractiveRole(node.Role))
                traits |= UIAccessibilityTrait.NotEnabled;
            if (node.Role == "progressbar" || !string.IsNullOrEmpty(node.Live))
                traits |= UIAccessibilityTrait.UpdatesFrequently;

            var result = (ulong)traits;
            if (node.IsPressed.HasValue)
                result |= ToggleTrait;
            return result;
        }
    }
}
