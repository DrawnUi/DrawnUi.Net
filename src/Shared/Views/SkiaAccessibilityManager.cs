using System.Collections.Concurrent;
using DrawnUi.Draw;

namespace DrawnUi.Views
{
    public record AccessibilityNode(string? Label, string? Hint, string? Role, SKRect Rect, bool CanInteract, bool? IsPressed)
    {
        internal ISkiaAccessibilityNode? Source { get; init; }

        internal static AccessibilityNode From(ISkiaAccessibilityNode node, float scale)
        {
            var px = node.GetAccessibilityPixelRect();
            return new AccessibilityNode(
                node.AccessibilityLabel,
                node.AccessibilityHint,
                node.AccessibilityRole,
                new SKRect(px.Left / scale, px.Top / scale, px.Right / scale, px.Bottom / scale),
                node.AccessibilityCanInteract,
                node.AccessibilityIsPressed)
            {
                Source = node
            };
        }
    }

    public class SkiaAccessibilityManager
    {
        private readonly ConcurrentDictionary<ISkiaAccessibilityNode, byte> _nodes = new();
        private readonly List<ISkiaAccessibilityNode> _sortBuffer = new();
        private volatile bool _dirty;
        private long _lastRebuildTick;

        /// <summary>
        /// Minimum milliseconds between snapshot rebuilds. Default 1000ms.
        /// </summary>
        public long MinUpdateIntervalMs { get; set; } = 1000;

        public AccessibilityNode[] Snapshot { get; private set; } = [];

        public event Action? Changed;

        public void Register(ISkiaAccessibilityNode node)
        {
            _nodes.TryAdd(node, 0);
            _dirty = true;
        }

        public void NotifyUpdated(ISkiaAccessibilityNode node)
        {
            if (!_nodes.ContainsKey(node)) return;
            if (!_dirty) _dirty = true;
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
        /// Called from DrawnView.OnFinalizeRendering. Rebuilds snapshot at most once per MinUpdateIntervalMs.
        /// </summary>
        internal void OnFrameEnd(float scale)
        {
            if (!_dirty) return;

            var now = Environment.TickCount64;
            if (now - _lastRebuildTick < MinUpdateIntervalMs) return;

            _dirty = false;
            _lastRebuildTick = now;

            _sortBuffer.Clear();
            foreach (var node in _nodes.Keys)
            {
                if (node is SkiaControl control && control.IsVisible && !control.IsDisposed)
                    _sortBuffer.Add(node);
            }

            _sortBuffer.Sort(RectComparer);

            var snapshot = new AccessibilityNode[_sortBuffer.Count];
            for (int i = 0; i < _sortBuffer.Count; i++)
                snapshot[i] = AccessibilityNode.From(_sortBuffer[i], scale);

            Snapshot = snapshot;
            Changed?.Invoke();
        }

        private static readonly Comparison<ISkiaAccessibilityNode> RectComparer = (a, b) =>
        {
            var ra = a.GetAccessibilityPixelRect();
            var rb = b.GetAccessibilityPixelRect();
            var cmp = ra.Top.CompareTo(rb.Top);
            return cmp != 0 ? cmp : ra.Left.CompareTo(rb.Left);
        };

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
