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

        public void NotifyFocused(ISkiaAccessibilityNode? node)
        {
            if (ReferenceEquals(FocusedNode, node)) return;
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
            if (!_dirty && _nodes.IsEmpty) return;

            var now = Environment.TickCount64;
            if (now - _lastRebuildTick < MinUpdateIntervalMs) return;

            _dirty = false;
            _lastRebuildTick = now;

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
