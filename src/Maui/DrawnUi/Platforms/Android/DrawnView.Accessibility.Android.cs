using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using AndroidX.CustomView.Widget;
using DrawnUi.Draw;
using View = Android.Views.View;

namespace DrawnUi.Views
{
    /// <summary>
    /// TalkBack: the canvas view exposes every node of the accessibility snapshot (<see cref="SkiaAccessibilityManager"/>,
    /// the same one MAUI Windows gives to UI Automation) as a virtual view. Explore by touch, swipes in reading order and
    /// double tap (the node's activation, a tap at its center) work as on a native screen. Costs nothing while no
    /// accessibility service runs: Android asks the helper only then.
    /// </summary>
    public partial class DrawnView
    {
        private View _a11yView;
        private DrawnUiAccessibilityHelper _a11yHelper;
        private System.Threading.Timer _a11yRefresh;

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

            if (canvasView.Handler?.PlatformView is View view)
            {
                WireAccessibility(view);
                return;
            }

            // the canvas gets its handler later: wire it then
            void OnHandlerChanged(object sender, EventArgs e)
            {
                canvasView.HandlerChanged -= OnHandlerChanged;
                if (ReferenceEquals(CanvasView, canvasView) && canvasView.Handler?.PlatformView is View platformView)
                    WireAccessibility(platformView);
            }

            canvasView.HandlerChanged += OnHandlerChanged;
        }

        private void WireAccessibility(View view)
        {
            if (ReferenceEquals(view, _a11yView))
                return;

            ReleaseAccessibility();

            _a11yView = view;
            _a11yHelper = new DrawnUiAccessibilityHelper(view, this);
            view.ImportantForAccessibility = ImportantForAccessibility.Yes;
            ViewCompat.SetAccessibilityDelegate(view, _a11yHelper);
            view.SetOnHoverListener(_a11yHelper.HoverListener); // explore by touch arrives as hover events

            AccessibilityManager.Changed += OnA11ySnapshotChanged;
            AccessibilityManager.RebuildSkipped += OnA11yRebuildSkipped;
            AccessibilityManager.LiveRegionUpdated += OnA11yLiveRegionUpdated;
        }

        private void ReleaseAccessibility()
        {
            AccessibilityManager.Changed -= OnA11ySnapshotChanged;
            AccessibilityManager.RebuildSkipped -= OnA11yRebuildSkipped;
            AccessibilityManager.LiveRegionUpdated -= OnA11yLiveRegionUpdated;
            _a11yRefresh?.Dispose();
            _a11yRefresh = null;

            var view = _a11yView;
            _a11yView = null;
            _a11yHelper = null;
            if (view != null && view.Handle != IntPtr.Zero)
            {
                ViewCompat.SetAccessibilityDelegate(view, null);
                view.SetOnHoverListener(null);
            }
        }

        private bool IsScreenReaderOn => _a11yHelper?.IsScreenReaderOn == true;

        // rendering thread: the reader learns the new nodes on the UI thread
        private void OnA11ySnapshotChanged()
        {
            if (!IsScreenReaderOn)
                return;

            var view = _a11yView;
            view?.Post(() =>
            {
                if (!ReferenceEquals(view, _a11yView))
                    return;
                _a11yHelper?.RefocusIfDropped();
                _a11yHelper?.InvalidateRoot();
            });
        }

        // a page that just opened may draw no more frames: rebuild the skipped snapshot once the rate limit allows
        private void OnA11yRebuildSkipped(long remainingMs)
        {
            if (!IsScreenReaderOn)
                return;

            var due = Math.Max(1, remainingMs) + 16;
            if (_a11yRefresh == null)
                _a11yRefresh = new System.Threading.Timer(_ => AccessibilityManager.RefreshIfStale(RenderingScale), null, due, System.Threading.Timeout.Infinite);
            else
                _a11yRefresh.Change(due, System.Threading.Timeout.Infinite);
        }

        private void OnA11yLiveRegionUpdated(ISkiaAccessibilityNode node)
        {
            if (!IsScreenReaderOn)
                return;

            var view = _a11yView;
            view?.Post(() =>
            {
                if (ReferenceEquals(view, _a11yView))
                    _a11yHelper?.InvalidateVirtualView(node.AccessibilityId);
            });
        }
    }

    /// <summary>
    /// The snapshot nodes as virtual views of the canvas view. Ids are <see cref="ISkiaAccessibilityNode.AccessibilityId"/>,
    /// stable across rebuilds, so TalkBack keeps its place when the snapshot changes.
    /// </summary>
    internal sealed class DrawnUiAccessibilityHelper : ExploreByTouchHelper
    {
        private readonly WeakReference<DrawnView> _canvas;
        private readonly AccessibilityManager _system;

        private readonly View _host;

        public DrawnUiAccessibilityHelper(View host, DrawnView canvas) : base(host)
        {
            _host = host;
            _canvas = new WeakReference<DrawnView>(canvas);
            _system = host.Context?.GetSystemService(Context.AccessibilityService) as AccessibilityManager;
            HoverListener = new HoverForwarder(this);
        }

        public View.IOnHoverListener HoverListener { get; }

        public bool IsScreenReaderOn => _system is { IsEnabled: true, IsTouchExplorationEnabled: true };

        private AccessibilityNode[] Snapshot(bool refresh)
        {
            if (!_canvas.TryGetTarget(out var canvas))
                return [];

            var manager = canvas.AccessibilityManager;
            if (refresh)
                manager.RefreshIfStale(canvas.RenderingScale); // a reader never gets the nodes of a page that left
            return manager.Snapshot;
        }

        private float Scale => _canvas.TryGetTarget(out var canvas) ? Math.Max(canvas.RenderingScale, 1f) : 1f;

        private AccessibilityNode Find(int id)
        {
            foreach (var node in Snapshot(false))
                if (node.Id == id)
                    return node;
            return null;
        }

        /// <summary>
        /// TalkBack's node left the snapshot (its page closed, a popup gone): focus the first node that says something (a name,
        /// or takes input), so the reader's cursor does not stay on an empty spot. Only when TalkBack really was on a node.
        /// </summary>
        public void RefocusIfDropped()
        {
            var focused = AccessibilityFocusedVirtualViewId;
            if (focused == HostId || focused == InvalidId)
                return;

            var snapshot = Snapshot(false);
            if (Array.Exists(snapshot, n => n.Id == focused))
                return;

            var next = Array.Find(snapshot, n => n.CanInteract || !string.IsNullOrEmpty(n.Label));
            if (next != null)
                GetAccessibilityNodeProvider(_host)?.PerformAction(next.Id, AccessibilityNodeInfoCompat.ActionAccessibilityFocus, null);
        }

        // Scroll paging sits on the canvas node: our virtual views are flat, there is no scroll node above them. It pages
        // the scroll holding the node TalkBack is on, and only while that scroll can move, so a page that does not scroll
        // is not called scrollable.
        protected override void OnPopulateNodeForHost(AccessibilityNodeInfoCompat info)
        {
            var source = Find(AccessibilityFocusedVirtualViewId)?.Source;
            if (source == null)
                return;

            var forward = SkiaAccessibilityManager.Page(source, true, true, probe: true) || SkiaAccessibilityManager.Page(source, false, true, probe: true);
            var backward = SkiaAccessibilityManager.Page(source, true, false, probe: true) || SkiaAccessibilityManager.Page(source, false, false, probe: true);
            if (forward)
                info.AddAction(AccessibilityNodeInfoCompat.ActionScrollForward);
            if (backward)
                info.AddAction(AccessibilityNodeInfoCompat.ActionScrollBackward);
            info.Scrollable = forward || backward;
        }

        public override bool PerformAccessibilityAction(View host, int action, Bundle args)
        {
            if (action is AccessibilityNodeInfoCompat.ActionScrollForward or AccessibilityNodeInfoCompat.ActionScrollBackward)
            {
                var source = Find(AccessibilityFocusedVirtualViewId)?.Source;
                var forward = action == AccessibilityNodeInfoCompat.ActionScrollForward;
                if (source != null && (SkiaAccessibilityManager.Page(source, true, forward) || SkiaAccessibilityManager.Page(source, false, forward)))
                    return true;
            }

            return base.PerformAccessibilityAction(host, action, args);
        }

        // the last node in reading order that contains the point, as MAUI Windows: a title over its card
        protected override int GetVirtualViewAt(float x, float y)
        {
            var snapshot = Snapshot(false);
            var scale = Scale;
            for (var i = snapshot.Length - 1; i >= 0; i--)
            {
                var rect = snapshot[i].Rect;
                if (x >= rect.Left * scale && x <= rect.Right * scale && y >= rect.Top * scale && y <= rect.Bottom * scale)
                    return snapshot[i].Id;
            }

            return HostId;
        }

        protected override void GetVisibleVirtualViews(IList<Java.Lang.Integer> virtualViewIds)
        {
            foreach (var node in Snapshot(true))
                virtualViewIds.Add(Java.Lang.Integer.ValueOf(node.Id));
        }

        protected override void OnPopulateNodeForVirtualView(int virtualViewId, AccessibilityNodeInfoCompat info)
        {
            var node = Find(virtualViewId);
            if (node == null)
            {
                // left the snapshot since TalkBack asked: an empty node, the helper needs bounds
                info.ContentDescription = string.Empty;
                info.SetBoundsInParent(new Android.Graphics.Rect(0, 0, 1, 1));
                return;
            }

            var scale = Scale;
            info.SetBoundsInParent(new Android.Graphics.Rect(
                (int)Math.Floor(node.Rect.Left * scale), (int)Math.Floor(node.Rect.Top * scale),
                (int)Math.Ceiling(node.Rect.Right * scale), (int)Math.Ceiling(node.Rect.Bottom * scale)));

            // the live label, the snapshot is rebuilt at most once a second; none when a title text inside says it
            var label = node.NamedByChild ? string.Empty : node.Source?.AccessibilityLabel ?? node.Label ?? string.Empty;
            if (node.Role is "text" or "heading")
                info.Text = label;
            else
                info.ContentDescription = label;
            if (!string.IsNullOrEmpty(node.Hint))
                info.HintText = node.Hint;

            info.ClassName = ClassName(node.Role);
            info.Heading = node.Role == "heading";

            var pressed = node.Source?.AccessibilityIsPressed ?? node.IsPressed;
            if (pressed.HasValue)
            {
                info.Checkable = true;
                info.Checked = pressed.Value;
            }

            // a control role that takes no input reads as disabled, as MAUI Windows (IsEnabledCore)
            info.Enabled = node.CanInteract || !DrawnUi.Models.Aria.IsInteractiveRole(node.Role);
            if (node.CanInteract)
            {
                info.Clickable = true;
                info.Focusable = true;
                info.AddAction(AccessibilityNodeInfoCompat.ActionClick);
            }

            // every node can be brought on screen (TalkBack "show on screen")
            info.AddAction(AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionShowOnScreen);

            // a range control's value: RangeInfo plus the spoken text; a slider steps with scroll forward / back
            // (TalkBack's adjust) and takes SET_PROGRESS snapped to its step
            if (node.Value is { } snapshotValue)
            {
                var value = node.Source?.GetAccessibilityValue() ?? snapshotValue;
                info.RangeInfo = AccessibilityNodeInfoCompat.RangeInfoCompat.Obtain(
                    AccessibilityNodeInfoCompat.RangeInfoCompat.RangeTypeFloat, (float)value.Min, (float)value.Max, (float)value.Now);
                if (!string.IsNullOrEmpty(value.Text))
                    info.StateDescription = value.Text;
                if (node.CanInteract && value.Step > 0)
                {
                    if (value.Now < value.Max)
                        info.AddAction(AccessibilityNodeInfoCompat.ActionScrollForward);
                    if (value.Now > value.Min)
                        info.AddAction(AccessibilityNodeInfoCompat.ActionScrollBackward);
                    info.AddAction(AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionSetProgress);
                }
            }

            var live = node.Live ?? node.Source?.AccessibilityLive;
            info.LiveRegion = live switch
            {
                "assertive" => ViewCompat.AccessibilityLiveRegionAssertive,
                "polite" => ViewCompat.AccessibilityLiveRegionPolite,
                _ => ViewCompat.AccessibilityLiveRegionNone,
            };
        }

        // double tap: the node's activation (a tap at its center), as UIA Invoke and the web overlay
        protected override bool OnPerformActionForVirtualView(int virtualViewId, int action, Bundle arguments)
        {
            var source = Find(virtualViewId)?.Source;
            if (source == null)
                return false;

            if (action == AccessibilityNodeInfoCompat.ActionClick)
            {
                if (!SkiaAccessibilityManager.Activate(source))
                    return false;
                InvalidateVirtualView(virtualViewId);
                SendEventForVirtualView(virtualViewId, (int)EventTypes.ViewClicked);
                return true;
            }

            bool used;
            if (action is AccessibilityNodeInfoCompat.ActionScrollForward or AccessibilityNodeInfoCompat.ActionScrollBackward)
                used = SkiaAccessibilityManager.Adjust(source, action == AccessibilityNodeInfoCompat.ActionScrollForward);
            else if (action == AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionSetProgress.Id)
                used = arguments != null && SkiaAccessibilityManager.SetValue(source,
                    arguments.GetFloat(AccessibilityNodeInfoCompat.ActionArgumentProgressValue));
            else if (action == AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionShowOnScreen.Id)
            {
                SkiaAccessibilityManager.ScrollIntoView(source);
                used = true;
            }
            else
                return false;

            if (used)
                InvalidateVirtualView(virtualViewId); // the new value is read back from the source at once
            return used;
        }

        private static string ClassName(string role) => role switch
        {
            "button" or "link" or "menuitem" or "tab" or "option" => "android.widget.Button",
            "checkbox" or "menuitemcheckbox" => "android.widget.CheckBox",
            "switch" => "android.widget.Switch",
            "radio" or "menuitemradio" => "android.widget.RadioButton",
            "slider" => "android.widget.SeekBar",
            "progressbar" => "android.widget.ProgressBar",
            "textbox" or "searchbox" => "android.widget.EditText",
            "img" => "android.widget.ImageView",
            "text" or "heading" => "android.widget.TextView",
            "list" or "listbox" => "android.widget.ListView",
            _ => "android.view.View",
        };

        private sealed class HoverForwarder : Java.Lang.Object, View.IOnHoverListener
        {
            private readonly DrawnUiAccessibilityHelper _helper;

            public HoverForwarder(DrawnUiAccessibilityHelper helper) => _helper = helper;

            public bool OnHover(View v, MotionEvent e) => _helper.DispatchHoverEvent(e);
        }
    }
}
