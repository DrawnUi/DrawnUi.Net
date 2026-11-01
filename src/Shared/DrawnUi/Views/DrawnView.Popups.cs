namespace DrawnUi.Views
{
    public partial class DrawnView
    {
        #region Popup windows

        private PopupLayer _popupLayer;
        private int _popupNextId;
        private bool _popupKeysHooked;

        /// <summary>
        /// Shows <paramref name="content"/> over everything, opening from <paramref name="anchor"/> the way native menus
        /// do: at a point (where a right click was), or below a rect or a control (a dropdown), flipped to the other
        /// side where it does not fit. It closes with a press outside it, Escape, <see cref="ClosePopupWindow"/>, and
        /// with the options. Same API as DrawnUI for Rust's <c>Cx::open_popup_window</c>.
        /// </summary>
        /// <param name="content">What the popup shows, a menu for example. Sized to what it measures to, unless
        /// <see cref="PopupWindowOptions.Size"/> is set.</param>
        /// <param name="anchor">Where it opens from, in canvas points, or a control.</param>
        /// <param name="options">How it behaves; null for the defaults.</param>
        public PopupId OpenPopupWindow(SkiaControl content, PopupAnchor anchor, PopupWindowOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(content);
            options ??= new PopupWindowOptions();

            if (_popupLayer == null || _popupLayer.IsDisposed || !Views.Contains(_popupLayer))
            {
                _popupLayer = new PopupLayer(this);
                AddSubView(_popupLayer);
            }

            var popup = new PopupWindow(new PopupId(++_popupNextId), content, anchor, options);
            _popupLayer.Open(popup);
            HookPopupKeys(true);

            if (!options.Passive)
            {
                lock (_lockHover)
                    ResetTooltipLocked(); // a menu opening closes the tooltip
            }

            Update();
            return popup.Id;
        }

        /// <summary>Closes the popup window (nothing when it is closed already).</summary>
        public void ClosePopupWindow(PopupId id)
        {
            _popupLayer?.Close(id);
            AfterPopupsChanged();
        }

        /// <summary>Closes every popup window.</summary>
        public void ClosePopupWindows()
        {
            _popupLayer?.CloseAll(passiveToo: true);
            AfterPopupsChanged();
        }

        /// <summary>
        /// The app's window stopped being the active window: its popup windows and its tooltip close, as native menus
        /// do. The desktop heads call it from their window's deactivation.
        /// </summary>
        public void OnWindowDeactivated()
        {
            ClosePopupWindows();
            CloseTooltipOnInput();
        }

        /// <summary>
        /// Heads call it for every input before routing it: the wheel outside the popup windows closes them (what
        /// they opened from is about to move). Done here, before the gesture owner of a scroll can take the wheel.
        /// </summary>
        public void PreviewInputForPopups(SkiaGesturesParameters args)
        {
            if (args.Type != TouchActionResult.Wheel || _popupLayer == null || !_popupLayer.HasInteractive)
                return;

            var location = args.Event.Location;
            if (_popupLayer.InsideInteractive(new SKPoint(location.X, location.Y)))
                return;

            _popupLayer.CloseAll(passiveToo: false);
            AfterPopupsChanged();
        }

        /// <summary>
        /// A popup window closed: picked, a press outside it, Escape, the wheel, the app's window deactivated or
        /// <see cref="ClosePopupWindow"/>. A menu bar uses it to unselect its title.
        /// </summary>
        public event EventHandler<PopupId> PopupWindowClosed;

        internal void RaisePopupWindowClosed(PopupId id) => PopupWindowClosed?.Invoke(this, id);

        /// <summary>The popup window is still open.</summary>
        public bool IsPopupWindowOpen(PopupId id) => _popupLayer?.IsOpen(id) == true;

        /// <summary>True while a popup window that takes input is open.</summary>
        public bool HasPopupWindows => _popupLayer?.HasInteractive == true;

        /// <summary>The content of the top popup window that takes input, null when none is open.</summary>
        public SkiaControl TopPopupWindow => _popupLayer?.TopInteractive;

        internal void AfterPopupsChanged()
        {
            if (_popupLayer == null || !_popupLayer.HasAny)
                HookPopupKeys(false);
            Update();
        }

        void HookPopupKeys(bool on)
        {
            if (on == _popupKeysHooked)
                return;

            _popupKeysHooked = on;
            if (on)
                KeyboardManager.KeyDown += OnPopupKeyDown;
            else
                KeyboardManager.KeyDown -= OnPopupKeyDown;
        }

        void OnPopupKeyDown(object sender, InputKey key)
        {
            // Escape closes the top popup window (native menus)
            if (key == InputKey.Escape && _popupLayer?.CloseTop() == true)
                AfterPopupsChanged();
        }

        #endregion
    }

    /// <summary>A popup window opened with <see cref="DrawnView.OpenPopupWindow"/>.</summary>
    public readonly record struct PopupId(int Value);

    /// <summary>Where a popup window opens from (<see cref="DrawnView.OpenPopupWindow"/>), in canvas points.</summary>
    public readonly struct PopupAnchor
    {
        private PopupAnchor(SKRect rect, bool isPoint, SkiaControl control)
        {
            Rect = rect;
            IsPoint = isPoint;
            Control = control;
        }

        /// <summary>The point or the rect, in canvas points (empty for a control).</summary>
        public SKRect Rect { get; }

        /// <summary>A point: the popup's top-left corner is there; where the canvas ends it opens to the left of the point, or above it.</summary>
        public bool IsPoint { get; }

        /// <summary>A control: the popup opens below the rect the control was last drawn at, as for a rect.</summary>
        public SkiaControl Control { get; }

        /// <summary>A point (where a right click was), in canvas points.</summary>
        public static PopupAnchor AtPoint(SKPoint point) => new(new SKRect(point.X, point.Y, point.X, point.Y), true, null);

        /// <summary>A rect (the button a dropdown belongs to), in canvas points: the popup opens below it, left edges together; where the canvas ends, above it or right edges together.</summary>
        public static PopupAnchor AtRect(SKRect rect) => new(rect, false, null);

        /// <summary>A control: as a rect, with the rect it was last drawn at (moved, scaled, scrolled).</summary>
        public static PopupAnchor AtControl(SkiaControl control) => new(SKRect.Empty, false, control);

        public static implicit operator PopupAnchor(SKPoint point) => AtPoint(point);
        public static implicit operator PopupAnchor(SKRect rect) => AtRect(rect);
        public static implicit operator PopupAnchor(SkiaControl control) => AtControl(control);

        /// <summary>The anchor in canvas pixels.</summary>
        internal SKRect Pixels(float scale)
        {
            if (Control != null)
            {
                var rect = Control.GetAccessibilityPixelRect(); // on screen now: through caches and scroll offsets
                return rect.IsEmpty ? Control.HitBoxAuto : rect;
            }
            return new SKRect(Rect.Left * scale, Rect.Top * scale, Rect.Right * scale, Rect.Bottom * scale);
        }
    }

    /// <summary>
    /// How a popup window behaves (<see cref="DrawnView.OpenPopupWindow"/>). The defaults: sized to its content, closed
    /// by a press outside it, Escape, or the app. Same fields as DrawnUI for Rust's <c>PopupWindowOptions</c>.
    /// </summary>
    public class PopupWindowOptions
    {
        /// <summary>Its size in points; null: the size its content measures to. Never larger than the canvas.</summary>
        public SKSize? Size { get; set; }

        /// <summary>It also closes once the mouse has left it and the canvas for 300 ms.</summary>
        public bool CloseOnPointerLeave { get; set; }

        /// <summary>Drawn on the canvas, inside it, also where the head can give popups windows of their own.</summary>
        public bool OnCanvas { get; set; }

        /// <summary>A window of its own is drawn on the CPU (a layered window on Windows). For small popups.</summary>
        public bool Cpu { get; set; }

        /// <summary>
        /// Shows only, as a tooltip: takes no input, has no accessibility node, is not the popup Escape closes, and a
        /// press outside it is not kept from the controls below.
        /// </summary>
        public bool Passive { get; set; }
    }

    internal sealed class PopupWindow
    {
        public PopupWindow(PopupId id, SkiaControl root, PopupAnchor anchor, PopupWindowOptions options)
        {
            Id = id;
            Root = root;
            Anchor = anchor;
            Options = options;
        }

        public PopupId Id { get; }
        public SkiaControl Root { get; }
        public PopupAnchor Anchor { get; }
        public PopupWindowOptions Options { get; }
    }

    /// <summary>
    /// The canvas' top layer holding its popup windows: draws them over everything, routes input to them first, and
    /// closes them on a press outside (that press goes nowhere, as with native menus).
    /// </summary>
    internal sealed class PopupLayer : SkiaLayer
    {
        private readonly DrawnView _canvas;
        private readonly List<PopupWindow> _popups = new();
        private bool _swallowPress;

        public PopupLayer(DrawnView canvas)
        {
            _canvas = canvas;
            ZIndex = int.MaxValue - 1;
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Fill;
            IsVisible = false;
        }

        public bool HasAny => _popups.Count > 0;

        public bool HasInteractive => _popups.Any(p => !p.Options.Passive);

        public SkiaControl TopInteractive => _popups.LastOrDefault(p => !p.Options.Passive)?.Root;

        public bool IsOpen(PopupId id) => _popups.Any(p => p.Id == id);

        public void Open(PopupWindow popup)
        {
            var root = popup.Root;
            root.HorizontalOptions = LayoutOptions.Start;
            root.VerticalOptions = LayoutOptions.Start;
            if (popup.Options.Size is { } size)
            {
                root.WidthRequest = size.Width;
                root.HeightRequest = size.Height;
            }
            if (popup.Options.Passive)
            {
                root.InputTransparent = true;
                root.AccessibilityRole ??= Aria.RolePresentation;
            }

            root.Opacity = 0; // shown once placed, no flash at the corner
            _popups.Add(popup);
            AddSubView(root);
            IsVisible = true;
            Invalidate();
        }

        public void Close(PopupId id)
        {
            var popup = _popups.FirstOrDefault(p => p.Id == id);
            if (popup != null)
                Remove(popup);
        }

        public bool CloseTop()
        {
            var top = _popups.LastOrDefault(p => !p.Options.Passive);
            if (top == null)
                return false;
            Remove(top);
            return true;
        }

        public void CloseAll(bool passiveToo)
        {
            foreach (var popup in _popups.ToList())
            {
                if (passiveToo || !popup.Options.Passive)
                    Remove(popup);
            }
        }

        private void Remove(PopupWindow popup)
        {
            _popups.Remove(popup);
            RemoveSubView(popup.Root);
            _canvas.DisposeObject(popup.Root);
            IsVisible = _popups.Count > 0 || _swallowPress;
            Invalidate();
            _canvas.RaisePopupWindowClosed(popup.Id);
        }

        protected override void Paint(DrawingContext ctx)
        {
            // place each popup from its anchor and its measured size, in the canvas' pixels
            var bounds = DrawingRect;
            var scale = ctx.Scale;
            foreach (var popup in _popups)
            {
                var root = popup.Root;
                var size = root.MeasuredSize.Pixels;
                if (size.Width <= 0 || size.Height <= 0)
                    continue;

                var anchor = popup.Anchor.Pixels(scale);
                var rect = Place(anchor, popup.Anchor.IsPoint, new SKSize(size.Width, size.Height), bounds);
                var x = (rect.Left - bounds.Left) / scale;
                var y = (rect.Top - bounds.Top) / scale;
                if (Math.Abs(root.TranslationX - x) > 0.01 || Math.Abs(root.TranslationY - y) > 0.01)
                {
                    root.TranslationX = x;
                    root.TranslationY = y;
                }
                if (root.Opacity < 1)
                    root.Opacity = 1;
            }

            base.Paint(ctx);
        }

        /// <summary>
        /// Where a popup of <paramref name="size"/> goes for <paramref name="anchor"/> (both pixels) inside
        /// <paramref name="bounds"/>: at the anchor's corner, flipped to its other side where it does not fit, then kept
        /// inside. Same rule as DrawnUI for Rust's <c>place_popup</c>.
        /// </summary>
        internal static SKRect Place(SKRect anchor, bool point, SKSize size, SKRect bounds)
        {
            var w = Math.Max(0, Math.Min(size.Width, bounds.Width));
            var h = Math.Max(0, Math.Min(size.Height, bounds.Height));
            var left = anchor.Left;
            if (left + w > bounds.Right)
                left = point ? anchor.Left - w : anchor.Right - w;
            var top = anchor.Bottom;
            if (top + h > bounds.Bottom)
                top = anchor.Top - h;
            left = MathF.Round(Math.Clamp(left, bounds.Left, bounds.Right - w));
            top = MathF.Round(Math.Clamp(top, bounds.Top, bounds.Bottom - h));
            return new SKRect(left, top, left + MathF.Round(w), top + MathF.Round(h));
        }

        internal bool InsideInteractive(SKPoint point)
        {
            foreach (var popup in _popups)
            {
                if (!popup.Options.Passive && popup.Root.HitIsInside(point.X, point.Y))
                    return true;
            }

            return false;
        }

        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {
            if (args.Type == TouchActionResult.Down)
                _swallowPress = false;

            // the rest of a press that closed the popups goes nowhere
            if (_swallowPress)
                return args.Type == TouchActionResult.Pointer ? null : this;

            if (!HasInteractive)
            {
                IsVisible = HasAny;
                return null;
            }

            var location = args.Event.Location;
            if (InsideInteractive(new SKPoint(location.X, location.Y)))
            {
                // over a popup: its controls take it, and nothing below sees it
                base.ProcessGestures(args, apply);
                return this;
            }

            switch (args.Type)
            {
                case TouchActionResult.Down:
                    CloseAll(passiveToo: false);
                    _swallowPress = true;
                    IsVisible = true;
                    _canvas.AfterPopupsChanged();
                    return this;

                case TouchActionResult.ContextMenu:
                    // a right click elsewhere closes the menu; the request goes on, so a new menu can open there
                    CloseAll(passiveToo: false);
                    _canvas.AfterPopupsChanged();
                    return null;
            }

            return null;
        }
    }
}
