namespace DrawnUi.Draw;

public class SkiaEditorSelection : SkiaControl
{
    private SKPaint _paint;
    private SKPaint _handlePaint;

    public SkiaLabel SourceLabel { get; set; }

    /// <summary>
    /// When set, used instead of Spans[0].Glyphs to support multi-span lines.
    /// </summary>
    public Func<TextLine, LineGlyph[]>? GetLineGlyphsOverride { get; set; }

    public int SelectionStart { get; set; } = -1;
    public int SelectionLength { get; set; } = 0;
    public Color SelectionColor { get; set; } = Color.FromArgb("#5590CFFE");

    // Updated each Paint pass; used by SkiaEditor.ProcessGestures for handle hit-testing.
    public SKPoint LeftHandleCenter { get; private set; }
    public SKPoint RightHandleCenter { get; private set; }

    protected override void Paint(DrawingContext ctx)
    {
        LeftHandleCenter = SKPoint.Empty;
        RightHandleCenter = SKPoint.Empty;

        if (SourceLabel?.Lines == null || SelectionLength <= 0 || SelectionStart < 0)
            return;

        _paint ??= new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        _paint.Color = SelectionColor.ToSKColor();

        _handlePaint ??= new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        _handlePaint.Color = SelectionColor.ToSKColor().WithAlpha(255);

        var canvas = ctx.Context.Canvas;
        var start = SelectionStart;
        var end = SelectionStart + SelectionLength;
        var lineIndex = 0;
        var text = SourceLabel.Text;

        var leftHandleX = 0f;
        var leftHandleBottom = 0f;
        var rightHandleX = 0f;
        var rightHandleBottom = 0f;
        var firstSelectedLine = true;

        foreach (var labelLine in SourceLabel.Lines)
        {
            var glyphs = GetLineGlyphsOverride != null
                ? GetLineGlyphsOverride(labelLine)
                : labelLine.Carets != null
                ? SkiaLabel.GetLineGlyphs(labelLine)
                : (labelLine.Spans.Count > 0 ? (labelLine.Spans[0].Glyphs ?? Array.Empty<LineGlyph>()) : Array.Empty<LineGlyph>());

            var lineEnd = lineIndex + glyphs.Length;
            var nextLineIndex = lineEnd;
            if (text != null && nextLineIndex < text.Length && text[nextLineIndex] == '\n')
                nextLineIndex++;

            if (end <= lineIndex)
                break;

            if (start < lineEnd)
            {
                var lineSelStart = Math.Max(start, lineIndex) - lineIndex;
                var lineSelEnd = Math.Min(end, lineEnd) - lineIndex;

                float left, right;
                if (labelLine.Carets != null)
                {
                    // right-to-left text: the selected characters' boxes, two blocks across a direction change
                    left = float.MaxValue;
                    right = float.MinValue;
                    foreach (var box in labelLine.Boxes(lineSelStart, lineSelEnd))
                    {
                        canvas.DrawRect(new SKRect(labelLine.Bounds.Left + box.Left, labelLine.Bounds.Top,
                            labelLine.Bounds.Left + box.Right, labelLine.Bounds.Bottom), _paint);
                        left = Math.Min(left, labelLine.Bounds.Left + box.Left);
                        right = Math.Max(right, labelLine.Bounds.Left + box.Right);
                    }
                    if (left > right)
                        left = right = labelLine.Bounds.Left + labelLine.CaretX(lineSelStart);
                }
                else
                {
                    left = glyphs.Length > 0
                        ? labelLine.Bounds.Left + glyphs[lineSelStart].Position
                        : labelLine.Bounds.Left;

                    right = lineSelEnd < glyphs.Length
                        ? labelLine.Bounds.Left + glyphs[lineSelEnd].Position
                        : labelLine.Bounds.Right;

                    canvas.DrawRect(new SKRect(left, labelLine.Bounds.Top, right, labelLine.Bounds.Bottom), _paint);
                }

                if (firstSelectedLine)
                {
                    leftHandleX = left;
                    leftHandleBottom = labelLine.Bounds.Bottom;
                    firstSelectedLine = false;
                }
                rightHandleX = right;
                rightHandleBottom = labelLine.Bounds.Bottom;
            }

            lineIndex = nextLineIndex;
        }

        if (!firstSelectedLine)
        {
            var hr = (float)(5 * RenderingScale);
            var lhc = new SKPoint(leftHandleX, leftHandleBottom + hr);
            var rhc = new SKPoint(rightHandleX, rightHandleBottom + hr);
            LeftHandleCenter = lhc;
            RightHandleCenter = rhc;
            canvas.DrawCircle(lhc, hr, _handlePaint);
            canvas.DrawCircle(rhc, hr, _handlePaint);
        }
    }

    public override void OnDisposing()
    {
        _paint?.Dispose();
        _paint = null;
        _handlePaint?.Dispose();
        _handlePaint = null;
        base.OnDisposing();
    }
}
