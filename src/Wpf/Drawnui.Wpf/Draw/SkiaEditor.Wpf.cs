namespace DrawnUi.Draw;

/// <summary>
/// WPF-side editor keys that need the laid-out lines: Up / Down arrows in a multiline editor.
/// The horizontal stubs (<c>StubMoveCursor</c>) live with the other desktop stubs in the Net head.
/// </summary>
public partial class SkiaEditor
{
    /// <summary>
    /// Moves the caret one visual line up (<paramref name="direction"/> -1) or down (+1), keeping its
    /// x position; past the first / last line it goes to the start / end of the text.
    /// </summary>
    /// <returns>False when the editor is single-line or has no laid-out lines, so the key can stay with the host.</returns>
    public bool StubMoveLine(int direction, bool extendSelection = false)
    {
        var lines = Label?.Lines;
        if (!IsMultiline || lines == null || lines.Length == 0 || direction == 0)
            return false;

        var textLength = Text?.Length ?? 0;
        var caret = extendSelection && HasSelection && _selectionMovingEdge >= 0 ? _selectionMovingEdge : CursorPosition;

        // find the line holding the caret and the caret's x inside it
        var first = 0;
        var lineIndex = lines.Length - 1;
        var caretX = lines[^1].Bounds.Left;
        for (var i = 0; i < lines.Length; i++)
        {
            var glyphs = GetLineGlyphs(lines[i]);
            var next = AdvanceLineTextIndex(first, glyphs.Length);
            if (caret < next || i == lines.Length - 1)
            {
                lineIndex = i;
                var inLine = Math.Clamp(caret - first, 0, glyphs.Length);
                caretX = lines[i].Bounds.Left + (inLine == 0 || glyphs.Length == 0 ? 0 : glyphs[inLine - 1].Position);
                break;
            }

            first = next;
        }

        var target = lineIndex + Math.Sign(direction);
        int position;
        if (target < 0)
            position = 0;
        else if (target >= lines.Length)
            position = textLength;
        else
            position = GetCursorPosition(caretX + 0.5f, lines[target].Bounds.MidY);

        StubMoveCursor(position - caret, extendSelection);
        return true;
    }
}
