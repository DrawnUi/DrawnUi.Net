using System.Collections.Concurrent;
using DrawnUi.Draw;

namespace DrawnUi.Views
{
    public record AccessibilityNode(string? Label, string? Hint, string? Role, SKRect Rect)
    {
        internal SkiaControl? Source { get; init; }

        internal static AccessibilityNode From(SkiaControl control, float scale)
        {
            var px = control.VisualLayer?.HitBoxWithTransforms.Pixels ?? control.DrawingRect;
            return new AccessibilityNode(
                control.AccessibilityLabel,
                control.AccessibilityHint,
                control.AccessibilityRole,
                new SKRect(px.Left / scale, px.Top / scale, px.Right / scale, px.Bottom / scale))
            {
                Source = control
            };
        }
    }

    public class SkiaAccessibilityManager
    {
        private readonly ConcurrentDictionary<SkiaControl, byte> _nodes = new();
        private readonly List<SkiaControl> _sortBuffer = new();
        private volatile bool _dirty;
        private long _lastRebuildTick;

        /// <summary>
        /// Minimum milliseconds between snapshot rebuilds. Default 1000ms.
        /// </summary>
        public long MinUpdateIntervalMs { get; set; } = 1000;

        public AccessibilityNode[] Snapshot { get; private set; } = [];

        public event Action? Changed;

        public void Register(SkiaControl control)
        {
            _nodes.TryAdd(control, 0);
            _dirty = true;
        }

        public void NotifyUpdated(SkiaControl control)
        {
            if (!_nodes.ContainsKey(control)) return;
            if (!_dirty) _dirty = true;
        }

        public void Unregister(SkiaControl control)
        {
            if (_nodes.TryRemove(control, out _))
                _dirty = true;
        }

        public void UnregisterSubtree(SkiaControl root)
        {
            bool any = false;
            foreach (var key in _nodes.Keys)
            {
                if (IsDescendantOrSelf(key, root) && _nodes.TryRemove(key, out _))
                    any = true;
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
            foreach (var control in _nodes.Keys)
            {
                if (control.IsVisible && !control.IsDisposed)
                    _sortBuffer.Add(control);
            }

            _sortBuffer.Sort(RectComparer);

            var snapshot = new AccessibilityNode[_sortBuffer.Count];
            for (int i = 0; i < _sortBuffer.Count; i++)
                snapshot[i] = AccessibilityNode.From(_sortBuffer[i], scale);

            Snapshot = snapshot;
            Changed?.Invoke();
        }

        private static readonly Comparison<SkiaControl> RectComparer = (a, b) =>
        {
            var ra = a.VisualLayer?.HitBoxWithTransforms.Pixels ?? a.DrawingRect;
            var rb = b.VisualLayer?.HitBoxWithTransforms.Pixels ?? b.DrawingRect;
            var cmp = ra.Top.CompareTo(rb.Top);
            return cmp != 0 ? cmp : ra.Left.CompareTo(rb.Left);
        };

        private static bool IsDescendantOrSelf(SkiaControl candidate, SkiaControl ancestor)
        {
            IDrawnBase? current = candidate;
            while (current is SkiaControl c)
            {
                if (ReferenceEquals(c, ancestor)) return true;
                current = c.Parent;
            }
            return false;
        }
    }
}
