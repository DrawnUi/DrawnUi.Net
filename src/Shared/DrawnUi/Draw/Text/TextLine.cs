namespace DrawnUi.Draw;

public class TextLine
{
    /// <summary>
    /// Set during rendering
    /// </summary>
    public SKRect Bounds { get; set; }

    public string Value { get; set; }

    public bool IsNewParagraph { get; set; }
    public bool IsLastInParagraph { get; set; }

    public float Width { get; set; }
    public float Height { get; set; }

    //public LineGlyph[] Glyphs { get; set; }

    //public List<ApplySpan> ApplySpans { get; set; }

    public List<LineSpan> Spans { get; set; }

    /// <summary>
    /// Set for a line of a paragraph with right-to-left text, whose spans are in visual order: one entry per UTF-16 code
    /// unit of the line in logical order, with its visual box relative to the line's left edge. Null for other lines,
    /// where glyph positions run left to right in text order and are the caret positions.
    /// </summary>
    public LineCaret[] Carets { get; set; }

    /// <summary>
    /// The paragraph of this line reads right to left (its first strong letter is right to left). Only meaningful when
    /// <see cref="Carets"/> is set.
    /// </summary>
    public bool IsRightToLeft { get; set; }

    public TextLine()
    {
        Spans = new();
    }

    /// <summary>
    /// Caret X before the code unit <paramref name="index"/> of a line with <see cref="Carets"/>, relative to the line's
    /// left edge: the trailing edge of the previous character (its right edge when it reads left to right, its left
    /// edge when right to left), the leading edge of the first character at the line start. So the caret stays with
    /// the text just typed.
    /// </summary>
    public float CaretX(int index)
    {
        if (Carets == null || Carets.Length == 0)
            return 0;

        if (index <= 0)
        {
            var first = Carets[0];
            return first.RightToLeft ? first.Left + first.Advance : first.Left;
        }

        var c = Carets[Math.Min(index, Carets.Length) - 1];
        return c.RightToLeft ? c.Left : c.Left + c.Advance;
    }

    /// <summary>
    /// The character that places the caret before <paramref name="index"/> (see <see cref="CaretX"/>) reads right to
    /// left, so the caret is drawn to the left of its X.
    /// </summary>
    public bool CaretIsRightToLeft(int index)
        => Carets != null && Carets.Length > 0 && Carets[Math.Clamp(index - 1, 0, Carets.Length - 1)].RightToLeft;

    /// <summary>
    /// Grapheme boundary of a line with <see cref="Carets"/> whose caret X is nearest to <paramref name="x"/> (relative
    /// to the line's left edge), the smaller index on a tie, at most <paramref name="maxIndex"/>. Never inside an emoji
    /// sequence or a letter and its marks.
    /// </summary>
    public int NearestStop(float x, int maxIndex = int.MaxValue)
    {
        var text = Value ?? string.Empty;
        if (maxIndex < text.Length)
            text = text.Substring(0, Math.Max(0, maxIndex));
        var best = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; ; i += System.Globalization.StringInfo.GetNextTextElementLength(text, i))
        {
            var distance = Math.Abs(CaretX(i) - x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
            if (i >= text.Length)
                break;
        }
        return best;
    }

    /// <summary>
    /// Visual boxes of the code units [<paramref name="start"/>, <paramref name="end"/>) of a line with
    /// <see cref="Carets"/>, left and right X relative to the line's left edge, touching boxes merged: a selection across
    /// a direction change gives two blocks.
    /// </summary>
    public List<(float Left, float Right)> Boxes(int start, int end)
    {
        var boxes = new List<(float Left, float Right)>();
        if (Carets == null)
            return boxes;

        for (var i = Math.Max(0, start); i < Math.Min(end, Carets.Length); i++)
        {
            if (Carets[i].Advance > 0)
                boxes.Add((Carets[i].Left, Carets[i].Left + Carets[i].Advance));
        }

        boxes.Sort((a, b) => a.Left.CompareTo(b.Left));
        var merged = new List<(float Left, float Right)>();
        foreach (var box in boxes)
        {
            if (merged.Count > 0 && box.Left <= merged[^1].Right + 0.5f)
                merged[^1] = (merged[^1].Left, Math.Max(merged[^1].Right, box.Right));
            else
                merged.Add(box);
        }
        return merged;
    }
}

/// <summary>
/// Visual box of one UTF-16 code unit in a line with right-to-left text: left X relative to the line, advance (a
/// cluster's advance on its first code unit, 0 on the rest) and direction.
/// </summary>
public readonly record struct LineCaret(float Left, float Advance, bool RightToLeft);
