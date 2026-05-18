namespace DrawnUi.Draw;

public class SkiaEditorSelection : SkiaControl
{
    private SKPaint _paint;

    public SkiaLabel SourceLabel { get; set; }

    /// <summary>
    /// When set, used instead of Spans[0].Glyphs to support multi-span lines.
    /// </summary>
    public Func<TextLine, LineGlyph[]>? GetLineGlyphsOverride { get; set; }

    public int SelectionStart { get; set; } = -1;
    public int SelectionLength { get; set; } = 0;
    public Color SelectionColor { get; set; } = Color.FromArgb("#5590CFFE");

    protected override void Paint(DrawingContext ctx)
    {
        if (SourceLabel?.Lines == null || SelectionLength <= 0 || SelectionStart < 0)
            return;

        _paint ??= new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        _paint.Color = SelectionColor.ToSKColor();

        var canvas = ctx.Context.Canvas;
        var start = SelectionStart;
        var end = SelectionStart + SelectionLength;
        var lineIndex = 0;
        var text = SourceLabel.Text;

        foreach (var labelLine in SourceLabel.Lines)
        {
            var glyphs = GetLineGlyphsOverride != null
                ? GetLineGlyphsOverride(labelLine)
                : (labelLine.Spans.Count > 0 ? (labelLine.Spans[0].Glyphs ?? Array.Empty<LineGlyph>()) : Array.Empty<LineGlyph>());

            var lineEnd = lineIndex + glyphs.Length;
            var nextLineIndex = lineEnd;
            if (text != null && nextLineIndex < text.Length && text[nextLineIndex] == '\n')
            {
                nextLineIndex++;
            }

            if (end <= lineIndex)
                break;

            if (start < lineEnd)
            {
                var lineSelStart = Math.Max(start, lineIndex) - lineIndex;
                var lineSelEnd = Math.Min(end, lineEnd) - lineIndex;

                var left = glyphs.Length > 0
                    ? labelLine.Bounds.Left + glyphs[lineSelStart].Position
                    : labelLine.Bounds.Left;

                var right = lineSelEnd < glyphs.Length
                    ? labelLine.Bounds.Left + glyphs[lineSelEnd].Position
                    : labelLine.Bounds.Right;

                canvas.DrawRect(new SKRect(left, labelLine.Bounds.Top, right, labelLine.Bounds.Bottom), _paint);
            }

            lineIndex = nextLineIndex;
        }
    }

    public override void OnDisposing()
    {
        _paint?.Dispose();
        _paint = null;
        base.OnDisposing();
    }
}
