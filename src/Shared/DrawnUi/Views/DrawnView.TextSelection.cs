using AppoMobi.Gestures;

namespace DrawnUi.Views
{
    public partial class DrawnView
    {
        #region Page text selection

        // Page text selection with the mouse, as in browsers (drawnui-cross): a main-button drag that starts on no
        // interactive control selects the selectable labels (SkiaLabel.AccessibilityTextSelectable) it crosses, in
        // document order, the first and last partly. Idle cost: one null check per gesture and per frame.

        private sealed class TextDrag
        {
            public List<SkiaLabel> Labels = null!;
            public SkiaLabel StartLabel = null!;
            public int StartIndex;
            public SkiaScroll? Scroll;
            public SKPoint Pointer;
            public bool Moved;
        }

        private TextDrag? _textDrag;
        private readonly List<SkiaLabel> _pageSelection = new();
        private bool _pageSelectionKeys;

        /// <summary>
        /// The text selected on the page: the labels a mouse drag selected, joined with new lines, in page order; else
        /// the selection of the label in focus. Empty when nothing is selected.
        /// </summary>
        public string SelectedText
        {
            get
            {
                if (_pageSelection.Count > 0)
                    return string.Join("\n", _pageSelection.Select(l => l.SelectedText).Where(t => !string.IsNullOrEmpty(t)));
                return (FocusedChild as SkiaLabel)?.SelectedText ?? string.Empty;
            }
        }

        /// <summary>True while a mouse drag selected text on the page (one label or more): the page, not a label, owns Ctrl+C.</summary>
        internal bool OwnsTextSelectionKeys => _pageSelection.Any(l => l.SelectionLength > 0);

        /// <summary>Puts <see cref="SelectedText"/> on the clipboard through <see cref="Super.SetClipboardText"/>. False when nothing is selected.</summary>
        public bool CopyTextSelection()
        {
            var text = SelectedText;
            if (string.IsNullOrEmpty(text))
                return false;
            Super.SetClipboardText?.Invoke(text);
            return true;
        }

        /// <summary>Drops the page text selection (every label a drag selected).</summary>
        public void ClearTextSelection()
        {
            foreach (var label in _pageSelection)
                label.ClearSelection();
            _pageSelection.Clear();
            AttachPageSelectionKeys(false);
        }

        private bool InsidePopup(SkiaGesturesParameters args) =>
            _popupLayer is { HasInteractive: true } && _popupLayer.InsideInteractive(new SKPoint(args.Event.Location.X, args.Event.Location.Y));

        private static bool IsMouse(SkiaGesturesParameters args) =>
            args.Event?.Pointer is { DeviceType: PointerDeviceType.Mouse };

        private static bool IsMainButton(SkiaGesturesParameters args) =>
            args.Event?.Pointer is not { Button: not AppoMobi.Gestures.MouseButton.Left };

        /// <summary>
        /// Before a gesture goes to the controls: a mouse drag that is selecting text keeps its moves (a scroll under it
        /// does not scroll), a main-button press drops the previous page selection. True when the gesture was used here.
        /// </summary>
        internal bool PreviewTextSelectionGesture(SkiaGesturesParameters args)
        {
            switch (args.Type)
            {
                case TouchActionResult.Down when IsMouse(args) && IsMainButton(args):
                    _textDrag = null;
                    // a press in an open popup (the app's Copy menu) keeps the selection for what it does
                    if (_pageSelection.Count > 0 && !InsidePopup(args))
                        ClearTextSelection();
                    return false;

                case TouchActionResult.Panning when _textDrag != null:
                    _textDrag.Pointer = new SKPoint(args.Event.Location.X, args.Event.Location.Y);
                    _textDrag.Moved = true;
                    UpdateTextDrag(_textDrag);
                    Update();
                    return true;

                case TouchActionResult.Up when _textDrag != null:
                    if (_textDrag.Moved)
                        UpdateTextDrag(_textDrag);
                    _textDrag = null;
                    return false;
            }

            return false;
        }

        /// <summary>
        /// After a mouse main-button press went to the controls: when it landed on no interactive control (empty space, a
        /// scroll's empty area, a selectable label), a drag from here selects text. Not inside a scroll that holds no
        /// selectable text: that one keeps scrolling by mouse drag.
        /// </summary>
        internal void AfterTextSelectionDown(SkiaGesturesParameters args, ISkiaGestureListener? consumed)
        {
            if (args.Type != TouchActionResult.Down || !IsMouse(args) || !IsMainButton(args) || InsidePopup(args))
                return;
            if (consumed != null && consumed is not SkiaScroll && consumed is not SkiaLabel { AccessibilityTextSelectable: true })
                return;
            // a press on a draggable scroll bar drags the bar
            if (consumed is SkiaScroll { ScrollBarOwnsGesture: true })
                return;

            var labels = new List<SkiaLabel>();
            foreach (var root in Views)
                CollectSelectable(root, labels);
            if (labels.Count == 0)
                return;

            var point = new SKPoint(args.Event.Location.X, args.Event.Location.Y);
            var scroll = consumed as SkiaScroll ?? ScrollOf(consumed as SkiaControl) ?? ScrollAt(labels, point);
            if (scroll != null && !labels.Any(l => IsInside(l, scroll)))
                return;

            var (label, index) = Locate(labels, point);
            if (label == null)
                return;

            _textDrag = new TextDrag { Labels = labels, StartLabel = label, StartIndex = index, Scroll = scroll, Pointer = point };
        }

        /// <summary>Frame end: while a drag holds the pointer past a scroll's top or bottom edge, the scroll follows it.</summary>
        internal void ProcessTextSelectionFrame()
        {
            var drag = _textDrag;
            if (drag?.Scroll == null || !drag.Moved)
                return;

            var view = drag.Scroll.GetAccessibilityPixelRect();
            if (view.IsEmpty)
                return;

            float past = 0;
            if (drag.Pointer.Y > view.Bottom)
                past = drag.Pointer.Y - view.Bottom;
            else if (drag.Pointer.Y < view.Top)
                past = drag.Pointer.Y - view.Top;
            if (past == 0)
                return;

            // faster the farther past the edge, as browsers do
            var step = Math.Clamp(Math.Abs(past) * 0.25f, 2f, 40f) / RenderingScale * Math.Sign(past);
            var offset = drag.Scroll.InternalViewportOffset.Units;
            drag.Scroll.ScrollTo(offset.X, offset.Y - step, 0, true);
            UpdateTextDrag(drag);
            Update();
        }

        private void UpdateTextDrag(TextDrag drag)
        {
            var (endLabel, endIndex) = Locate(drag.Labels, drag.Pointer);
            if (endLabel == null)
                return;

            var a = drag.Labels.IndexOf(drag.StartLabel);
            var b = drag.Labels.IndexOf(endLabel);
            if (a < 0 || b < 0)
                return;

            int fromLabel, fromIndex, toLabel, toIndex;
            if (a < b || (a == b && drag.StartIndex <= endIndex))
                (fromLabel, fromIndex, toLabel, toIndex) = (a, drag.StartIndex, b, endIndex);
            else
                (fromLabel, fromIndex, toLabel, toIndex) = (b, endIndex, a, drag.StartIndex);

            foreach (var label in _pageSelection)
            {
                var at = drag.Labels.IndexOf(label);
                if (at < fromLabel || at > toLabel)
                    label.ClearSelection();
            }
            _pageSelection.Clear();

            for (var i = fromLabel; i <= toLabel; i++)
            {
                var label = drag.Labels[i];
                var length = label.SelectableLength;
                var start = i == fromLabel ? fromIndex : 0;
                var end = i == toLabel ? toIndex : length;
                label.Select(start, Math.Max(0, end - start));
                _pageSelection.Add(label);
            }

            AttachPageSelectionKeys(OwnsTextSelectionKeys);
        }

        /// <summary>
        /// The text boundary a canvas point stands for: inside a drawn label, its text index there; elsewhere the label
        /// seen nearest to it (by distance to its drawn rect, so a press in the right column of a page never lands on a
        /// label of the left one), at its start when the point is above it or before it on its line, else its end.
        /// </summary>
        private static (SkiaLabel? Label, int Index) Locate(List<SkiaLabel> labels, SKPoint point)
        {
            SkiaLabel? nearest = null;
            var nearestRect = SKRect.Empty;
            var best = float.MaxValue;
            foreach (var label in labels)
            {
                var r = label.GetAccessibilityPixelRect();
                if (r.IsEmpty)
                    continue;
                if (r.Contains(point))
                    return (label, label.TextIndexAtCanvas(point));
                var dx = MathF.Max(MathF.Max(r.Left - point.X, 0), point.X - r.Right);
                var dy = MathF.Max(MathF.Max(r.Top - point.Y, 0), point.Y - r.Bottom);
                var d = dx * dx + dy * dy;
                if (d < best)
                {
                    best = d;
                    nearest = label;
                    nearestRect = r;
                }
            }

            if (nearest == null)
                return (null, 0);
            var before = point.Y < nearestRect.Top || (point.Y <= nearestRect.Bottom && point.X < nearestRect.Left);
            return (nearest, before ? 0 : nearest.SelectableLength);
        }

        private static void CollectSelectable(SkiaControl control, List<SkiaLabel> into)
        {
            if (!control.IsVisible || control.IsDisposed)
                return;
            if (control is SkiaLabel { AccessibilityTextSelectable: true } label)
                into.Add(label);
            foreach (var child in control.Views)
                CollectSelectable(child, into);
        }

        private static SkiaScroll? ScrollOf(SkiaControl? control)
        {
            for (var p = control?.Parent as SkiaControl; p != null; p = p.Parent as SkiaControl)
            {
                if (p is SkiaScroll scroll)
                    return scroll;
            }
            return null;
        }

        /// <summary>The innermost drawn scroll around a selectable label whose viewport holds the point.</summary>
        private static SkiaScroll? ScrollAt(List<SkiaLabel> labels, SKPoint point)
        {
            foreach (var label in labels)
            {
                for (var p = label.Parent as SkiaControl; p != null; p = p.Parent as SkiaControl)
                {
                    if (p is SkiaScroll scroll && scroll.GetAccessibilityPixelRect().Contains(point))
                        return scroll;
                }
            }
            return null;
        }

        private static bool IsInside(SkiaControl control, SkiaControl ancestor)
        {
            for (var p = control.Parent as SkiaControl; p != null; p = p.Parent as SkiaControl)
            {
                if (ReferenceEquals(p, ancestor))
                    return true;
            }
            return false;
        }

        private void AttachPageSelectionKeys(bool attach)
        {
            if (attach == _pageSelectionKeys)
                return;
            _pageSelectionKeys = attach;
            if (attach)
                KeyboardManager.KeyDown += OnPageSelectionKey;
            else
                KeyboardManager.KeyDown -= OnPageSelectionKey;
        }

        private void OnPageSelectionKey(object? sender, InputKey key)
        {
            // Esc keeps the selection, as in browsers: a click elsewhere drops it
            if (KeyboardManager.IsKeyForOtherElement)
                return;
            if (!KeyboardManager.IsControlPressed && !KeyboardManager.IsMetaPressed)
                return;
            if (key == InputKey.KeyC)
                CopyTextSelection();
            else if (key == InputKey.KeyA && FocusedChild is SkiaLabel focused)
            {
                // as before: Ctrl+A selects all of the label in focus
                ClearTextSelection();
                focused.SelectAll();
            }
        }

        #endregion
    }
}
