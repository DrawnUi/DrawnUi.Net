namespace DrawnUi.Draw;

public partial class SkiaEditor
{
    private int _stubSelectionStop = -1;

    public void SetCursorPositionNative(int position, int stop = -1)
    {
        _stubSelectionStop = stop;
    }

    public void DisposePlatform()
    {
    }

    public void SetFocusNative(bool focus)
    {
    }

    public void UpdateNativePosition()
    {
    }

    public void StubTypeText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        ReplaceSelection(NormalizeLineBreaks(value));
    }

    public void StubPressEnter()
    {
        if (IsMultiline)
        {
            ReplaceSelection("\n");
            return;
        }

        Submit();
    }

    public void StubBackspace(int count = 1)
    {
        if (count <= 0)
            return;

        if (HasSelection)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        var text = Text ?? string.Empty;
        if (text.Length == 0)
            return;

        var remove = Math.Min(count, Math.Max(0, CursorPosition));
        if (remove == 0)
            return;

        var start = CursorPosition - remove;
        Text = text.Remove(start, remove);
        CursorPosition = start;
        SelectionLength = 0;
    }

    public void StubDelete(int count = 1)
    {
        if (count <= 0)
            return;

        if (HasSelection)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        var text = Text ?? string.Empty;
        if (text.Length == 0 || CursorPosition >= text.Length)
            return;

        var remove = Math.Min(count, text.Length - CursorPosition);
        Text = text.Remove(CursorPosition, remove);
        SelectionLength = 0;
    }

    public void StubMoveCursor(int delta, bool extendSelection = false)
    {
        var textLength = Text?.Length ?? 0;
        var target = Math.Clamp(CursorPosition + delta, 0, textLength);

        if (extendSelection)
        {
            if (!HasSelection)
            {
                _stubSelectionStop = CursorPosition;
            }

            CursorPosition = Math.Min(target, _stubSelectionStop);
            SelectionLength = Math.Abs(target - _stubSelectionStop);
            return;
        }

        CursorPosition = target;
        SelectionLength = 0;
        _stubSelectionStop = -1;
    }

    public void StubSelectRange(int start, int length)
    {
        var textLength = Text?.Length ?? 0;
        var normalizedStart = Math.Clamp(start, 0, textLength);
        var normalizedLength = Math.Clamp(length, 0, textLength - normalizedStart);

        CursorPosition = normalizedStart;
        SelectionLength = normalizedLength;
        _stubSelectionStop = normalizedStart + normalizedLength;
    }

    public void StubSelectAll()
    {
        StubSelectRange(0, Text?.Length ?? 0);
    }

    public void StubMoveCursorToLineColumn(int line, int column, bool extendSelection = false)
    {
        var target = GetIndexFromLineColumn(Text, line, column);
        var delta = target - CursorPosition;
        StubMoveCursor(delta, extendSelection);
    }

    public void StubSelectLineColumnRange(int startLine, int startColumn, int endLine, int endColumn)
    {
        var text = Text ?? string.Empty;
        var start = GetIndexFromLineColumn(text, startLine, startColumn);
        var end = GetIndexFromLineColumn(text, endLine, endColumn);

        if (end < start)
        {
            (start, end) = (end, start);
        }

        StubSelectRange(start, end - start);
    }

    private bool HasSelection => SelectionLength > 0;

    private void ReplaceSelection(string insertedText)
    {
        var text = Text ?? string.Empty;
        var selectionStart = Math.Clamp(CursorPosition, 0, text.Length);
        var selectionLength = Math.Clamp(SelectionLength, 0, text.Length - selectionStart);
        var normalizedInsertedText = NormalizeLineBreaks(insertedText);

        var updated = text.Remove(selectionStart, selectionLength).Insert(selectionStart, normalizedInsertedText);

        Text = updated;
        CursorPosition = selectionStart + normalizedInsertedText.Length;
        SelectionLength = 0;
        _stubSelectionStop = -1;
    }

    private static string NormalizeLineBreaks(string? value)
    {
        return value?
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n') ?? string.Empty;
    }

    private static int GetIndexFromLineColumn(string? value, int line, int column)
    {
        var text = NormalizeLineBreaks(value);
        var targetLine = Math.Max(0, line);
        var targetColumn = Math.Max(0, column);
        var index = 0;
        var currentLine = 0;

        while (currentLine < targetLine && index < text.Length)
        {
            var lineBreak = text.IndexOf('\n', index);
            if (lineBreak < 0)
            {
                return text.Length;
            }

            index = lineBreak + 1;
            currentLine++;
        }

        var lineEnd = text.IndexOf('\n', index);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        return Math.Min(index + targetColumn, lineEnd);
    }
}