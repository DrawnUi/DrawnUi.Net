using System.Text;

#if DEBUG
namespace DrawnUi.Draw;

/// <summary>
/// Rich text editor with per-character formatting, built on top of SkiaEditor.
/// Uses SkiaEditorDocument for the document model and SkiaRichLabel for rendering.
/// </summary>
public class SkiaRichEditor : SkiaEditor
{
    private readonly SkiaEditorDocument _document = new();
    private bool _suppressTextSync;

    public SkiaRichEditor()
    {
        _document.TextChanged += OnDocumentTextChanged;
        _document.FormattingChanged += OnDocumentFormattingChanged;
    }

    // ---- Document access --------------------------------------------------------

    public SkiaEditorDocument Document => _document;

    /// <summary>
    /// Format at the selection start when text is selected, or at the character
    /// just before the caret when collapsed.
    /// </summary>
    public RichCharFormat SelectionFormat
    {
        get
        {
            var s = CursorPosition;
            var len = SelectionLength;
            if (len > 0) return _document.GetFormatAt(s);
            return _document.GetFormatAt(Math.Max(0, s - 1));
        }
    }

    // ---- Format toggle operations -----------------------------------------------

    public void ToggleBold()         => ToggleFormat(_document.ToggleBold);
    public void ToggleItalic()       => ToggleFormat(_document.ToggleItalic);
    public void ToggleUnderline()    => ToggleFormat(_document.ToggleUnderline);
    public void ToggleStrikethrough() => ToggleFormat(_document.ToggleStrikethrough);

    public void ApplyFormat(RichCharFormat delta)
    {
        var (s, e) = GetSelectionRangeForFormat();
        if (s < e) _document.ApplyFormat(s, e, delta);
    }

    private void ToggleFormat(Action<int, int> toggle)
    {
        var (s, e) = GetSelectionRangeForFormat();
        if (s < e) toggle(s, e);
    }

    private (int start, int end) GetSelectionRangeForFormat()
    {
        var s = CursorPosition;
        var len = SelectionLength;
        if (len > 0) return (s, s + len);
        // No selection: apply to character before caret
        return (Math.Max(0, s - 1), s);
    }

    // ---- Undo / Redo ------------------------------------------------------------

    public bool CanUndoRich => _document.CanUndo;
    public bool CanRedoRich => _document.CanRedo;

    public void UndoRich()
    {
        _document.Undo();
        SyncDocumentToText();
    }

    public void RedoRich()
    {
        _document.Redo();
        SyncDocumentToText();
    }

    // ---- Text operations through document --------------------------------------

    public void InsertTextAtCursor(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var start = CursorPosition;
        var selLen = SelectionLength;

        if (selLen > 0)
        {
            _document.DeleteRange(start, start + selLen);
            SelectionLength = 0;
        }

        _document.InsertText(start, text);
        CursorPosition = start + text.Length;
    }

    public void DeleteBeforeCursor(int count = 1)
    {
        if (count <= 0) return;

        if (SelectionLength > 0)
        {
            _document.DeleteRange(CursorPosition, CursorPosition + SelectionLength);
            SelectionLength = 0;
            return;
        }

        if (_document.Length == 0) return;
        var remove = Math.Min(count, Math.Max(0, CursorPosition));
        if (remove == 0) return;

        var start = CursorPosition - remove;
        _document.DeleteRange(start, CursorPosition);
        CursorPosition = start;
        SelectionLength = 0;
    }

    public void DeleteAfterCursor(int count = 1)
    {
        if (count <= 0) return;

        if (SelectionLength > 0)
        {
            _document.DeleteRange(CursorPosition, CursorPosition + SelectionLength);
            SelectionLength = 0;
            return;
        }

        if (_document.Length == 0 || CursorPosition >= _document.Length) return;
        var remove = Math.Min(count, _document.Length - CursorPosition);
        _document.DeleteRange(CursorPosition, CursorPosition + remove);
        SelectionLength = 0;
    }

    // ---- Shadowed stub methods (redirect through document) ----------------------

    public new void StubTypeText(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        InsertTextAtCursor(NormalizeRich(value));
    }

    public new void StubPressEnter(bool splitLine = false)
    {
        if (IsMultiline)
        {
            InsertTextAtCursor(GetEditorBreakText(splitLine));
            return;
        }

        ExecuteSubmit(clearFocus: false);
    }

    public new void StubBackspace(int count = 1) => DeleteBeforeCursor(count);

    public new void StubDelete(int count = 1) => DeleteAfterCursor(count);

    // ---- CreateLabel / CreateControl overrides ----------------------------------

    protected override SkiaLabel OnCreatingLabel(SkiaLabel label) => label;

    public override SkiaLabel CreateLabel()
    {
        return new RichEditorLabel
        {
            HorizontalOptions = LayoutOptions.Fill,
            KeepSpacesOnLineBreaks = true,
            NeedsGlyphPositions = true,
            Margin = new Thickness(0, 0, 4, 0),
        };
    }

    protected override void OnControlCreated()
    {
        base.OnControlCreated();
        if (_selectionControl != null)
            _selectionControl.GetLineGlyphsOverride = GetLineGlyphs;
    }

    // ---- Multi-span glyph flattening -------------------------------------------

    protected override LineGlyph[] GetLineGlyphs(TextLine line)
    {
        if (line.Spans.Count == 0) return Array.Empty<LineGlyph>();
        if (line.Spans.Count == 1) return line.Spans[0].Glyphs ?? Array.Empty<LineGlyph>();

        var result = new List<LineGlyph>();
        float spanOffset = 0f;
        foreach (var lineSpan in line.Spans)
        {
            var glyphs = lineSpan.Glyphs;
            if (glyphs != null)
            {
                foreach (var g in glyphs)
                    result.Add(LineGlyph.Move(g, g.Position + spanOffset));
            }
            spanOffset += lineSpan.Size.Width;
        }
        return result.ToArray();
    }

    // ---- UpdateLabel override builds spans from document -----------------------

    public override void UpdateLabel()
    {
        if (Label is not RichEditorLabel richLabel)
        {
            base.UpdateLabel();
            return;
        }

        richLabel.FontFamily = FontFamily;
        richLabel.FontSize = FontSize;
        richLabel.TextColor = TextColor;
        richLabel.FontWeight = FontWeight;
        richLabel.FillGradient = TextGradient;
        richLabel.HorizontalTextAlignment = HorizontalTextAlignment;
        richLabel.VerticalTextAlignment = VerticalTextAlignment;
        richLabel.LineHeight = LineHeight;

        if (IsMultiline)
        {
            richLabel.HeightRequest = -1;
            richLabel.MaxLines = -1;
            if (_scroll != null)
                _scroll.Orientation = ScrollOrientation.Vertical;
        }
        else
        {
            richLabel.HeightRequest = 32;
            richLabel.MaxLines = 1;
            if (_scroll != null)
                _scroll.Orientation = ScrollOrientation.Horizontal;
        }

        if (Cursor != null)
            Cursor.Color = CursorColor;

        richLabel.SetSpans(BuildSpans());

        UpdateCursorVisibility();
        Invalidate();
    }

    // ---- Span construction from document ----------------------------------------

    private IEnumerable<TextSpan> BuildSpans()
    {
        var text = _document.GetText();

        var displayText = text;
        if (IsMultiline && !string.IsNullOrEmpty(text) && IsTrailingEditorBreak(text))
            displayText += "​";

        if (string.IsNullOrEmpty(displayText))
        {
            yield return MakeSpan(string.Empty, null);
            yield break;
        }

        var runs = _document.GetFormattingRuns();
        if (runs.Count == 0)
        {
            yield return MakeSpan(displayText, null);
            yield break;
        }

        int cursor = 0;
        foreach (var (rStart, rEnd, fmt) in runs.OrderBy(r => r.Start))
        {
            if (rEnd <= cursor) continue;

            var gapStart = cursor;
            var gapEnd = Math.Min(rStart, displayText.Length);
            if (gapEnd > gapStart)
            {
                yield return MakeSpan(displayText.Substring(gapStart, gapEnd - gapStart), null);
                cursor = gapEnd;
            }

            var runStart = Math.Max(rStart, cursor);
            var runEnd = Math.Min(rEnd, displayText.Length);
            if (runEnd > runStart)
            {
                yield return MakeSpan(displayText.Substring(runStart, runEnd - runStart), fmt);
                cursor = runEnd;
            }
        }

        if (cursor < displayText.Length)
            yield return MakeSpan(displayText.Substring(cursor), null);
    }

    private TextSpan MakeSpan(string text, RichCharFormat? fmt)
    {
        var span = new TextSpan { Text = text };

        if (fmt.HasValue)
        {
            var f = fmt.Value;
            if (f.ForegroundColor != null)   span.TextColor = f.ForegroundColor;
            if (f.BackgroundColor != null)   span.BackgroundColor = f.BackgroundColor;
            if (f.FontSize.HasValue)         span.FontSize = f.FontSize.Value;
            if (!string.IsNullOrEmpty(f.FontFamily)) span.FontFamily = f.FontFamily;
            if (f.FontWeight.HasValue)       span.FontWeight = f.FontWeight.Value;
            if (f.StrikeoutColor != null)    span.StrikeoutColor = f.StrikeoutColor;
            span.IsBold    = f.Bold == true;
            span.IsItalic  = f.Italic == true;
            span.Underline = f.Underline == true;
            span.Strikeout = f.Strikethrough == true;
        }

        return span;
    }

    // ---- Sync: document ↔ base Text property ------------------------------------

    private void OnDocumentTextChanged(object? sender, EventArgs e)
        => SyncDocumentToText();

    private void OnDocumentFormattingChanged(object? sender, EventArgs e)
        => UpdateLabel();

    private void SyncDocumentToText()
    {
        if (_suppressTextSync) return;
        _suppressTextSync = true;
        try { SetValue(TextProperty, _document.GetText()); }
        finally { _suppressTextSync = false; }
        // OnControlTextChanged → UpdateLabel is triggered by SetValue above
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(Text) && !_suppressTextSync)
        {
            var text = GetValue(TextProperty) as string;
            _document.SetText(text);
            // base.OnPropertyChanged fires UpdateLabel before we reach here (document still empty).
            // SyncDocumentToText skips the SetValue callback (same value). Call explicitly now.
            UpdateLabel();
        }
    }

    private static string NormalizeRich(string? value)
        => NormalizeEditorLineBreaks(value);

    // ---- Inner label type -------------------------------------------------------

    public class RichEditorLabel : SkiaRichLabel
    {
        public void SetSpans(IEnumerable<TextSpan> spans)
        {
            ReplaceSpans(spans);
        }
    }
}
#endif
