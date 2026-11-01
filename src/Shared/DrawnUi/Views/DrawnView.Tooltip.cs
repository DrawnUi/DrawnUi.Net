namespace DrawnUi.Views
{
    public partial class DrawnView
    {
        #region Tooltip

        // State shares _lockHover with hover: the pointer pass reports, CommitHover decides.
        private readonly List<SkiaControl> _tooltipReporters = new();
        private SkiaControl _tooltipOwner, _tooltipSuppressed;
        private string _tooltipText, _tooltipShownText;
        private SKPoint _tooltipPointer, _tooltipCursor; // the pointer now, and where it was when the tooltip showed
        private SKRect _tooltipAnchor; // the control the tooltip belongs to, canvas pixels
        private long _tooltipClosedAt;
        private int _tooltipGeneration;
        private bool _tooltipKeysHooked;

        // drawing thread only
        private SkiaControl _tooltipView;
        private string _tooltipViewText;
        private SKRect _tooltipViewRect;
        private bool _tooltipNativeShown;

        /// <summary>
        /// Look and timing of the tooltips on this canvas; null (default) uses <see cref="Super.TooltipStyle"/>.
        /// </summary>
        public TooltipStyle TooltipStyle { get; set; }

        /// <summary>
        /// Set by heads that show tooltips in a window of their own (they can go past the edge of the app window).
        /// Null: the canvas draws them itself, inside its bounds.
        /// </summary>
        public ITooltipHost TooltipHost { get; set; }

        /// <summary>The text of the tooltip shown now, null when none is shown.</summary>
        public string ShownTooltip => _tooltipShownText;

        /// <summary>Where the control is on the canvas now, in pixels: through caches, scroll offsets and transforms.</summary>
        static SKRect OnScreenRect(SkiaControl control)
        {
            var rect = control.GetAccessibilityPixelRect();
            return rect.IsEmpty ? control.HitBoxAuto : rect;
        }

        TooltipStyle TooltipStyleUsed => TooltipStyle ?? Super.TooltipStyle ?? new TooltipStyle();

        /// <summary>
        /// A control with tooltip text was reached by the mouse pass being processed; called by
        /// <see cref="SkiaControl.ProcessGestures"/>. The innermost one with text wins when the pass is committed.
        /// </summary>
        public void ReportTooltip(SkiaControl control)
        {
            lock (_lockHover)
            {
                _tooltipReporters.Add(control);
            }
        }

        /// <summary>A mouse pass was committed (under <see cref="_lockHover"/>): pick the tooltip control under the pointer.</summary>
        bool CommitTooltipLocked(SkiaGesturesParameters pointer)
        {
            SkiaControl owner = null;
            string text = null;
            for (var i = _tooltipReporters.Count - 1; i >= 0; i--)
            {
                var control = _tooltipReporters[i];
                if (control.IsDisposed || control.IsDisposing)
                    continue;
                text = control.GetTooltipText();
                if (!string.IsNullOrEmpty(text))
                {
                    owner = control;
                    break;
                }
            }

            _tooltipReporters.Clear();

            // a press closed it: it stays closed until the mouse leaves that control
            if (_tooltipSuppressed != null)
            {
                if (owner == _tooltipSuppressed)
                    return false;
                _tooltipSuppressed = null;
            }

            if (owner == null)
            {
                _tooltipOwner = null;
                _tooltipText = null;
                return HideTooltipLocked();
            }

            var location = pointer?.Event?.Location;
            _tooltipPointer = location != null ? new SKPoint(location.Value.X, location.Value.Y) : _tooltipPointer;

            if (owner == _tooltipOwner && text == _tooltipText)
                return false; // still on the same control: the shown tooltip stays where it opened

            _tooltipOwner = owner;
            _tooltipText = text;
            HookTooltipKeys(true);

            var style = TooltipStyleUsed;
            if (_tooltipShownText != null
                || (_tooltipClosedAt != 0 && Environment.TickCount64 - _tooltipClosedAt < style.FollowWindowMs))
            {
                // moving from one tooltip to the next: no new wait
                _tooltipGeneration++;
                _tooltipShownText = text;
                _tooltipAnchor = OnScreenRect(owner);
                _tooltipCursor = _tooltipPointer;
                return true;
            }

            var generation = ++_tooltipGeneration;
            Task.Delay(Math.Max(0, style.ShowDelayMs)).ContinueWith(_ =>
            {
                lock (_lockHover)
                {
                    if (generation != _tooltipGeneration || _tooltipOwner == null || IsDisposed)
                        return;
                    _tooltipShownText = _tooltipText;
                    _tooltipAnchor = OnScreenRect(_tooltipOwner);
                    _tooltipCursor = _tooltipPointer;
                }

                Update();
            });

            return false;
        }

        /// <summary>Closes the tooltip and cancels a pending one (under <see cref="_lockHover"/>). True when a redraw is needed.</summary>
        bool HideTooltipLocked()
        {
            _tooltipGeneration++;
            if (_tooltipOwner == null)
                HookTooltipKeys(false);

            if (_tooltipShownText == null)
                return false;

            _tooltipShownText = null;
            _tooltipClosedAt = Environment.TickCount64;
            return true;
        }

        /// <summary>
        /// Closes the tooltip; it does not come back until the mouse moves onto another control. Heads call it for any
        /// input that is not a plain mouse move (press, wheel, key).
        /// </summary>
        public void CloseTooltipOnInput()
        {
            if (_tooltipOwner == null && _tooltipShownText == null)
                return;

            bool update;
            lock (_lockHover)
            {
                _tooltipSuppressed = _tooltipOwner;
                _tooltipOwner = null;
                _tooltipText = null;
                update = HideTooltipLocked();
                HookTooltipKeys(false);
            }

            if (update)
                Update();
        }

        /// <summary>Closes the tooltip and forgets its control (pointer left, content moves, control went away).</summary>
        void ResetTooltipLocked()
        {
            _tooltipReporters.Clear();
            _tooltipOwner = null;
            _tooltipText = null;
            _tooltipSuppressed = null;
            if (HideTooltipLocked())
                Update();
            HookTooltipKeys(false);
        }

        void HookTooltipKeys(bool on)
        {
            if (on == _tooltipKeysHooked)
                return;

            _tooltipKeysHooked = on;
            if (on)
                KeyboardManager.KeyDown += OnTooltipKeyDown;
            else
                KeyboardManager.KeyDown -= OnTooltipKeyDown;
        }

        void OnTooltipKeyDown(object sender, InputKey key)
        {
            CloseTooltipOnInput();
        }

        /// <summary>Draws the tooltip on top of everything, or hands it to the <see cref="TooltipHost"/>.</summary>
        void DrawTooltip(DrawingContext context)
        {
            string text;
            SKRect anchor;
            SKPoint cursor;
            lock (_lockHover)
            {
                text = _tooltipShownText;
                anchor = _tooltipAnchor;
                cursor = _tooltipCursor;
            }

            if (text == null)
            {
                if (_tooltipNativeShown)
                {
                    _tooltipNativeShown = false;
                    TooltipHost?.HideTooltip();
                }

                _tooltipViewText = null;
                return;
            }

            var scale = context.Scale;
            var host = TooltipHost;

            if (_tooltipView == null || _tooltipViewText != text)
            {
                var previous = _tooltipView;
                var style = TooltipStyleUsed;
                _tooltipView = style.CreateTooltip?.Invoke(text) ?? style.CreateDefault(text);
                _tooltipView.IsParentIndependent = true;
                _tooltipViewText = text;

                previous?.Dispose();

                // a finite width lets long text wrap: MaxWidth, never wider than the canvas
                var maxWidth = Math.Min(context.Destination.Width,
                    (float)((style.MaxWidth + _tooltipView.Margin.HorizontalThickness) * scale));
                var measured = _tooltipView.Measure(maxWidth, float.PositiveInfinity, scale);
                var width = measured.Pixels.Width;
                var height = measured.Pixels.Height;

                // Attached to its control: centered above it, below it where there is no room. AtCursor: below and
                // to the right of the pointer where it rested, above it where there is no room. Either way it stays put
                // while the pointer moves over the control, and OffsetX / OffsetY shift it (the vertical offset is
                // mirrored on the flipped side). The view's margin (room for a shadow) is not part of the box.
                var margin = _tooltipView.Margin;
                var boxWidth = width - (float)margin.HorizontalThickness * scale;
                var boxHeight = height - (float)margin.VerticalThickness * scale;
                var gap = 6 * scale;
                var offsetX = (float)style.OffsetX * scale;
                var offsetY = (float)style.OffsetY * scale;

                float boxLeft, boxTop, flippedTop;
                if (style.AtCursor)
                {
                    boxLeft = cursor.X + 4 * scale + offsetX;
                    boxTop = cursor.Y + 20 * scale + offsetY; // clear of the pointer's arrow
                    flippedTop = cursor.Y - gap - boxHeight - offsetY;
                }
                else
                {
                    boxLeft = anchor.MidX - boxWidth / 2 + offsetX;
                    boxTop = anchor.Top - gap - boxHeight + offsetY;
                    flippedTop = anchor.Bottom + gap - offsetY;
                }

                if (host == null)
                {
                    var bounds = context.Destination;
                    if (style.AtCursor ? boxTop + boxHeight > bounds.Bottom : boxTop < bounds.Top)
                        boxTop = flippedTop; // no room on its side: the other side
                    boxLeft = Math.Max(bounds.Left, Math.Min(boxLeft, bounds.Right - boxWidth));
                    boxTop = Math.Max(bounds.Top, Math.Min(boxTop, bounds.Bottom - boxHeight));
                }

                var left = boxLeft - (float)margin.Left * scale;
                var top = boxTop - (float)margin.Top * scale;

                _tooltipViewRect = new SKRect(left, top, left + width, top + height);

                if (host != null)
                {
                    using var surface = SKSurface.Create(new SKImageInfo((int)Math.Ceiling(width), (int)Math.Ceiling(height)));
                    if (surface != null)
                    {
                        var offscreen = context.CreateForRecordingImage(surface, new SKSize(width, height));
                        _tooltipView.Render(offscreen.WithDestination(new SKRect(0, 0, width, height)));
                        surface.Canvas.Flush();
                        host.ShowTooltip(surface.Snapshot(), new SKPoint(left, top), scale);
                        _tooltipNativeShown = true;
                    }
                }
            }

            if (host == null)
            {
                _tooltipView.Render(context.WithDestination(_tooltipViewRect));
            }
        }

        #endregion
    }
}
