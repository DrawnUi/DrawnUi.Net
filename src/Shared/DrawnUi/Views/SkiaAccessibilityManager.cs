using System.Collections.Concurrent;
using DrawnUi.Draw;

namespace DrawnUi.Views
{
    public record AccessibilityNode(string? Label, string? Hint, string? Role, SKRect Rect, bool CanInteract, bool? IsPressed, string? Live = null)
    {
        internal ISkiaAccessibilityNode? Source { get; init; }

        /// <summary>Stable id of the source control (see <see cref="ISkiaAccessibilityNode.AccessibilityId"/>).</summary>
        public int Id { get; init; }

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
                Id = node.AccessibilityId
            };
        }
    }

    public class SkiaAccessibilityManager
    {
        private readonly ConcurrentDictionary<ISkiaAccessibilityNode, byte> _nodes = new();
        private readonly List<(ISkiaAccessibilityNode Node, SKRect Rect)> _sortBuffer = new();
        private volatile bool _dirty;
        private long _lastRebuildTick;

        /// <summary>
        /// Minimum milliseconds between snapshot rebuilds. Default 1000ms.
        /// </summary>
        public long MinUpdateIntervalMs { get; set; } = 1000;

        public AccessibilityNode[] Snapshot { get; private set; } = [];

        public event Action? Changed;
        public event Action<ISkiaAccessibilityNode?>? FocusChanged;

        /// <summary>
        /// Fired immediately (bypassing snapshot rate-limit) when a live-region node's value changes.
        /// Platform layer uses this to raise the AT live-region announcement.
        /// </summary>
        public event Action<ISkiaAccessibilityNode>? LiveRegionUpdated;

        public ISkiaAccessibilityNode? FocusedNode { get; private set; }

        /// <summary>
        /// Enter / Space from keyboard navigation or a screen reader's Invoke: activates the node only while a tap
        /// could reach it (<see cref="ISkiaAccessibilityNode.AccessibilityCanInteract"/>), the rule every head applies.
        /// </summary>
        public static bool Activate(ISkiaAccessibilityNode? node)
        {
            if (node == null || !node.AccessibilityCanInteract)
                return false;

            node.OnAccessibilityActivated();
            return true;
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
                return true;

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

        private bool IsInSnapshot(SkiaControl control)
        {
            foreach (var n in Snapshot)
            {
                if (ReferenceEquals(n.Source, control))
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

        /// <summary>Items per row of a 2D group: Split when set, else the drawn items sharing the row of <paramref name="item"/>.</summary>
        private static int RowLength(SkiaControl group, SkiaControl item)
        {
            if (group is SkiaLayout { Split: > 1 } split)
                return split.Split;

            var top = item.GetAccessibilityPixelRect();
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

            var cell = ItemAt(request.Group, request.Index);
            if (cell != null && IndexOf(request.Group, cell) != request.Index)
                cell = null; // a recycled cell still mapped to this slot while the list scrolls, but bound to another item

            if (cell != null && cell.WasInLastFrame())
            {
                var node = FindTarget(cell);
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
            }
            else if (Environment.TickCount64 > request.Deadline || request.Group.IsDisposed)
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
            ProcessGroupFocus(scale);

            if (!_dirty && _nodes.IsEmpty) return;

            var now = Environment.TickCount64;
            if (now - _lastRebuildTick < MinUpdateIntervalMs) return;

            Rebuild(scale);
        }

        private void Rebuild(float scale)
        {
            _dirty = false;
            _lastRebuildTick = Environment.TickCount64;

            _sortBuffer.Clear();
            foreach (var node in _nodes.Keys)
            {
                if (node is not SkiaControl control || !control.IsVisible || control.IsDisposed)
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

            var snapshot = new AccessibilityNode[_sortBuffer.Count];
            for (int i = 0; i < _sortBuffer.Count; i++)
                snapshot[i] = AccessibilityNode.From(_sortBuffer[i].Node, _sortBuffer[i].Rect, scale);

            // same nodes, same metadata, same rects: keep the old array and stay silent, so platform layers
            // do not raise StructureChanged (and AT does not re-traverse the tree) once per interval for nothing
            if (Same(snapshot, Snapshot))
                return;

            Snapshot = snapshot;
            Changed?.Invoke();
        }

        private static bool Same(AccessibilityNode[] a, AccessibilityNode[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                var x = a[i];
                var y = b[i];
                if (!ReferenceEquals(x.Source, y.Source)
                    || x.Label != y.Label || x.Hint != y.Hint || x.Role != y.Role
                    || x.CanInteract != y.CanInteract || x.IsPressed != y.IsPressed || x.Live != y.Live
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
