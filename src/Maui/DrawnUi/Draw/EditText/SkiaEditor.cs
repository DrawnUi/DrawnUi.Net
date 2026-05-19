using AppoMobi.Specials;
using DrawnUi.Draw;
using System.Diagnostics;
using System.Windows.Input;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
    {
        public SkiaEditor()
        {
            CreateControl();
        }

        public override bool WillClipBounds => true;

        public override void OnWillDisposeWithChildren()
        {
            base.OnWillDisposeWithChildren();

            TextChanged = null;
            FocusChanged = null;
            TextSubmitted = null;
        }

        #region EVENTS

        public event EventHandler<string> TextChanged;

        public event EventHandler<bool> FocusChanged;

        public event EventHandler<string> TextSubmitted;

        #endregion

        #region CHILDREN

        protected SkiaScroll _scroll;
        protected SkiaLayout _contentLayer;
        protected SkiaEditorSelection _selectionControl;
        protected SkiaLabel _placeholderLabel;

        private void RecreateLabelIfNeeded()
        {
            if (_contentLayer == null || _selectionControl == null || Cursor == null)
                return;

            var oldLabel = Label;
            var newLabel = CreateLabel();

            if (ReferenceEquals(oldLabel, newLabel))
                return;

            Label = newLabel;
            _selectionControl.SourceLabel = Label;

            _contentLayer.ClearChildren();
            _contentLayer.AddSubView(Label);
            _contentLayer.AddSubView(_selectionControl);
            _contentLayer.AddSubView(Cursor);

            Cursor.Initialize(Label);
        }


        public virtual void UpdateLabel()
        {
            if (Label == null)
                return;

            var displayText = Text;
            if (IsPassword && !string.IsNullOrEmpty(displayText))
            {
                displayText = new string('\u2022', displayText.Length);
            }
            else if (IsMultiline && !string.IsNullOrEmpty(displayText) && displayText.EndsWith("\n", StringComparison.Ordinal))
            {
                // Preserve the visible trailing empty line for caret placement without mutating editor state.
                displayText += "\u200B";
            }

            Label.Text = displayText;
            //Debug.WriteLine($"Label Text: {Label.Text}");
            Label.FontFamily = FontFamily;
            Label.FontSize = FontSize;
            Label.TextColor = this.TextColor;
            Label.FontWeight = this.FontWeight;
            Label.FillGradient = this.TextGradient;

            Label.HorizontalTextAlignment = this.HorizontalTextAlignment;
            Label.VerticalTextAlignment = this.VerticalTextAlignment;
            Label.LineHeight = this.LineHeight;

            if (IsMultiline)
            {
                Label.HeightRequest = -1;
                Label.MaxLines = -1;
                if (_scroll != null)
                    _scroll.Orientation = ScrollOrientation.Vertical;
            }
            else
            {
                Label.HeightRequest = FontSize > 0 ? Math.Ceiling(FontSize * 1.2) : 20;
                Label.MaxLines = 1;
                if (_scroll != null)
                    _scroll.Orientation = ScrollOrientation.Horizontal;
            }

            Cursor.Color = this.CursorColor;

            UpdateCursorVisibility();
            UpdatePlaceholder();

            Invalidate();
        }

        protected virtual void UpdatePlaceholder()
        {
            if (_placeholderLabel == null)
                return;

            _placeholderLabel.Text = PlaceholderText;
            _placeholderLabel.TextColor = PlaceholderColor;
            _placeholderLabel.HorizontalTextAlignment = PlaceholderHorizontalAlignment;
            _placeholderLabel.FontFamily = FontFamily;
            _placeholderLabel.FontSize = FontSize;
            _placeholderLabel.FontWeight = FontWeight;
            _placeholderLabel.LineHeight = LineHeight;

            if (IsMultiline)
            {
                _placeholderLabel.HeightRequest = -1;
                _placeholderLabel.MaxLines = -1;
            }
            else
            {
                _placeholderLabel.HeightRequest = FontSize > 0 ? Math.Ceiling(FontSize * 1.2) : 20;
                _placeholderLabel.MaxLines = 1;
            }

            _placeholderLabel.IsVisible = string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(PlaceholderText);
        }

        protected virtual SkiaLabel OnCreatingLabel(SkiaLabel label)
        {
            return label;
        }

        protected virtual SkiaLabel CreatePlaceholderLabel()
        {
            return new SkiaLabel
            {
                UseCache = SkiaCacheType.Operations,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 0, 4, 0),
                IsVisible = false,
            };
        }

        public virtual SkiaLabel CreateLabel()
        {
            var label = UseMarkdown ? new SkiaRichLabel() : new SkiaLabel()
            {
                HorizontalOptions = LayoutOptions.Fill,
                KeepSpacesOnLineBreaks = true,
                NeedsGlyphPositions = true,
                Margin = new Thickness(0, 0, 4, 0),
                TextColor = Colors.Black
            };

            return OnCreatingLabel(label);
        }

        protected virtual SkiaCursor CreateCursor()
        {
            return new()
            {
                UseCache = SkiaCacheType.Operations,
                WidthRequest = 2,
                HeightRequest = FontSize > 0 ? FontSize * 1.2 : 20
            };
        }

        public virtual void CreateControl()
        {
            Label = CreateLabel();

            _placeholderLabel = CreatePlaceholderLabel();

            Cursor = CreateCursor();

            Cursor.ZIndex = 1;
            Cursor.IsVisible = false;

            Children = new List<SkiaControl>()
            {
                // Placeholder lives at the editor level so it receives the correct
                // constrained width from the editor's layout pass, not float.MaxValue
                // from the horizontal SkiaScroll content area.
                _placeholderLabel,

                new SkiaScroll()
                {
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Fill,
                    Content =
                        new SkiaLayer
                        {
                            Children =
                            {
                                Label,
                                new SkiaEditorSelection
                                {
                                    HorizontalOptions = LayoutOptions.Fill,
                                    VerticalOptions = LayoutOptions.Fill,
                                    SourceLabel = Label,
                                }.Assign(out _selectionControl),
                                Cursor
                            }
                        }.Assign(out _contentLayer)
                }.Assign(out _scroll)
            };



            Cursor?.Initialize(Label);

            OnControlCreated();

            UpdateLabel();
        }

        public SkiaLabel Label { get; protected set; }

        public SkiaCursor Cursor { get; protected set; }

        #endregion

        #region ENGINE

        protected override void Paint(DrawingContext ctx)
        {
            // Push the editor's real pixel viewport width to the label before any child draws,
            // so DrawLines can use it for Center/End alignment when the label sits inside the
            // horizontal scroll (which gives it an unconstrained/huge rectDraw.Width).
            if (Label != null && DrawingRect.Width > 0)
            {
                var paddingPx = (float)(Padding.HorizontalThickness * ctx.Scale);
                Label.ViewportConstraintWidth = Math.Max(0f, DrawingRect.Width - paddingPx);
            }
            base.Paint(ctx);
        }

        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            if (HeightRequest < 0)
            {
                var lines = MaxLines > 0 ? MaxLines : 1;
                var paddingPx = (float)(Padding.VerticalThickness * scale);
                // use the label's own height for single-line; fall back to FontSize×1.3 for multiline
                var lineH = Label?.HeightRequest >= 0
                    ? Label.HeightRequest
                    : (FontSize > 0 ? FontSize * (LineHeight > 0 ? LineHeight : 1.3) : 20.0);
                heightConstraint = (float)(lines * lineH * scale + paddingPx);
            }
            return base.OnMeasuring(widthConstraint, heightConstraint, scale);
        }

        /// <summary>
        /// This is Done or Enter key, so maybe just split lines in specific case
        /// </summary>
        public void Submit()
        {
            if (IsMultiline)
            {
                Text += "\n";
            }
            else
            {
                IsFocused = false;
                TextSubmitted?.Invoke(this, Text);
                CommandOnSubmit?.Execute(Text);
            }
        }

        public bool IsMultiline
        {
            get
            {
                return MaxLines != 1;
            }
        }

        public override bool OnFocusChanged(bool focus)
        {
            //base.OnFocusChanged(focus);

            if (focus)
            {
                SetFocus(true);
            }
            else
            {
                SetFocus(false);
            }

            FocusChanged?.Invoke(this, focus);
            CommandOnFocusChanged?.Execute(focus);

            return true;
        }

        /// <summary>
        /// Returns all glyphs for a rendered line, flattened across spans.
        /// Override in subclasses to support multi-span lines.
        /// </summary>
        protected virtual LineGlyph[] GetLineGlyphs(TextLine line)
        {
            return line.Spans.Count > 0 ? (line.Spans[0].Glyphs ?? Array.Empty<LineGlyph>()) : Array.Empty<LineGlyph>();
        }

        /// <summary>
        /// Input in pixels
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        protected int GetCursorPosition(float x, float y)
        {
            //we have all lines position inside Label
            if (Label != null && Label.Lines != null)
            {
                var firstPosInLine = 0;
                foreach (var labelLine in Label.Lines)
                {
                    var posX = x - labelLine.Bounds.Left;
                    var rect = labelLine.Bounds;
                    // rect.Offset(new SKPoint(0, this.HitBoxAuto.Top));

                    var lineGlyphs = GetLineGlyphs(labelLine);

                    if (y >= rect.Top && y <= rect.Bottom) //inside line
                    {
                        if (lineGlyphs.Length == 0)
                            return Text?.Length ?? 0;

                        var posInline = 0;
                        var prevX = 0f;
                        foreach (var charX in lineGlyphs)
                        {
                            //we are checking x vs next char in line, not not current
                            if (prevX <= posX && posX <= charX.Position)
                            {
                                return firstPosInLine + posInline;
                            }
                            prevX = charX.Position;
                            posInline++;
                        }

                        //if we fallen here means we clicked outside the line.
                        var endPos = firstPosInLine + posInline;
                        // If the glyph array included a trailing '\n', step back so cursor lands
                        // at end-of-line (before \n) rather than at the start of the next line.
                        if (Text != null && endPos > 0 && endPos - 1 < Text.Length && Text[endPos - 1] == '\n')
                            endPos--;
                        // clamp — glyph array can have N+1 slots for N chars (end-of-text sentinel)
                        return Math.Min(endPos, Text?.Length ?? endPos);

                    }

                    firstPosInLine = AdvanceLineTextIndex(firstPosInLine, lineGlyphs.Length);
                }

                if (Text != null)
                    return Text.Length;
            }
            return 0;
        }




        private enum SelectionDragMode { None, ExtendFromAnchor, DragHandleStart, DragHandleEnd }
        private SelectionDragMode _selectionDragMode;
        private int _selectionAnchor;

        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {

            switch (args.Type)
            {
            case TouchActionResult.Up:
            _selectionDragMode = SelectionDragMode.None;
            return this;
            break;

            case TouchActionResult.LongPressing:
            {
                if (!HitIsInside(args.Event.StartingLocation.X, args.Event.StartingLocation.Y))
                    break;

                var lpOffset = TranslateInputCoords(apply.ChildOffset);
                var lpX = args.Event.StartingLocation.X + lpOffset.X;
                var lpY = args.Event.StartingLocation.Y + lpOffset.Y;
                if (_scroll != null)
                {
                    lpX += (float)(_scroll.ViewportOffsetX * RenderingScale);
                    lpY += (float)(_scroll.ViewportOffsetY * RenderingScale);
                }

                var lpPos = GetCursorPosition(lpX, lpY);
                SelectWord(lpPos);
                _selectionDragMode = SelectionDragMode.ExtendFromAnchor;
                _selectionAnchor = CursorPosition;
                SetFocusInternal(true);
                Superview.FocusedChild = this;
                return this;
            }

            case TouchActionResult.Down:

            if (!HitIsInside(args.Event.StartingLocation.X, args.Event.StartingLocation.Y))
            {
                // tap outside bounds while focused — unfocus; let tap propagate
                SetFocus(false);
                return null;
            }

            var thisOffset = TranslateInputCoords(apply.ChildOffset);

            var x = args.Event.StartingLocation.X + thisOffset.X;
            var y = args.Event.StartingLocation.Y + thisOffset.Y;

            // account for scroll offset so hit-testing is in content space
            if (_scroll != null)
            {
                x += (float)(_scroll.ViewportOffsetX * RenderingScale);
                y += (float)(_scroll.ViewportOffsetY * RenderingScale);
            }

            // Handle hit-test: tapping near a selection handle starts drag mode.
            if (SelectionLength > 0 && _selectionControl != null)
            {
                var lhc = _selectionControl.LeftHandleCenter;
                if (lhc != SKPoint.Empty)
                {
                    var hitR = (float)(22 * RenderingScale);
                    var hitR2 = hitR * hitR;
                    float hdx, hdy;
                    hdx = x - lhc.X; hdy = y - lhc.Y;
                    if (hdx * hdx + hdy * hdy <= hitR2)
                    {
                        _selectionDragMode = SelectionDragMode.DragHandleStart;
                        _selectionAnchor = CursorPosition + SelectionLength;
                        SetFocusInternal(true);
                        Superview.FocusedChild = this;
                        return this;
                    }
                    var rhc = _selectionControl.RightHandleCenter;
                    hdx = x - rhc.X; hdy = y - rhc.Y;
                    if (hdx * hdx + hdy * hdy <= hitR2)
                    {
                        _selectionDragMode = SelectionDragMode.DragHandleEnd;
                        _selectionAnchor = CursorPosition;
                        SetFocusInternal(true);
                        Superview.FocusedChild = this;
                        return this;
                    }
                }
            }

            _selectionDragMode = SelectionDragMode.None;
            var pos = GetCursorPosition(x, y);
            CursorPosition = pos;
            SelectionLength = 0;

            // Re-focus native keyboard sink — WinUI shifts keyboard focus on canvas tap
            // even when IsFocused is already true, so BindableProperty callback never fires.
            SetFocusInternal(true);
            Superview.FocusedChild = this;
            return this;
            break;

            case TouchActionResult.Panning:
            if (_selectionDragMode != SelectionDragMode.None)
            {
                var panOffset = TranslateInputCoords(apply.ChildOffset);
                var px = args.Event.Location.X + panOffset.X;
                var py = args.Event.Location.Y + panOffset.Y;
                if (_scroll != null)
                {
                    px += (float)(_scroll.ViewportOffsetX * RenderingScale);
                    py += (float)(_scroll.ViewportOffsetY * RenderingScale);
                }
                var newPos = GetCursorPosition(px, py);
                var selStart = Math.Min(newPos, _selectionAnchor);
                var selEnd = Math.Max(newPos, _selectionAnchor);
                CursorPosition = selStart;
                SelectionLength = selEnd - selStart;
                SetCursorPositionNative(selStart, selEnd);
                return this;
            }
            goto default;

            default:
            if (IsFocused)
            {
                // route to children (scroll) but keep Canvas focus on this editor
                base.ProcessGestures(args, apply);
                return this;
            }
            break;
            }

            return base.ProcessGestures(args, apply);
        }



        protected void SetFocusInternal(bool value)
        {
            // 100 ms: on Android/iOS the IME needs time to connect before ShowSoftInput +
            // SetSelection fire. On Windows, WinUI shifts keyboard focus to the Canvas on
            // every tap — 100 ms lets that settle before we steal it back with Focus(Programmatic).
            // Stray keystrokes during the window are blocked by PlatformClearFocusNow().
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetFocusNative(value);
                    MoveInternalCursor();
                });
            });
        }


        /// <summary>
        /// Sets native control cursor position to CursorPosition (and selection end if active) and calls UpdateCursorVisibility.
        /// </summary>
        protected void MoveInternalCursor()
        {
            if (SelectionLength > 0)
                SetCursorPositionNative(CursorPosition, CursorPosition + SelectionLength);
            else
                SetCursorPositionNative(CursorPosition);

            UpdateCursorVisibility();
        }

        public void SelectAll()
        {
            if (Text == null) return;
            SelectionLength = Text.Length;
            CursorPosition = 0;
            SetCursorPositionNative(0, Text.Length);
        }

        /// <summary>
        /// Positions cursor control where it should be using translation, and sets its visibility.
        /// </summary>
        public virtual void UpdateCursorVisibility()
        {
            if (Label != null && Cursor != null)
            {

                // Clamp to text length — CursorPosition can be stale (16 ms delay on Windows,
                // 50 ms on Android) while text has already shrunk via native TextBox events.
                // Without clamping, the glyph loop exits without calling MoveCursorTo and the
                // cursor stays at its previous pixel position indefinitely.
                var textLen = Text?.Length ?? 0;
                var cursorIndex = SelectionLength > 0
                    ? Math.Min(CursorPosition + SelectionLength, textLen)
                    : Math.Min(CursorPosition, textLen);

                //make cursor fit the line height
                var lineH = Label.MeasuredLineHeight / RenderingScale;
                if (lineH > 0)
                    Cursor.HeightRequest = lineH;

                if (cursorIndex < 0)
                {
                    CursorPosition = 0;
                    MoveCursorTo(0, 0);
                    SetCursorVisible(IsFocused && CanShowCursor);
                    return;
                }

                if (Label.Lines == null || Label.LinesCount <= 0)
                {
#if !BROWSER
                    // On Blazor, Lines are populated during the render pass which happens
                    // after property-change handlers. Hiding cursor here would cause a
                    // 1-frame invisible flicker every keystroke; defer instead (see DeferVisualCursorUpdate).
                    if (string.IsNullOrEmpty(Text))
                    {
                        // Empty editor: no lines — place cursor at the text insertion point
                        // for the active horizontal alignment so it aligns with placeholder text.
                        var cursorX = 0.0;
                        // Use ViewportConstraintWidth (set from editor's real pixel width in Paint)
                        // so cursor centers within the visible area, not the infinite scroll content.
                        var viewportW = Label.ViewportConstraintWidth > 0
                            ? Label.ViewportConstraintWidth / RenderingScale
                            : Label.DrawingRect.Width / RenderingScale;
                        if (viewportW > 0 && viewportW < SkiaControl.MaxRealPixelSize)
                        {
                            if (HorizontalTextAlignment == DrawTextAlignment.Center)
                                cursorX = (viewportW - Cursor.WidthRequest) / 2.0;
                            else if (HorizontalTextAlignment == DrawTextAlignment.End)
                                cursorX = viewportW - Cursor.WidthRequest;
                        }
                        MoveCursorTo(cursorX, 0);
                        SetCursorVisible(IsFocused && CanShowCursor);
                    }
                    else
                    {
                        // Text was just changed and label hasn't re-measured yet — hide
                        // cursor briefly; it will be repositioned by the delayed position update.
                        SetCursorVisible(false);
                    }
#endif
                    return;
                }

                var index = 0;
                var line = 0;
                var endX = 0f;
                var lastY = 0f;
                foreach (var labelLine in Label.Lines)
                {
                    var lineGlyphs = GetLineGlyphs(labelLine);
                    var nextIndex = AdvanceLineTextIndex(index, lineGlyphs.Length);

                    // Check if we're on the last line and the cursor is at the last position
                    if (line == Label.LinesCount - 1 && cursorIndex == index + lineGlyphs.Length)
                    {
                        var endsWithHardLineBreak = cursorIndex > 0
                            && Text != null
                            && cursorIndex == Text.Length
                            && Text[cursorIndex - 1] == '\n';

                        double translateX;
                        double translateY;

                        if (endsWithHardLineBreak)
                        {
                            var startX = labelLine.Bounds.Left + (lineGlyphs.Length > 0 ? lineGlyphs[0].Position : 0);
                            translateX = (startX - Label.DrawingRect.Left) / RenderingScale;
                            translateY = (labelLine.Bounds.Bottom - Label.DrawingRect.Top) / RenderingScale;
                        }
                        else
                        {
                            translateX = (labelLine.Bounds.Right - Label.DrawingRect.Left) / RenderingScale;
                            translateY = (labelLine.Bounds.Top - Label.DrawingRect.Top) / RenderingScale;
                        }

                        MoveCursorTo(translateX, translateY);
                        break;
                    }

                    if (cursorIndex == nextIndex && nextIndex > index + lineGlyphs.Length)
                    {
                        var nextLine = line + 1 < Label.LinesCount ? Label.Lines[line + 1] : null;
                        if (nextLine != null)
                        {
                            var nextGlyphs = GetLineGlyphs(nextLine);
                            var startX = nextLine.Bounds.Left + (nextGlyphs.Length > 0 ? nextGlyphs[0].Position : 0);
                            MoveCursorTo((startX - Label.DrawingRect.Left) / RenderingScale,
                                (nextLine.Bounds.Top - Label.DrawingRect.Top) / RenderingScale);
                            break;
                        }
                    }

                    if (cursorIndex == nextIndex - 1 && nextIndex > index + lineGlyphs.Length)
                    {
                        MoveCursorTo((labelLine.Bounds.Right - Label.DrawingRect.Left) / RenderingScale,
                            (labelLine.Bounds.Top - Label.DrawingRect.Top) / RenderingScale);
                        break;
                    }

                    if (cursorIndex < index + lineGlyphs.Length)
                    {

                        if (line > 0 && cursorIndex - index == 0)
                        {
                            var isHardLineBreak = cursorIndex > 0
                                && Text != null
                                && cursorIndex - 1 < Text.Length
                                && Text[cursorIndex - 1] == '\n';

                            if (isHardLineBreak)
                            {
                                var startX = labelLine.Bounds.Left + (lineGlyphs.Length > 0 ? lineGlyphs[0].Position : 0);
                                MoveCursorTo((startX - Label.DrawingRect.Left) / RenderingScale,
                                    (labelLine.Bounds.Top - Label.DrawingRect.Top) / RenderingScale);
                            }
                            else
                            {
                                MoveCursorTo((endX - Label.DrawingRect.Left) / RenderingScale,
                                    (lastY - Label.DrawingRect.Top) / RenderingScale);
                            }
                            break;
                        }

                        var x = labelLine.Bounds.Left + lineGlyphs[cursorIndex - index].Position;
                        var y = labelLine.Bounds.Top;

                        MoveCursorTo((x - Label.DrawingRect.Left) / RenderingScale, (y - Label.DrawingRect.Top) / RenderingScale);
                        break;
                    }

                    endX = labelLine.Bounds.Right;
                    lastY = labelLine.Bounds.Top;
                    line++;
                    index = nextIndex;
                }

                SetCursorVisible(IsFocused && CanShowCursor);

                if (_selectionControl != null)
                {
                    _selectionControl.SelectionStart = CursorPosition;
                    _selectionControl.SelectionLength = SelectionLength;
                    _selectionControl.SelectionColor = SelectionColor;
                    _selectionControl.Invalidate();
                }
            }

        }

        protected void SetCursorVisible(bool show)
        {
            if (show)
            {
                Cursor.Opacity = 1;
                if (!Cursor.IsVisible) Cursor.Invalidate();
                Cursor.IsVisible = true;
            }
            else
            {
                Cursor.IsVisible = false;
            }
        }

        /// <summary>
        /// Translate cursor from the left top corner, params in pts.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        protected virtual void MoveCursorTo(double x, double y)
        {
            Cursor.Left = x;
            Cursor.Top = y;
            // BindableProperty skips propertyChanged when value equals the default (0.0).
            // At cursor position 0 both Left and Top are 0, so NeedRepaint never fires.
            // Force a repaint so the cursor always redraws at the new position.
            Cursor.Repaint();

            // scroll to keep cursor visible
            if (_scroll != null)
            {
                var cursorRight = x + Cursor.WidthRequest;
                var cursorBottom = y + Cursor.HeightRequest;

                if (IsMultiline)
                {
                    // vertical scroll: ensure cursor Y is visible
                    var viewH = _scroll.Height;
                    var offsetY = _scroll.ViewportOffsetY;
                    if (y < -offsetY)
                        _scroll.ScrollTo((float)_scroll.ViewportOffsetX, (float)-y, 0.1f, true);
                    else if (cursorBottom > -offsetY + viewH)
                        _scroll.ScrollTo((float)_scroll.ViewportOffsetX, (float)-(cursorBottom - viewH), 0.1f, true);
                }
                else
                {
                    // horizontal scroll: ensure cursor X is visible
                    var viewW = _scroll.Width;
                    var offsetX = _scroll.ViewportOffsetX;
                    if (x < -offsetX)
                    {
                        // cursor off left edge — scroll right to show it
                        _scroll.ScrollTo((float)-x, (float)_scroll.ViewportOffsetY, 0.1f, true);
                    }
                    else if (cursorRight > -offsetX + viewW)
                    {
                        // cursor off right edge — scroll left to show it
                        _scroll.ScrollTo((float)-(cursorRight - viewW), (float)_scroll.ViewportOffsetY, 0.1f, true);
                    }
                    else if (offsetX < 0)
                    {
                        // cursor is visible but viewport is scrolled right further than needed
                        // (e.g. text was deleted) — scroll back left as far as possible while
                        // keeping cursor in view
                        var idealOffsetX = (float)(cursorRight > viewW ? -(cursorRight - viewW) : 0);
                        if (idealOffsetX > offsetX)
                            _scroll.ScrollTo(idealOffsetX, (float)_scroll.ViewportOffsetY, 0.1f, true);
                    }
                }
            }

            Invalidate();
        }

        private int AdvanceLineTextIndex(int currentIndex, int glyphCount)
        {
            var nextIndex = currentIndex + glyphCount;
            if (Text != null && nextIndex < Text.Length && Text[nextIndex] == '\n')
            {
                nextIndex++;
            }

            return nextIndex;
        }

        /// <summary>Returns the zero-based line index the cursor is currently on.</summary>
        public int GetCursorLine()
        {
            if (Label?.Lines == null || Label.LinesCount == 0)
                return 0;

            var cursorIndex = CursorPosition;
            var textIndex = 0;
            var lineNum = 0;

            foreach (var labelLine in Label.Lines)
            {
                var lineGlyphs = GetLineGlyphs(labelLine);
                var nextIndex = AdvanceLineTextIndex(textIndex, lineGlyphs.Length);

                if (cursorIndex < nextIndex || lineNum == Label.LinesCount - 1)
                    return lineNum;

                textIndex = nextIndex;
                lineNum++;
            }

            return lineNum;
        }

        public void SetFocus(bool focus)
        {
            if (focus)
            {
                IsFocused = true;
            }
            else
            {
                SelectionLength = 0;
                _selectionDragMode = SelectionDragMode.None;
                IsFocused = false;
                // Release native control immediately so it stops capturing keystrokes
                // while the next editor's focus delay (16–100 ms) elapses.
                // Does NOT close the keyboard — another editor will inherit it.
                PlatformClearFocusNow();
            }

            UpdateLabel();
        }

        // Implemented per platform: release the native control from input focus without
        // closing the soft keyboard (another editor will steal input).
        partial void PlatformClearFocusNow();

        protected virtual void HandleVerticalArrow(bool up)
        {
            if (Label?.Lines == null || Label.LinesCount <= 1) return;

            var curLine = GetCursorLine();
            var targetLine = up ? curLine - 1 : curLine + 1;

            if (targetLine < 0 || targetLine >= Label.LinesCount) return;

            var cursorXPixels = (float)(Cursor.Left * RenderingScale);

            var lineStart = 0;
            for (var i = 0; i < targetLine; i++)
                lineStart = AdvanceLineTextIndex(lineStart, GetLineGlyphs(Label.Lines[i]).Length);

            var glyphs = GetLineGlyphs(Label.Lines[targetLine]);
            if (glyphs.Length == 0)
            {
                CursorPosition = lineStart;
                return;
            }

            var prevX = 0f;
            for (var i = 0; i < glyphs.Length; i++)
            {
                if (prevX <= cursorXPixels && cursorXPixels <= glyphs[i].Position)
                {
                    CursorPosition = lineStart + i;
                    return;
                }
                prevX = glyphs[i].Position;
            }

            var pos = lineStart + glyphs.Length;
            if (Text != null && pos > 0 && pos - 1 < Text.Length && Text[pos - 1] == '\n')
                pos--;
            CursorPosition = pos;
        }




        protected virtual void OnControlCreated()
        {

        }



        public static readonly BindableProperty CursorPositionProperty = BindableProperty.Create(
            nameof(CursorPosition),
            typeof(int),
            typeof(SkiaEditor),
            -1, propertyChanged: OnNeedUpdateSelection);

        private static void OnNeedUpdateSelection(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.MoveInternalCursor();
            }
        }

        public int CursorPosition
        {
            get { return (int)GetValue(CursorPositionProperty); }
            set { SetValue(CursorPositionProperty, value); }
        }


 

        protected RestartingTimer<int> TimerUpdateParentCursorPosition;

        /// <summary>
        /// We have to sync with a delay after text was changed otherwise the cursor position is not updated yet.
        /// Using restarting timer, every time this is called the timer is reset if callback wasn't executed yet.
        /// </summary>
        /// <param name="ms"></param>
        /// <param name="position"></param>
        protected void SetCursorPositionWithDelay(int ms, int position)
        {
            if (TimerUpdateParentCursorPosition == null)
            {
                TimerUpdateParentCursorPosition = new(TimeSpan.FromMilliseconds(ms), (arg) =>
                {
                    CursorPosition = arg;
                    //Debug.WriteLine("CursorPosition from native: " + arg);
                });
                TimerUpdateParentCursorPosition.Start(position);
            }
            else
            {
                TimerUpdateParentCursorPosition.Restart(position);
            }
        }

        public virtual void SetSelection(int start, int end)
        {
            if (Label != null)
            {
                MoveInternalCursor();
            }
        }

        protected (int start, int end) GetWordBoundaries(int charIndex)
        {
            var text = Text;
            if (string.IsNullOrEmpty(text) || charIndex < 0 || charIndex >= text.Length)
                return (charIndex, charIndex);

            if (!char.IsLetterOrDigit(text[charIndex]))
                return (charIndex, charIndex);

            var start = charIndex;
            while (start > 0 && char.IsLetterOrDigit(text[start - 1]))
                start--;

            var end = charIndex;
            while (end < text.Length && char.IsLetterOrDigit(text[end]))
                end++;

            return (start, end);
        }

        public virtual void SelectWord(int charIndex)
        {
            var (start, end) = GetWordBoundaries(charIndex);
            if (end > start)
            {
                CursorPosition = start;
                SelectionLength = end - start;
                SetCursorPositionNative(start, end);
            }
        }

        public string GetSelectedText()
        {
            if (IsPassword || SelectionLength <= 0) return null;
            var text = Text ?? string.Empty;
            var start = Math.Clamp(CursorPosition, 0, text.Length);
            var len = Math.Clamp(SelectionLength, 0, text.Length - start);
            return len > 0 ? text.Substring(start, len) : null;
        }

        public void DeleteSelection()
        {
            if (SelectionLength <= 0) return;
            var text = Text ?? string.Empty;
            var start = Math.Clamp(CursorPosition, 0, text.Length);
            var len = Math.Clamp(SelectionLength, 0, text.Length - start);
            if (len == 0) return;
            Text = text.Remove(start, len);
            SelectionLength = 0;
            OnSelectionDeleted();
        }

        public void InsertAtCursor(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var existing = Text ?? string.Empty;
            var start = Math.Clamp(CursorPosition, 0, existing.Length);
            var len = Math.Clamp(SelectionLength, 0, existing.Length - start);
            var normalized = value.Replace("\r\n", "\n").Replace("\r", "\n");
            if (!IsMultiline)
                normalized = normalized.Replace("\n", " ");
            Text = existing.Remove(start, len).Insert(start, normalized);
            CursorPosition = start + normalized.Length;
            SelectionLength = 0;
            OnTextInsertedAtCursor();
        }

#if !BROWSER && !DRAWNUI_NET
        public void CutSelection()
        {
            CopySelection();
            DeleteSelection();
        }
#else
        public void CutSelection() => DeleteSelection();
        public void CopySelection() { }
        public void PasteFromClipboard() { }
#endif

#if !BROWSER && !DRAWNUI_NET
        public void CopySelection()
        {
            var text = GetSelectedText();
            if (string.IsNullOrEmpty(text)) return;
            _ = Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.SetTextAsync(text);
        }

        public void PasteFromClipboard()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var text = await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.GetTextAsync();
                    if (!string.IsNullOrEmpty(text))
                    {
                        InsertAtCursor(text);
                        SetFocusNative(true);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[SkiaEditor] PasteFromClipboard: {e}");
                }
            });
        }
#endif

        partial void OnSelectionDeleted();

        partial void OnTextInsertedAtCursor();

        public override void OnDisposing()
        {
            DisposePlatform();

            base.OnDisposing();
        }

        protected override void OnLayoutChanged()
        {
            base.OnLayoutChanged();

            UpdateNativePosition();
        }

        #endregion

        #region PROPERTIES

        public enum SkiaEditorKeyboard
        {
            Default,
            Numeric,
            Decimal,
            Phone,
            Email
        }

#if !BROWSER
        public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(
            nameof(ReturnType),
            typeof(ReturnType),
            typeof(SkiaEditor),
            ReturnType.Done);

        public ReturnType ReturnType
        {
            get { return (ReturnType)GetValue(ReturnTypeProperty); }
            set { SetValue(ReturnTypeProperty, value); }
        }
#endif

        public static readonly BindableProperty KeyboardTypeProperty = BindableProperty.Create(
            nameof(KeyboardType),
            typeof(SkiaEditorKeyboard),
            typeof(SkiaEditor),
            SkiaEditorKeyboard.Default,
            propertyChanged: (b, o, n) => { if (b is SkiaEditor e && e.IsFocused) e.ApplyKeyboardType(); });

        public SkiaEditorKeyboard KeyboardType
        {
            get { return (SkiaEditorKeyboard)GetValue(KeyboardTypeProperty); }
            set { SetValue(KeyboardTypeProperty, value); }
        }

        public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(
            nameof(IsPassword),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            propertyChanged: (b, o, n) =>
            {
                if (b is SkiaEditor e)
                {
                    e.UpdateLabel();
                    if (e.IsFocused) e.ApplyKeyboardType();
                }
            });

        public bool IsPassword
        {
            get { return (bool)GetValue(IsPasswordProperty); }
            set { SetValue(IsPasswordProperty, value); }
        }

        public static readonly BindableProperty CommandOnSubmitProperty = BindableProperty.Create(
            nameof(CommandOnSubmit),
            typeof(ICommand),
            typeof(SkiaEditor),
            null);

        public ICommand CommandOnSubmit
        {
            get { return (ICommand)GetValue(CommandOnSubmitProperty); }
            set { SetValue(CommandOnSubmitProperty, value); }
        }

        public static readonly BindableProperty CommandOnFocusChangedProperty = BindableProperty.Create(
            nameof(CommandOnFocusChanged),
            typeof(ICommand),
            typeof(SkiaEditor),
            null);

        public ICommand CommandOnFocusChanged
        {
            get { return (ICommand)GetValue(CommandOnFocusChangedProperty); }
            set { SetValue(CommandOnFocusChangedProperty, value); }
        }

        public static readonly BindableProperty CommandOnTextChangedProperty = BindableProperty.Create(
            nameof(CommandOnTextChanged),
            typeof(ICommand),
            typeof(SkiaEditor),
            null);

        public ICommand CommandOnTextChanged
        {
            get { return (ICommand)GetValue(CommandOnTextChangedProperty); }
            set { SetValue(CommandOnTextChangedProperty, value); }
        }

        public static readonly BindableProperty UseMarkdownProperty = BindableProperty.Create(
            nameof(UseMarkdown),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            propertyChanged: OnUseMarkdownChanged);

        private static void OnUseMarkdownChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.RecreateLabelIfNeeded();
                control.UpdateLabel();
            }
        }

        public bool UseMarkdown
        {
            get { return (bool)GetValue(UseMarkdownProperty); }
            set { SetValue(UseMarkdownProperty, value); }
        }


        public static readonly BindableProperty CanShowCursorProperty = BindableProperty.Create(
            nameof(CanShowCursor),
            typeof(bool),
            typeof(SkiaEditor),
            true);

        public bool CanShowCursor
        {
            get { return (bool)GetValue(CanShowCursorProperty); }
            set { SetValue(CanShowCursorProperty, value); }
        }

        public new static readonly BindableProperty IsFocusedProperty = BindableProperty.Create(
            nameof(IsFocused),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            BindingMode.TwoWay,
            propertyChanged: OnNeedChangeFocus);

        private static void OnNeedChangeFocus(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.SetFocusInternal((bool)newvalue);
            }
        }

        public new bool IsFocused
        {
            get { return (bool)GetValue(IsFocusedProperty); }
            set { SetValue(IsFocusedProperty, value); }
        }

        private static void OnNeedUpdateText(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.UpdateLabel();
            }
        }

        public static readonly BindableProperty TextProperty = BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(SkiaEditor),
            default(string),
            BindingMode.TwoWay,
            propertyChanged: OnControlTextChanged);


        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }
        private static void OnControlTextChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.TextChanged?.Invoke(control, (string)newvalue);
                control.CommandOnTextChanged?.Execute((string)newvalue);
                OnNeedUpdateText(bindable, oldvalue, newvalue);
            }
        }

        public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize),
            typeof(double), typeof(SkiaEditor), 12.0,
            propertyChanged: OnNeedUpdateText);

        public double FontSize
        {
            get { return (double)GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }

        public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(DrawTextAlignment),
            typeof(SkiaEditor),
            defaultValue: DrawTextAlignment.Start,
            propertyChanged: OnNeedUpdateText);

        public DrawTextAlignment HorizontalTextAlignment
        {
            get { return (DrawTextAlignment)GetValue(HorizontalTextAlignmentProperty); }
            set { SetValue(HorizontalTextAlignmentProperty, value); }
        }

        public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaEditor),
            defaultValue: TextAlignment.Start,
            propertyChanged: OnNeedUpdateText);

        public TextAlignment VerticalTextAlignment
        {
            get { return (TextAlignment)GetValue(VerticalTextAlignmentProperty); }
            set { SetValue(VerticalTextAlignmentProperty, value); }
        }


        public static readonly BindableProperty LineHeightProperty = BindableProperty.Create(
            nameof(LineHeight),
            typeof(double),
            typeof(SkiaEditor),
            1.0,
            propertyChanged: OnNeedUpdateText);

        public double LineHeight
        {
            get { return (double)GetValue(LineHeightProperty); }
            set { SetValue(LineHeightProperty, value); }
        }

        public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily),
            typeof(string), typeof(SkiaEditor), string.Empty, propertyChanged: OnNeedUpdateText);
        public string FontFamily
        {
            get { return (string)GetValue(FontFamilyProperty); }
            set { SetValue(FontFamilyProperty, value); }
        }

        public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
            nameof(TextColor), typeof(Color), typeof(SkiaEditor),
            Colors.GreenYellow,
            propertyChanged: OnNeedUpdateText);
        public Color TextColor
        {
            get { return (Color)GetValue(TextColorProperty); }
            set { SetValue(TextColorProperty, value); }
        }

        public static readonly BindableProperty FontWeightProperty = BindableProperty.Create(
            nameof(FontWeight),
            typeof(int),
            typeof(SkiaEditor),
            0,
            propertyChanged: OnNeedUpdateText);

        public int FontWeight
        {
            get { return (int)GetValue(FontWeightProperty); }
            set { SetValue(FontWeightProperty, value); }
        }


        public static readonly BindableProperty TextGradientProperty = BindableProperty.Create(
            nameof(TextGradient),
            typeof(SkiaGradient),
            typeof(SkiaEditor),
            null,
            propertyChanged: OnNeedUpdateText);

        public SkiaGradient TextGradient
        {
            get { return (SkiaGradient)GetValue(TextGradientProperty); }
            set { SetValue(TextGradientProperty, value); }
        }

        public static readonly BindableProperty CursorColorProperty = BindableProperty.Create(
            nameof(CursorColor),
            typeof(Color),
            typeof(SkiaEditor),
            Colors.Black,
            propertyChanged: OnNeedUpdateText);

        public Color CursorColor
        {
            get { return (Color)GetValue(CursorColorProperty); }
            set { SetValue(CursorColorProperty, value); }
        }


        public static readonly BindableProperty CursorGradientProperty = BindableProperty.Create(
            nameof(CursorGradient),
            typeof(SkiaGradient),
            typeof(SkiaEditor),
            null,
            propertyChanged: OnNeedUpdateText);


        public SkiaGradient CursorGradient
        {
            get { return (SkiaGradient)GetValue(CursorGradientProperty); }
            set { SetValue(CursorGradientProperty, value); }
        }


        public static readonly BindableProperty SelectionLengthProperty = BindableProperty.Create(
            nameof(SelectionLength),
            typeof(int),
            typeof(SkiaEditor),
            0,
            propertyChanged: OnSelectionChanged);

        private static void OnSelectionChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.UpdateCursorVisibility();
            }
        }

        public int SelectionLength
        {
            get { return (int)GetValue(SelectionLengthProperty); }
            set { SetValue(SelectionLengthProperty, value); }
        }

        public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create(
            nameof(SelectionColor),
            typeof(Color),
            typeof(SkiaEditor),
            Color.FromArgb("#5590CFFE"),
            propertyChanged: OnNeedUpdateText);

        public Color SelectionColor
        {
            get { return (Color)GetValue(SelectionColorProperty); }
            set { SetValue(SelectionColorProperty, value); }
        }

        public static readonly BindableProperty PlaceholderTextProperty = BindableProperty.Create(
            nameof(PlaceholderText),
            typeof(string),
            typeof(SkiaEditor),
            null,
            propertyChanged: OnPlaceholderChanged);

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(
            nameof(PlaceholderColor),
            typeof(Color),
            typeof(SkiaEditor),
            Colors.Gray,
            propertyChanged: OnPlaceholderChanged);

        public Color PlaceholderColor
        {
            get => (Color)GetValue(PlaceholderColorProperty);
            set => SetValue(PlaceholderColorProperty, value);
        }

        public static readonly BindableProperty PlaceholderHorizontalAlignmentProperty = BindableProperty.Create(
            nameof(PlaceholderHorizontalAlignment),
            typeof(DrawTextAlignment),
            typeof(SkiaEditor),
            DrawTextAlignment.Start,
            propertyChanged: OnPlaceholderChanged);

        public DrawTextAlignment PlaceholderHorizontalAlignment
        {
            get => (DrawTextAlignment)GetValue(PlaceholderHorizontalAlignmentProperty);
            set => SetValue(PlaceholderHorizontalAlignmentProperty, value);
        }

        private static void OnPlaceholderChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor e)
                e.UpdatePlaceholder();
        }

        public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines),
            typeof(int), typeof(SkiaEditor), 1, propertyChanged: OnNeedUpdateText);
        public int MaxLines
        {
            get { return (int)GetValue(MaxLinesProperty); }
            set { SetValue(MaxLinesProperty, value); }
        }



        #endregion

        #region LOCALIZE

        public static string ActionGo = "Go";
        public static string ActionNext = "Next";
        public static string ActionSend = "Send";
        public static string ActionSearch = "Search";
        public static string ActionDone = "Done";

        #endregion

#if (!ANDROID && !IOS && !MACCATALYST && !WINDOWS && !TIZEN && !DRAWNUI_NET && !BROWSER)


        public void SetCursorPositionNative(int position, int stop = -1)
        {
            throw new NotImplementedException();
        }

        public void DisposePlatform()
        {
            throw new NotImplementedException();
        }

        public void SetFocusNative(bool focus)
        {
            throw new NotImplementedException();
        }

        public void UpdateNativePosition()
        {
            throw new NotImplementedException();
        }

        public void ApplyKeyboardType() { }

#endif

    }
}
