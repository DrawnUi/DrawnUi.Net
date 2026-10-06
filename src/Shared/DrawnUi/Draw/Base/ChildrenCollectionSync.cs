using System.Collections.Specialized;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Applies a change of a <c>Children</c> collection to its parent's <see cref="IDrawnBase.Views"/> for every
    /// collection action, so what is drawn follows the collection: the same children, in the same order.
    /// Internal subviews a control adds by itself (not through <c>Children</c>) keep their place.
    /// </summary>
    internal static class ChildrenCollectionSync
    {
        /// <summary>
        /// Returns true when the order of <paramref name="views"/> changed; the caller then invalidates its views
        /// list and layout.
        /// </summary>
        public static bool Apply(List<SkiaControl> views, IList<SkiaControl> children, NotifyCollectionChangedEventArgs e,
            Action<SkiaControl> attach, Action<SkiaControl> detach)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (SkiaControl child in e.NewItems)
                    {
                        if (child != null)
                            attach(child);
                    }

                    // an insert, not an append, takes its place in the drawing order
                    return e.NewStartingIndex >= 0 && e.NewStartingIndex + e.NewItems.Count < children.Count
                           && OrderViews(views, children);

                case NotifyCollectionChangedAction.Remove:
                    foreach (SkiaControl child in e.OldItems)
                    {
                        if (child != null)
                            detach(child);
                    }

                    return false;

                case NotifyCollectionChangedAction.Replace:
                    foreach (SkiaControl child in e.OldItems)
                    {
                        if (child != null)
                            detach(child);
                    }

                    foreach (SkiaControl child in e.NewItems)
                    {
                        if (child != null)
                            attach(child);
                    }

                    return OrderViews(views, children);

                case NotifyCollectionChangedAction.Move:
                    return OrderViews(views, children);

                case NotifyCollectionChangedAction.Reset:
                    // ObservableCollection.Clear and range collections send Reset without the old items: drop the
                    // children the collection no longer holds, add the ones it gained, then follow its order.
                    var keep = new HashSet<SkiaControl>();
                    foreach (var child in children)
                    {
                        if (child != null)
                            keep.Add(child);
                    }

                    var present = new HashSet<SkiaControl>();
                    foreach (var view in views.ToArray())
                    {
                        if (view.IsChildrenItem && !keep.Contains(view))
                            detach(view);
                        else
                            present.Add(view);
                    }

                    foreach (var child in keep)
                    {
                        if (!present.Contains(child))
                            attach(child);
                    }

                    return OrderViews(views, children);
            }

            return false;
        }

        /// <summary>
        /// Puts the children found in <paramref name="views"/> in collection order, inside the slots they already
        /// occupy, so other subviews keep their place. Returns true when something moved.
        /// </summary>
        static bool OrderViews(List<SkiaControl> views, IList<SkiaControl> children)
        {
            if (views.Count < 2)
                return false;

            var inViews = new HashSet<SkiaControl>(views);
            var ordered = new List<SkiaControl>(children.Count);
            foreach (var child in children)
            {
                if (child != null && inViews.Remove(child))
                    ordered.Add(child);
            }

            if (ordered.Count < 2)
                return false;

            var isChild = new HashSet<SkiaControl>(ordered);
            var changed = false;
            var next = 0;
            for (var i = 0; i < views.Count && next < ordered.Count; i++)
            {
                if (!isChild.Contains(views[i]))
                    continue;

                if (!ReferenceEquals(views[i], ordered[next]))
                {
                    views[i] = ordered[next];
                    changed = true;
                }

                next++;
            }

            return changed;
        }
    }
}
