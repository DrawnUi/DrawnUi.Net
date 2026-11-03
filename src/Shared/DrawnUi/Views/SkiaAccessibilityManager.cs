using System.Collections.Concurrent;
using DrawnUi.Draw;

namespace DrawnUi.Views
{
    public record AccessibilityNode(string? Label, string? Hint, string? Role, SKRect Rect, bool CanInteract, bool? IsPressed, string? Live = null)
    {
        internal ISkiaAccessibilityNode? Source { get; init; }

        /// <summary>Stable id of the source control (see <see cref="ISkiaAccessibilityNode.AccessibilityId"/>).</summary>
        public int Id { get; init; }

        /// <summary>The value of a range control (slider, progress bar), null for other nodes.</summary>
        public AccessibilityValue? Value { get; init; }

        /// <summary>
        /// A title text inside says this node's name (a card and its title), so the node is not named again: a head reads
        /// no name for it, also not the live <see cref="ISkiaAccessibilityNode.AccessibilityLabel"/>.
        /// </summary>
        public bool NamedByChild { get; init; }

        internal static AccessibilityNode From(ISkiaAccessibilityNode node, SKRect px, float scale)
        {
            return new AccessibilityNode(
                node.AccessibilityLabel,
                node.AccessibilityHint,
                node.AccessibilityRole,
                new SKRect(px.Left / scale, px.Top / scale, px.Right / scale, px.Bottom / scale),
                node.AccessibilityCanInteract,
                node.AccessibilityIsPressed,
                node.AccessibilityLive)
            {
                Source = node,
                Id = node.AccessibilityId,
                Value = node.GetAccessibilityValue()
            };
        }
    }

    public class SkiaAccessibilityManager
    {
        private readonly ConcurrentDictionary<ISkiaAccessibilityNode, byte> _nodes = new();
        private readonly List<(ISkiaAccessibilityNode Node, SKRect Rect)> _sortBuffer = new();
        private volatile bool _dirty;
        private volatile bool _stale; // frames were drawn since the last rebuild, the rate limit skipped them
        private long _lastRebuildTick;

        /// <summary>
        /// Minimum milliseconds between snapshot rebuilds. Default 1000ms.
        /// </summary>
        public long MinUpdateIntervalMs { get; set; } = 1000;

        public AccessibilityNode[] Snapshot { get; private set; } = [];

        public event Action? Changed;

        /// <summary>
        /// The rate limit skipped a rebuild: frames moved or changed nodes and the snapshot is stale until the next
        /// frame end after the interval. Raised once per stale period, on the rendering thread, with the milliseconds
        /// left until a rebuild may run. A head whose canvas can go idle right after a change (a page that just opened)
        /// calls <see cref="RefreshIfStale"/> then, on its rendering thread, so assistive technology never keeps the
        /// previous page's nodes: no frame is drawn for it.
        /// </summary>
        public event Action<long>? RebuildSkipped;
        public event Action<ISkiaAccessibilityNode?>? FocusChanged;

        /// <summary>
        /// Fired immediately (bypassing snapshot rate-limit) when a live-region node's value changes.
        /// Platform layer uses this to raise the AT live-region announcement.
        /// </summary>
        public event Action<ISkiaAccessibilityNode>? LiveRegionUpdated;

        public ISkiaAccessibilityNode? FocusedNode { get; private set; }

        /// <summary>
        /// The node the screen reader's cursor is on, reported by the head (<see cref="NotifyReaderFocused"/>): TalkBack's
        /// accessibility focus, VoiceOver's focused element, UI Automation focus.
        /// </summary>
        public ISkiaAccessibilityNode? ReaderNode { get; private set; }

        /// <summary>
        /// Raised on the rendering thread after a rebuild dropped <see cref="ReaderNode"/> (its page closed, a popup gone),
        /// with the first node in reading order that says something (a name, or takes input). The head moves the screen
        /// reader there, so its cursor does not stay on an empty spot.
        /// </summary>
        public event Action<AccessibilityNode>? ReaderRefocusRequested;

        /// <summary>The head reports where the screen reader's cursor went; null when it left the canvas.</summary>
        public void NotifyReaderFocused(ISkiaAccessibilityNode? node) => ReaderNode = node;

        /// <summary>
        /// Enter / Space from keyboard navigation or a screen reader's Invoke: activates the node only while a tap
        /// could reach it (<see cref="ISkiaAccessibilityNode.AccessibilityCanInteract"/>), the rule every head applies.
        /// </summary>
        public static bool Activate(ISkiaAccessibilityNode? node)
        {
            if (node == null || !node.AccessibilityCanInteract)
                return false;

            node.OnAccessibilityActivated();
            RefreshAfterAction(node);
            return true;
        }

        /// <summary>
        /// A screen reader's Increment / Decrement (VoiceOver swipe up / down, TalkBack, UI Automation): one step of a range
        /// control, the arrow key's path (<see cref="ISkiaAccessibilityNode.OnAccessibilityKey"/> ArrowUp / ArrowDown), only
        /// while a pan could reach it. Never moves focus in a group.
        /// </summary>
        public static bool Adjust(ISkiaAccessibilityNode? node, bool increment)
        {
            if (node?.GetAccessibilityValue() == null || !CanAdjust(node)
                || !node.OnAccessibilityKey(increment ? InputKey.ArrowUp : InputKey.ArrowDown))
                return false;

            RefreshAfterAction(node);
            return true;
        }

        /// <summary>A screen reader sets a range control's value (UI Automation RangeValue.SetValue), snapped to its step.</summary>
        public static bool SetValue(ISkiaAccessibilityNode? node, double value)
        {
            if (node?.GetAccessibilityValue() == null || !CanAdjust(node) || !node.OnAccessibilitySetValue(value))
                return false;

            RefreshAfterAction(node);
            return true;
        }

        /// <summary>
        /// A screen reader asks to see the node (UI Automation ScrollItem, TalkBack show on screen, VoiceOver): the scrolls
        /// above it bring it into view, as for keyboard focus, without moving any focus.
        /// </summary>
        public static void ScrollIntoView(ISkiaAccessibilityNode? node)
        {
            if (node is SkiaControl control)
                SkiaScroll.EnsureVisible(control);
        }

        /// <summary>
        /// A screen reader pages from the node: the nearest scroll above it that can move that way (<see cref="SkiaScroll.AccessibilityPage"/>).
        /// False when none can, so the reader says there is nothing more; <paramref name="probe"/> only answers that.
        /// </summary>
        public static bool Page(ISkiaAccessibilityNode? node, bool vertical, bool forward, bool probe = false)
        {
            for (var parent = (node as SkiaControl)?.Parent; parent is SkiaControl control; parent = control.Parent)
            {
                if (control is SkiaScroll scroll && scroll.AccessibilityPage(vertical, forward, probe))
                    return true;
            }

            return false;
        }

        private static bool CanAdjust(ISkiaAccessibilityNode node) =>
            node is not SkiaControl control || control.CanReceiveGesture(AppoMobi.Gestures.TouchActionResult.Panning);

        /// <summary>
        /// After a screen reader's action the snapshot is rebuilt on the next frame, not after <see cref="MinUpdateIntervalMs"/>:
        /// a reader reads the new value or state back at once, and a stale one made VoiceOver fall back to jumps.
        /// </summary>
        private static void RefreshAfterAction(ISkiaAccessibilityNode node)
        {
            if (node is SkiaControl { Superview: { } view } control)
            {
                view.AccessibilityManager.ForceRebuildOnNextFrame();
                control.Update();
            }
        }

        /// <summary>
        /// Arrow keys, Home / End, PageUp / PageDown for the node in keyboard focus. The control gets them first, only
        /// while a pan could reach it (<see cref="SkiaControl.CanReceiveGesture"/>): a slider steps its value. Keys the
        /// control does not use move keyboard focus between the items of the group around it (<see cref="MoveInGroup"/>).
        /// </summary>
        public static bool Key(ISkiaAccessibilityNode? node, InputKey key)
        {
            if (node == null)
                return false;

            if ((node is not SkiaControl control || control.CanReceiveGesture(AppoMobi.Gestures.TouchActionResult.Panning))
                && node.OnAccessibilityKey(key))
            {
                if (node.GetAccessibilityValue() != null)
                    RefreshAfterAction(node); // a reader reads the new value back at once
                return true;
            }

            return node is SkiaControl focused && focused.Superview?.AccessibilityManager.MoveInGroup(focused, key) == true;
        }

        #region Arrow-key groups

        /// <summary>
        /// Keyboard focus moved by the manager itself (arrow keys in a group): heads that keep their own focus element
        /// (Blazor's overlay) move it there. MAUI Windows and WPF follow <see cref="FocusChanged"/>, raised too.
        /// </summary>
        public event Action<ISkiaAccessibilityNode>? KeyboardFocusRequested;

        private sealed class GroupFocusRequest
        {
            public SkiaControl Group = null!;
            public SkiaScroll? Scroll;
            public int Index;
            public int Step;
            public long Deadline;
            public bool ScrollIssued;
        }

        private GroupFocusRequest? _groupRequest;
        private readonly object _groupRequestLock = new();

        /// <summary>
        /// While set, keyboard navigation stays inside this control: Tab and Shift+Tab stop only on its nodes. Set by the
        /// canvas to the top popup window that takes input (a menu, a dropdown), null when none is open.
        /// </summary>
        public SkiaControl? KeyboardScope { get; internal set; }

        /// <summary>Controls with <see cref="SkiaControl.AccessibilityModal"/> set, on every canvas. Empty: no modal work at all.</summary>
        internal static readonly System.Collections.Concurrent.ConcurrentDictionary<SkiaControl, byte> Modals = new();

        /// <summary>
        /// The modal overlay that accessibility is limited to (<see cref="SkiaControl.AccessibilityModal"/>): the topmost
        /// flagged control that is visible and was drawn in the last frame. Null when none.
        /// </summary>
        public SkiaControl? ActiveModal { get; private set; }

        // where the keyboard was when each modal became active, to go back there when it goes
        private readonly Dictionary<SkiaControl, ISkiaAccessibilityNode> _modalReturns = new();

        /// <summary>Frame end, only while some control is flagged modal or one was active: follows the top modal.</summary>
        private void UpdateActiveModal()
        {
            SkiaControl? next = null;
            foreach (var modal in Modals.Keys)
            {
                if (modal.IsDisposed || !modal.IsVisible || modal.Opacity <= 0 || IsHiddenByAncestor(modal)
                    || !ReferenceEquals(modal.Superview?.AccessibilityManager, this) || !modal.WasInLastFrame())
                    continue;
                if (next == null || IsDrawnAbove(modal, next))
                    next = modal;
            }

            var previous = ActiveModal;
            if (ReferenceEquals(previous, next))
                return;

            ActiveModal = next;
            ForceRebuildOnNextFrame(); // readers and Tab switch to the new scope in this frame

            var canvas = (next ?? previous)?.Superview;
            if (canvas == null)
                return;

            var keyboard = canvas.KeyboardFocusNode;
            if (next != null && keyboard != null && !IsDescendantOrSelf(keyboard, next))
            {
                // the keyboard is in use: into the modal, and back here when it goes
                _modalReturns[next] = keyboard;
                RequestFocusInside(next);
            }

            if (previous != null && _modalReturns.Remove(previous, out var back)
                && (keyboard == null || IsDescendantOrSelf(keyboard, previous)) // unless it went elsewhere meanwhile
                && (next == null || IsDescendantOrSelf(back, next)))
            {
                RequestFocusReturn(back);
            }
        }

        /// <summary>Drawing order: <paramref name="a"/> draws over <paramref name="b"/> (ZIndex, then tree order; a nested control over its ancestor).</summary>
        private static bool IsDrawnAbove(SkiaControl a, SkiaControl b)
        {
            var pathA = PathFromRoot(a);
            var pathB = PathFromRoot(b);
            var i = 0;
            while (i < pathA.Count && i < pathB.Count && ReferenceEquals(pathA[i], pathB[i]))
                i++;

            if (i == pathA.Count)
                return false; // a contains b
            if (i == pathB.Count)
                return true; // b contains a

            var sa = pathA[i];
            var sb = pathB[i];
            if (sa.ZIndex != sb.ZIndex)
                return sa.ZIndex > sb.ZIndex;
            return sa.Parent is SkiaControl parent && parent.Views.IndexOf(sa) > parent.Views.IndexOf(sb);
        }

        private static List<SkiaControl> PathFromRoot(SkiaControl control)
        {
            var path = new List<SkiaControl>();
            IDrawnBase? current = control;
            while (current is SkiaControl c)
            {
                path.Add(c);
                current = c.Parent;
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Whether a screen reader is running. Decides what happens when the focused control leaves the screen: with a
        /// reader, its cursor moves to the first node that says something; without one, the focus goes to nothing, as in
        /// browsers, so an app's own keys work again. Windows reads the system flag that screen readers set
        /// (Narrator, NVDA, JAWS); elsewhere it is assumed true. An app or a head can replace it.
        /// </summary>
        public Func<bool> IsScreenReaderRunning { get; set; } = DefaultIsScreenReaderRunning;

        private static bool DefaultIsScreenReaderRunning()
        {
            if (!OperatingSystem.IsWindows())
                return true;
            try
            {
                var running = false;
                return SystemParametersInfoW(0x0046 /* SPI_GETSCREENREADER */, 0, ref running, 0) ? running : true;
            }
            catch
            {
                return true;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SystemParametersInfoW(uint action, uint param, ref bool value, uint winIni);

        /// <summary>
        /// The node is outside what keyboard navigation may reach now: a modal overlay is shown and the node is neither
        /// in it nor in a popup window. A head treats such a focus as no focus, so Tab or an arrow starts in the modal.
        /// </summary>
        public bool IsOutsideModal(ISkiaAccessibilityNode? node) =>
            node is SkiaControl control && ActiveModal is { } modal
            && !IsDescendantOrSelf(control, modal) && !IsInPopupWindow(control);

        /// <summary>Inside a popup window (a menu, a dropdown), which stays readable over a modal it was opened from.</summary>
        private static bool IsInPopupWindow(SkiaControl control)
        {
            IDrawnBase? current = control;
            while (current is SkiaControl c)
            {
                if (c is PopupLayer)
                    return true;
                current = c.Parent;
            }
            return false;
        }

        private SkiaControl? _focusInside;
        private long _focusInsideDeadline;
        private ISkiaAccessibilityNode? _focusReturn;

        /// <summary>A popup window opened from the keyboard: its first node takes the keyboard focus once it is drawn.</summary>
        internal void RequestFocusInside(SkiaControl root)
        {
            _focusReturn = null;
            _focusInsideDeadline = Environment.TickCount64 + 1000;
            Volatile.Write(ref _focusInside, root);
        }

        /// <summary>A popup window that had the keyboard closed: the keyboard focus goes back to what opened it, at the frame end.</summary>
        internal void RequestFocusReturn(ISkiaAccessibilityNode node)
        {
            // the reader's node is going away with the popup: a rebuild before the return lands must not send the
            // reader (and the heads' focus with it) to the first node of the page
            ReaderNode = node;
            if (Volatile.Read(ref _focusInside) == null) // a menu opening next (Left / Right in a menu bar) wins
                Volatile.Write(ref _focusReturn, node);
        }

        private void ProcessPendingFocus(float scale)
        {
            var inside = Volatile.Read(ref _focusInside);
            if (inside != null)
            {
                if (inside.IsDisposed || Environment.TickCount64 > _focusInsideDeadline)
                {
                    _focusInside = null;
                }
                else
                {
                    Rebuild(scale);
                    foreach (var n in Snapshot)
                    {
                        if (n.Source is SkiaControl { AccessibilityCanInteract: true } source && IsDescendantOrSelf(source, inside))
                        {
                            _focusInside = null;
                            FocusFromKeyboard(source, scale);
                            break;
                        }
                    }
                }
            }

            var back = Interlocked.Exchange(ref _focusReturn, null);
            if (back is SkiaControl { IsDisposed: false, AccessibilityCanInteract: true })
            {
                Rebuild(scale); // the closed popup's nodes leave before the focus moves
                if (IsInSnapshot((SkiaControl)back))
                    FocusFromKeyboard(back, scale);
            }
        }

        // the ScrollToIndex order keyboard navigation issued last: dropped once the item has focus, or the scroll
        // keeps homing to it (re-issues on stall) and pulls the list back while focus already moved on
        private SkiaScroll? _orderScroll;
        private int _orderIndex = -1;

        // last focused node of each group: where Tab enters the group
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<SkiaControl, ISkiaAccessibilityNode> _groupFocus = new();

        private enum GroupAxis { Vertical, Horizontal, Both }

        /// <summary>
        /// The arrow-key group around <paramref name="control"/>: its nearest ancestor with a composite role
        /// (<see cref="DrawnUi.Models.Aria.IsCompositeRole"/>, e.g. <c>Aria.RoleList</c> on a SkiaStack), and the item of that
        /// group holding the control (one of its children, a cell of a templated layout).
        /// </summary>
        public static bool TryFindGroup(SkiaControl control, out SkiaControl group, out SkiaControl item)
        {
            var child = control;
            var parent = control.Parent as SkiaControl;
            while (parent != null)
            {
                if (DrawnUi.Models.Aria.IsCompositeRole(parent.AccessibilityRole))
                {
                    group = parent;
                    item = child;
                    return true;
                }

                child = parent;
                parent = parent.Parent as SkiaControl;
            }

            group = null!;
            item = null!;
            return false;
        }

        /// <summary>Remembers where keyboard focus is inside its group, so the next Tab into the group lands there.</summary>
        internal void NoteFocus(ISkiaAccessibilityNode? node)
        {
            if (node is SkiaControl control && TryFindGroup(control, out var group, out _))
                _groupFocus.AddOrUpdate(group, node);
        }

        /// <summary>
        /// Whether Tab / Shift+Tab stop on this node. A group of items (<see cref="TryFindGroup"/>) is one Tab stop,
        /// like a native list: its current item, the one keyboard focus last had there (else the group's first usable
        /// node), plus the controls inside that same item, then Tab leaves the group. The arrow keys move between items.
        /// Every node outside a group is a stop.
        /// </summary>
        public bool IsTabStop(ISkiaAccessibilityNode? node)
        {
            // an open popup window keeps the keyboard: Tab never leaves it, as with native menus
            if (KeyboardScope is { } scope && (node == null || !IsDescendantOrSelf(node, scope)))
                return false;

            if (node is not SkiaControl control || !TryFindGroup(control, out var group, out var item))
                return true;

            SkiaControl? stopItem = null;
            if (_groupFocus.TryGetValue(group, out var remembered) && remembered is SkiaControl current
                && !current.IsDisposed && current.AccessibilityCanInteract && IsInSnapshot(current)
                && TryFindGroup(current, out var currentGroup, out var currentItem) && ReferenceEquals(currentGroup, group))
            {
                stopItem = currentItem;
            }
            else
            {
                foreach (var n in Snapshot)
                {
                    if (n.Source is SkiaControl s && s.AccessibilityCanInteract
                        && TryFindGroup(s, out var g, out var i) && ReferenceEquals(g, group))
                    {
                        stopItem = i;
                        break;
                    }
                }
            }

            return stopItem == null || ReferenceEquals(item, stopItem);
        }

        /// <summary>
        /// The Tab stop after <paramref name="from"/> in reading order (<see cref="IsTabStop"/>, interactive nodes only),
        /// or the one before it for Shift+Tab. Null past either end: keyboard focus leaves the drawn nodes and the next
        /// Tab starts over. From a node that is not a stop it starts at the first / last stop. The order the MAUI Windows
        /// and WPF accessibility peers walk, for heads that have no such layer (MAUI Mac Catalyst).
        /// </summary>
        public ISkiaAccessibilityNode? NextTabStop(ISkiaAccessibilityNode? from, bool forward)
        {
            var stops = new List<ISkiaAccessibilityNode>();
            foreach (var n in Snapshot)
            {
                if (n.Source is { AccessibilityCanInteract: true } source && IsTabStop(source))
                    stops.Add(source);
            }

            var current = from == null ? -1 : stops.IndexOf(from);
            if (current < 0)
                current = forward ? -1 : stops.Count;
            var next = forward ? current + 1 : current - 1;
            if ((KeyboardScope != null || ActiveModal != null) && stops.Count > 0)
                next = (next + stops.Count) % stops.Count; // inside an open popup Tab goes round, it never leaves
            return next >= 0 && next < stops.Count ? stops[next] : null;
        }

        /// <summary>Whether <paramref name="node"/> is in the current <see cref="Snapshot"/>.</summary>
        internal bool IsInSnapshot(ISkiaAccessibilityNode node)
        {
            foreach (var n in Snapshot)
            {
                if (ReferenceEquals(n.Source, node))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Moves keyboard focus between the items of the group around <paramref name="focused"/> (<see cref="TryFindGroup"/>),
        /// by item index like a native list: Down / Up in a Column, Right / Left in a Row, all four in a Wrap, a Grid or a
        /// Split layout (Up / Down by one row), Home / End to the first / last item, PageDown / PageUp by one viewport of the
        /// scroll around it. No wrap at the ends. The target is the item itself when it is an interactive node, else its
        /// first interactive node; items the pointer cannot use are skipped. An item not realized yet (recycled cells, large
        /// windowed ItemsSource) is scrolled in with ScrollToIndex when the group is the scroll's content, and focused once drawn.
        /// Returns true when the key belongs to the group.
        /// </summary>
        public bool MoveInGroup(SkiaControl focused, InputKey key)
        {
            if (!TryFindGroup(focused, out var group, out var item))
                return false;

            var scroll = FindScroll(group);
            var axis = AxisOf(group);
            var count = ItemCount(group);

            int current;
            lock (_groupRequestLock)
            {
                current = _groupRequest != null && ReferenceEquals(_groupRequest.Group, group) ? _groupRequest.Index : IndexOf(group, item);
            }
            if (current < 0 || count < 1)
                return false;

            var row = axis == GroupAxis.Both ? RowLength(group, item) : 1;
            var vertical = axis != GroupAxis.Horizontal;

            int target, step;
            if (group is SkiaLayout { Type: LayoutType.Masonry } masonry
                && MasonryTarget(masonry, current, key, scroll) is var masonryTarget and >= 0)
            {
                target = masonryTarget;
                step = key is InputKey.ArrowDown or InputKey.ArrowRight or InputKey.PageDown ? 1 : -1;
            }
            else
            switch (key)
            {
                case InputKey.ArrowDown when axis == GroupAxis.Vertical:
                case InputKey.ArrowRight when axis != GroupAxis.Vertical:
                    target = current + 1;
                    step = 1;
                    break;
                case InputKey.ArrowUp when axis == GroupAxis.Vertical:
                case InputKey.ArrowLeft when axis != GroupAxis.Vertical:
                    target = current - 1;
                    step = -1;
                    break;
                case InputKey.ArrowDown when axis == GroupAxis.Both:
                    target = Math.Min(count - 1, current + row);
                    step = 1;
                    break;
                case InputKey.ArrowUp when axis == GroupAxis.Both:
                    target = Math.Max(0, current - row);
                    step = -1;
                    break;
                case InputKey.Home:
                    target = 0;
                    step = 1;
                    break;
                case InputKey.End:
                    target = count - 1;
                    step = -1;
                    break;
                case InputKey.PageDown when scroll != null:
                    target = Math.Min(count - 1, current + PageSize(scroll, item, vertical) * row);
                    step = 1;
                    break;
                case InputKey.PageUp when scroll != null:
                    target = Math.Max(0, current - PageSize(scroll, item, vertical) * row);
                    step = -1;
                    break;
                default:
                    return false;
            }

            if (target < 0 || target >= count || target == current)
                return true; // at the end of the group nothing happens, the key still belongs to it

            lock (_groupRequestLock)
            {
                _groupRequest = new GroupFocusRequest
                {
                    Group = group,
                    Scroll = scroll,
                    Index = target,
                    Step = step,
                    Deadline = Environment.TickCount64 + 3000,
                };
            }

            // resolved at the end of the next frame, on the rendering side, together with the snapshot
            group.Superview?.Update();
            return true;
        }

        /// <summary>
        /// Target of an arrow or page key in a Masonry group, from the placed items: its columns have uneven heights, so
        /// the item Split places later is rarely under the focused one. Up / Down go to the previous / next item of the
        /// same column, Left / Right to the item of the neighbour column nearest in height, PageUp / PageDown one viewport
        /// along the column. <paramref name="current"/> when there is no item that way, -1 for other keys or no placement.
        /// </summary>
        private static int MasonryTarget(SkiaLayout masonry, int current, InputKey key, SkiaScroll? scroll)
        {
            if (key is not (InputKey.ArrowDown or InputKey.ArrowUp or InputKey.ArrowLeft or InputKey.ArrowRight
                or InputKey.PageDown or InputKey.PageUp))
                return -1;

            var cells = masonry.GetStackStructure()?.GetChildren().Where(c => c != null && c.ControlIndex >= 0).ToList();
            var focused = cells?.FirstOrDefault(c => c.ControlIndex == current);
            if (focused == null)
                return -1;

            var rect = focused.Destination;
            var column = cells.Where(c => c.Column == focused.Column).OrderBy(c => c.Destination.Top).ToList();
            ControlInStack? found = key switch
            {
                InputKey.ArrowDown => column.FirstOrDefault(c => c.Destination.Top > rect.Top),
                InputKey.ArrowUp => column.LastOrDefault(c => c.Destination.Top < rect.Top),
                InputKey.ArrowRight or InputKey.ArrowLeft => cells
                    .Where(c => c.Column == focused.Column + (key == InputKey.ArrowRight ? 1 : -1))
                    .MinBy(c => Math.Abs(c.Destination.MidY - rect.MidY)),
                InputKey.PageDown when scroll != null => column.LastOrDefault(c => c.Destination.Top > rect.Top
                    && c.Destination.Top <= rect.Top + scroll.Viewport.Pixels.Height) ?? column.FirstOrDefault(c => c.Destination.Top > rect.Top),
                InputKey.PageUp when scroll != null => column.FirstOrDefault(c => c.Destination.Top < rect.Top
                    && c.Destination.Top >= rect.Top - scroll.Viewport.Pixels.Height) ?? column.LastOrDefault(c => c.Destination.Top < rect.Top),
                _ => null,
            };

            if (found == null && key is InputKey.PageDown or InputKey.PageUp && scroll == null)
                return -1;

            return found?.ControlIndex ?? current;
        }

        private static SkiaScroll? FindScroll(SkiaControl group)
        {
            var parent = group.Parent as SkiaControl;
            while (parent != null && parent is not SkiaScroll)
                parent = parent.Parent as SkiaControl;
            return parent as SkiaScroll;
        }

        private static GroupAxis AxisOf(SkiaControl group) => group switch
        {
            SkiaLayout { Type: LayoutType.Column, Split: <= 1 } => GroupAxis.Vertical,
            SkiaLayout { Type: LayoutType.Row } => GroupAxis.Horizontal,
            SkiaLayout { Type: LayoutType.Absolute } => GroupAxis.Vertical,
            SkiaLayout => GroupAxis.Both,
            _ => GroupAxis.Vertical,
        };

        /// <summary>
        /// Items per row of a 2D group: Split when set, else the drawn items sharing the first drawn row. Not the row of
        /// <paramref name="item"/>: a short last row (12 tiles, 10 per row) made Up from 12 land on 10 instead of 2.
        /// </summary>
        private static int RowLength(SkiaControl group, SkiaControl item)
        {
            if (group is SkiaLayout { Split: > 1 } split)
                return split.Split;

            var top = SKRect.Empty;
            foreach (var child in group.Views)
            {
                top = child.GetAccessibilityPixelRect();
                if (!top.IsEmpty)
                    break;
            }

            if (top.IsEmpty)
                return 1;

            var count = 0;
            foreach (var child in group.Views)
            {
                var r = child.GetAccessibilityPixelRect();
                if (!r.IsEmpty && Math.Abs(r.MidY - top.MidY) < top.Height / 2)
                    count++;
            }

            return Math.Max(1, count);
        }

        // Item indices are global: a templated layout with a built-in source window (large ItemsSource) realizes a window
        // of it, its cells carry window-local indices, and ScrollToIndex takes global ones and rebases the window.

        private static int ItemCount(SkiaControl group) =>
            group is SkiaLayout { IsTemplated: true } list
                ? list.ItemsWindow?.Source?.Count ?? list.EffectiveItemsSource?.Count ?? 0
                : group.Views.Count;

        private static int IndexOf(SkiaControl group, SkiaControl item) =>
            group is SkiaLayout { IsTemplated: true } list
                ? item.ContextIndex + (list.ItemsWindow?.WindowStart ?? 0)
                : group.Views.IndexOf(item);

        private static SkiaControl? ItemAt(SkiaControl group, int index)
        {
            if (group is SkiaLayout { IsTemplated: true } list)
            {
                var local = index - (list.ItemsWindow?.WindowStart ?? 0);
                return local >= 0 ? list.ChildrenFactory?.GetCellInUseOrNull(local) : null;
            }

            return index >= 0 && index < group.Views.Count ? group.Views[index] : null;
        }

        private static int PageSize(SkiaScroll scroll, SkiaControl item, bool vertical)
        {
            var viewport = vertical ? scroll.Viewport.Units.Height : scroll.Viewport.Units.Width;
            var size = vertical ? item.MeasuredSize.Units.Height : item.MeasuredSize.Units.Width;
            return size > 0 ? Math.Max(1, (int)(viewport / size)) : 1;
        }

        /// <summary>The item itself when it is an interactive node, else its first interactive node in tree order.</summary>
        private static ISkiaAccessibilityNode? FindTarget(SkiaControl item)
        {
            if (item.IsAccessibilityElement && item.AccessibilityCanInteract)
                return item;

            foreach (var child in item.Views)
            {
                var found = FindTarget(child);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void ScrollToItem(GroupFocusRequest request)
        {
            // ScrollToIndex speaks item indices of the scroll's own content (global ones for a windowed source)
            if (request.Scroll != null && ReferenceEquals(request.Scroll.Content, request.Group))
            {
                request.Scroll.ScrollToIndex(request.Index, true,
                    request.Step > 0 ? RelativePositionType.End : RelativePositionType.Start);
                _orderScroll = request.Scroll;
                _orderIndex = request.Scroll.PendingScrollToIndex;
            }
        }

        /// <summary>Frame end: focus the requested item once it is drawn, skipping items that cannot take input.</summary>
        private void ProcessGroupFocus(float scale)
        {
            var request = Volatile.Read(ref _groupRequest); // every frame: no lock for the common empty case
            if (request == null)
                return;

            SkiaControl? CellAt(int index)
            {
                var item = ItemAt(request.Group, index);
                // a recycled cell still mapped to this slot while the list scrolls, but bound to another item
                return item != null && IndexOf(request.Group, item) == index ? item : null;
            }

            // a hidden item (IsVisible false, transparent) is never drawn: skip it at once like one that takes no input,
            // instead of waiting for it to be drawn
            static bool Hidden(SkiaControl item) => !item.IsVisible || item.Opacity <= 0;

            var cell = CellAt(request.Index);
            while (cell != null && (Hidden(cell) || cell.WasInLastFrame()))
            {
                var node = Hidden(cell) ? null : FindTarget(cell);
                if (node != null)
                {
                    ClearGroupRequest(request);
                    FocusFromKeyboard(node, scale);
                    return;
                }

                // this item cannot take input: go on to the next one in the same direction
                request.Index += request.Step;
                if (request.Index < 0 || request.Index >= ItemCount(request.Group))
                {
                    ClearGroupRequest(request);
                    return;
                }

                request.Deadline = Environment.TickCount64 + 3000;
                request.ScrollIssued = false;
                cell = CellAt(request.Index);
            }

            if (Environment.TickCount64 > request.Deadline || request.Group.IsDisposed)
            {
                ClearGroupRequest(request);
                return;
            }

            if (!request.ScrollIssued)
            {
                request.ScrollIssued = true;
                if (ItemAt(request.Group, request.Index) is not { } next || !next.WasInLastFrame())
                    ScrollToItem(request);
            }

            request.Group.Superview?.Update(); // keep frames coming until the item is drawn
        }

        private void ClearGroupRequest(GroupFocusRequest request)
        {
            lock (_groupRequestLock)
            {
                if (ReferenceEquals(_groupRequest, request))
                    _groupRequest = null;
            }
        }

        /// <summary>
        /// Starts keyboard navigation as the first Tab or Shift+Tab does: the keyboard focus goes to the first Tab
        /// stop, or the last when <paramref name="forward"/> is false, and the heads follow (the web overlay moves the
        /// browser focus there). Used by heads whose keys arrive through KeyboardManager, for
        /// <see cref="DrawnView.ArrowsStartKeyboardNavigation"/>. False when there is no Tab stop.
        /// </summary>
        public bool FocusFirstTabStop(bool forward, float scale)
        {
            RefreshIfStale(scale);
            if (NextTabStop(null, forward) is not { } first)
                return false;
            FocusFromKeyboard(first, scale);
            return true;
        }

        private void FocusFromKeyboard(ISkiaAccessibilityNode node, float scale)
        {
            // the item is here: our scroll order is done, EnsureVisible below positions it
            if (_orderScroll != null)
            {
                _orderScroll.CancelScrollToIndex(_orderIndex);
                _orderScroll = null;
                _orderIndex = -1;
            }

            // the heads look the node up in the snapshot: make sure it is there now, not within the rebuild interval
            Rebuild(scale);

            if (node is SkiaControl control)
            {
                SkiaScroll.EnsureVisible(control);
                if (control.Superview is { } canvas)
                    canvas.KeyboardFocusNode = node;
            }

            NoteFocus(node);
            FocusedNode = node;
            FocusChanged?.Invoke(node);
            KeyboardFocusRequested?.Invoke(node);
        }

        #endregion

        public void NotifyFocused(ISkiaAccessibilityNode? node)
        {
            if (ReferenceEquals(FocusedNode, node)) return;
            NoteFocus(node);
            FocusedNode = node;
            FocusChanged?.Invoke(node);
        }

        public void Register(ISkiaAccessibilityNode node)
        {
            _nodes.TryAdd(node, 0);
            _dirty = true;
        }

        public void NotifyUpdated(ISkiaAccessibilityNode node)
        {
            if (!_nodes.ContainsKey(node)) return;
            if (!_dirty) _dirty = true;
            // Live regions bypass the snapshot rate-limit — announce value immediately.
            if (!string.IsNullOrEmpty(node.AccessibilityLive))
                LiveRegionUpdated?.Invoke(node);
        }

        public void ForceRebuildOnNextFrame()
        {
            _lastRebuildTick = 0;
            _dirty = true;
        }

        public void Unregister(ISkiaAccessibilityNode node)
        {
            if (_nodes.TryRemove(node, out _))
            {
                node.OnAccessibilityUnregistered();
                _dirty = true;
            }
        }

        public void UnregisterSubtree(ISkiaAccessibilityNode root)
        {
            bool any = false;
            foreach (var key in _nodes.Keys)
            {
                if (IsDescendantOrSelf(key, root) && _nodes.TryRemove(key, out _))
                {
                    key.OnAccessibilityUnregistered();
                    any = true;
                }
            }
            if (any) _dirty = true;
        }

        /// <summary>
        /// Called from DrawnView.OnFinalizeRendering. Rebuilds snapshot at most once per MinUpdateIntervalMs,
        /// also when no node changed: drawn frames can move nodes (scrolling, animations), and the snapshot
        /// rects follow them. Silent when nothing differs, free when no node is registered.
        /// </summary>
        internal void OnFrameEnd(float scale)
        {
            if (!Modals.IsEmpty || ActiveModal != null)
                UpdateActiveModal();
            ProcessPendingFocus(scale);
            ProcessGroupFocus(scale);

            if (!_dirty && _nodes.IsEmpty) return;

            var now = Environment.TickCount64;
            if (now - _lastRebuildTick < MinUpdateIntervalMs)
            {
                if (!_stale)
                {
                    _stale = true;
                    RebuildSkipped?.Invoke(MinUpdateIntervalMs - (now - _lastRebuildTick));
                }
                return;
            }

            Rebuild(scale);
        }

        /// <summary>
        /// Rebuilds the snapshot now when frames were drawn since the last rebuild (the <see cref="MinUpdateIntervalMs"/>
        /// rate limit skipped them), so keyboard navigation never walks the nodes of a page that just left the screen
        /// while the canvas sits idle. Free when the snapshot is current, draws no frame. Call it on the thread that
        /// renders the canvas, or on any thread when rebuilds are rare (it is serialized; MAUI Windows renders on a worker).
        /// True when the snapshot changed.
        /// </summary>
        public bool RefreshIfStale(float scale)
        {
            if (!_stale)
                return false;

            var before = Snapshot;
            Rebuild(scale);
            return !ReferenceEquals(before, Snapshot);
        }

        // a head may rebuild a stale snapshot off its rendering thread (MAUI Windows renders on a worker): one at a time
        private readonly object _rebuildLock = new();

        private void Rebuild(float scale)
        {
            lock (_rebuildLock)
                RebuildLocked(scale);
        }

        private void RebuildLocked(float scale)
        {
            _dirty = false;
            _stale = false;
            _lastRebuildTick = Environment.TickCount64;

            _sortBuffer.Clear();
            var modal = ActiveModal;
            foreach (var node in _nodes.Keys)
            {
                if (node is not SkiaControl control || !control.IsVisible || control.IsDisposed)
                    continue;
                // a modal overlay is shown: only its own nodes, and those of popup windows opened over it
                if (modal != null && !IsDescendantOrSelf(control, modal) && !IsInPopupWindow(control))
                    continue;
                // not drawn: an ancestor is hidden (e.g. the root page kept mounted under a pushed page)
                if (IsHiddenByAncestor(control))
                    continue;
                var px = node.GetAccessibilityPixelRect();
                if (px.Width <= 0 || px.Height <= 0)
                    continue;
                _sortBuffer.Add((node, px));
            }

            _sortBuffer.Sort(RectComparer);

            // reading order: nodes whose tops are within half the smaller height form one row, read left to right,
            // so a row of vertically centered controls of different heights keeps its visual order
            var rowStart = 0;
            for (int i = 1; i <= _sortBuffer.Count; i++)
            {
                if (i < _sortBuffer.Count)
                {
                    var first = _sortBuffer[rowStart].Rect;
                    var next = _sortBuffer[i].Rect;
                    if (next.Top - first.Top < Math.Min(first.Height, next.Height) / 2)
                        continue;
                }

                if (i - rowStart > 1)
                    _sortBuffer.Sort(rowStart, i - rowStart, LeftComparer);
                rowStart = i;
            }

            var snapshot = BuildSaidOnce(scale);

            // same nodes, same metadata, same rects: keep the old array and stay silent, so platform layers
            // do not raise StructureChanged (and AT does not re-traverse the tree) once per interval for nothing
            if (Same(snapshot, Snapshot))
                return;

            Snapshot = snapshot;
            Changed?.Invoke();

            if (!IsScreenReaderRunning())
            {
                // no screen reader to strand: a focus whose control left goes to nothing, as in browsers, so the keys
                // belong to the app again (a game screen) until the next Tab or arrow starts navigation
                ReaderNode = null;
                var focused = FocusedNode;
                if (focused != null && Array.FindIndex(snapshot, n => ReferenceEquals(n.Source, focused)) < 0)
                {
                    FocusedNode = null;
                    FocusChanged?.Invoke(null);
                }
                return;
            }

            var reader = ReaderNode;
            if (reader != null && Array.FindIndex(snapshot, n => ReferenceEquals(n.Source, reader)) < 0)
            {
                var next = Array.Find(snapshot, n => n.CanInteract || !string.IsNullOrEmpty(n.Label));
                ReaderNode = next?.Source;
                if (next != null)
                    ReaderRefocusRequested?.Invoke(next);
            }
        }

        /// <summary>
        /// Every name is said once (drawnui-cross 6c rule 1, as DrawnUI for Rust and React). A text or heading whose nearest
        /// node above is a control with the same name is left out: the control says it (a button and its caption). A node
        /// that takes no input and holds such a text keeps its place without a name of its own: the text says it (a card and
        /// its title). Selectable text always stays itself.
        /// </summary>
        private AccessibilityNode[] BuildSaidOnce(float scale)
        {
            var count = _sortBuffer.Count;
            _parentBuffer.Clear();
            for (int i = 0; i < count; i++)
                _parentBuffer[_sortBuffer[i].Node] = null;
            for (int i = 0; i < count; i++)
            {
                var node = _sortBuffer[i].Node;
                IDrawnBase? up = (node as SkiaControl)?.Parent;
                while (up is SkiaControl p && !_parentBuffer.ContainsKey(p))
                    up = p.Parent;
                _parentBuffer[node] = up as ISkiaAccessibilityNode;
            }

            var list = new List<AccessibilityNode>(count);
            for (int i = 0; i < count; i++)
            {
                var node = _sortBuffer[i].Node;
                var parent = _parentBuffer[node];
                if (parent != null && parent.AccessibilityCanInteract && SaysName(node, parent))
                    continue;

                var built = AccessibilityNode.From(node, _sortBuffer[i].Rect, scale);
                // a dialog keeps its name even when its title says it too (aria-labelledby): readers announce the
                // dialog by name on entry, then read the title in its place
                if (!built.CanInteract && !string.IsNullOrEmpty(built.Label)
                    && built.Role != DrawnUi.Models.Aria.RoleDialog && built.Role != "alertdialog")
                {
                    foreach (var pair in _parentBuffer)
                    {
                        if (ReferenceEquals(pair.Value, node) && SaysName(pair.Key, node))
                        {
                            built = built with { Label = null, NamedByChild = true };
                            break;
                        }
                    }
                }

                list.Add(built);
            }

            return list.ToArray();
        }

        private readonly Dictionary<ISkiaAccessibilityNode, ISkiaAccessibilityNode?> _parentBuffer = new();

        /// <summary>A plain (not selectable) text or heading with the same name as <paramref name="owner"/>.</summary>
        private static bool SaysName(ISkiaAccessibilityNode text, ISkiaAccessibilityNode owner) =>
            (text.AccessibilityRole == DrawnUi.Models.Aria.RoleText || text.AccessibilityRole == DrawnUi.Models.Aria.RoleHeading)
            && text is not SkiaLabel { AccessibilityTextSelectable: true }
            && !string.IsNullOrEmpty(owner.AccessibilityLabel)
            && text.AccessibilityLabel == owner.AccessibilityLabel;

        private static bool Same(AccessibilityNode[] a, AccessibilityNode[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                var x = a[i];
                var y = b[i];
                if (!ReferenceEquals(x.Source, y.Source)
                    || x.Label != y.Label || x.Hint != y.Hint || x.Role != y.Role
                    || x.CanInteract != y.CanInteract || x.IsPressed != y.IsPressed || x.Live != y.Live || x.Value != y.Value
                    || x.NamedByChild != y.NamedByChild
                    || Math.Abs(x.Rect.Left - y.Rect.Left) > 0.5f || Math.Abs(x.Rect.Top - y.Rect.Top) > 0.5f
                    || Math.Abs(x.Rect.Right - y.Rect.Right) > 0.5f || Math.Abs(x.Rect.Bottom - y.Rect.Bottom) > 0.5f)
                    return false;
            }
            return true;
        }

        private static bool IsHiddenByAncestor(SkiaControl control)
        {
            var current = control.Parent;
            while (current is SkiaControl p)
            {
                if (!p.IsVisible || p.Opacity <= 0)
                    return true;
                current = p.Parent;
            }
            return false;
        }

        private static readonly Comparison<(ISkiaAccessibilityNode Node, SKRect Rect)> RectComparer = (a, b) =>
        {
            var ra = a.Rect;
            var rb = b.Rect;
            var cmp = ra.Top.CompareTo(rb.Top);
            return cmp != 0 ? cmp : ra.Left.CompareTo(rb.Left);
        };

        private static readonly IComparer<(ISkiaAccessibilityNode Node, SKRect Rect)> LeftComparer =
            Comparer<(ISkiaAccessibilityNode Node, SKRect Rect)>.Create((a, b) => a.Rect.Left.CompareTo(b.Rect.Left));

        private static bool IsDescendantOrSelf(ISkiaAccessibilityNode candidate, ISkiaAccessibilityNode ancestor)
        {
            if (candidate is not SkiaControl || ancestor is not SkiaControl ancestorControl)
                return ReferenceEquals(candidate, ancestor);

            IDrawnBase? current = candidate as SkiaControl;
            while (current is SkiaControl c)
            {
                if (ReferenceEquals(c, ancestorControl)) return true;
                current = c.Parent;
            }
            return false;
        }
    }
}
