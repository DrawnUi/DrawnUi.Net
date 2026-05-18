using System.Text;

#if DEBUG
namespace DrawnUi.Draw;

/// <summary>
/// Document model for SkiaRichEditor. Tracks text + per-character format runs.
/// Ported from UnoRichText RichEditTextDocument, adapted to MAUI types.
/// </summary>
public sealed class SkiaEditorDocument
{
    internal event EventHandler? TextChanged;
    internal event EventHandler? FormattingChanged;

    private readonly StringBuilder _buffer = new();
    private readonly List<FormatRun> _runs = new();
    private readonly Stack<DocumentSnapshot> _undoStack = new();
    private readonly Stack<DocumentSnapshot> _redoStack = new();
    private bool _isReplaying;

    private int _selectionStart;
    private int _selectionEnd;

    public int SelectionStart => _selectionStart;
    public int SelectionEnd => _selectionEnd;
    public int Length => _buffer.Length;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string GetText() => _buffer.ToString();

    public void SetSelection(int start, int end)
    {
        var len = _buffer.Length;
        _selectionStart = Math.Clamp(start, 0, len);
        _selectionEnd = Math.Clamp(end, _selectionStart, len);
    }

    public void SetText(string? value)
    {
        _buffer.Clear();
        _runs.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        if (!string.IsNullOrEmpty(value))
            _buffer.Append(value);
        _selectionStart = _selectionEnd = 0;
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Returns immutable snapshot of current formatting runs.</summary>
    public IReadOnlyList<(int Start, int End, RichCharFormat Format)> GetFormattingRuns()
    {
        var result = new (int, int, RichCharFormat)[_runs.Count];
        for (int i = 0; i < _runs.Count; i++)
            result[i] = (_runs[i].Start, _runs[i].End, _runs[i].Format);
        return result;
    }

    /// <summary>Returns effective format at character position (0-based).</summary>
    public RichCharFormat GetFormatAt(int position)
    {
        position = Math.Clamp(position, 0, _buffer.Length);
        foreach (var run in _runs)
        {
            if (run.Start <= position && run.End > position)
                return run.Format;
        }
        // Caret at end of a run
        foreach (var run in _runs)
        {
            if (run.Start < position && run.End == position)
                return run.Format;
        }
        return RichCharFormat.Default;
    }

    public void InsertText(int offset, string text, RichCharFormat? explicitFormat = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        var before = TakeSnapshot();
        offset = Math.Clamp(offset, 0, _buffer.Length);
        int delta = text.Length;

        // Inherit format from surrounding run at insertion point
        RichCharFormat insertFormat = explicitFormat ?? GetFormatAt(offset);

        _buffer.Insert(offset, text);

        // Shift existing runs
        for (int i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.End <= offset)
                continue; // before insertion, unchanged
            else if (run.Start < offset && offset <= run.End)
                _runs[i] = run with { End = run.End + delta }; // extends over insertion
            else
                _runs[i] = run with { Start = run.Start + delta, End = run.End + delta }; // after insertion
        }

        // If the inherited format is non-default, apply it to the inserted span
        // (only needed when insertion is NOT inside an existing run — that case
        //  is already handled by run extension above; but if there was no surrounding
        //  run and we have an explicit format, we need to create a new run)
        bool insertedInsideRun = _runs.Any(r => r.Start <= offset && r.End >= offset + delta && r.End > offset);
        if (!insertedInsideRun && !insertFormat.IsEffectivelyEmpty)
            ApplyFormatCore(offset, offset + delta, insertFormat);

        NormalizeRuns();
        PushUndo(before);
        if (!_isReplaying)
            TextChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteRange(int start, int end)
    {
        start = Math.Clamp(start, 0, _buffer.Length);
        end = Math.Clamp(end, start, _buffer.Length);
        if (end <= start) return;

        var before = TakeSnapshot();
        int length = end - start;
        _buffer.Remove(start, length);

        for (int i = _runs.Count - 1; i >= 0; i--)
        {
            var run = _runs[i];
            if (run.End <= start)
            {
                // entirely before deletion, unchanged
            }
            else if (run.Start >= end)
            {
                // entirely after deletion, shift left
                _runs[i] = run with { Start = run.Start - length, End = run.End - length };
            }
            else
            {
                // overlaps deletion range — trim or remove
                int leftKeep = Math.Max(0, start - run.Start);
                int rightKeep = Math.Max(0, run.End - end);
                int keptLength = leftKeep + rightKeep;
                if (keptLength <= 0)
                {
                    _runs.RemoveAt(i);
                }
                else
                {
                    int newStart = Math.Min(run.Start, start);
                    _runs[i] = run with { Start = newStart, End = newStart + keptLength };
                }
            }
        }

        var len = _buffer.Length;
        _selectionStart = Math.Min(_selectionStart, len);
        _selectionEnd = Math.Min(_selectionEnd, len);

        NormalizeRuns();
        PushUndo(before);
        if (!_isReplaying)
            TextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Applies <paramref name="delta"/> on top of existing formatting in [start, end).
    /// Merges per-field: delta null fields preserve existing, non-null fields override.
    /// </summary>
    public void ApplyFormat(int start, int end, RichCharFormat delta)
    {
        start = Math.Clamp(start, 0, _buffer.Length);
        end = Math.Clamp(end, start, _buffer.Length);
        if (start >= end) return;

        var before = TakeSnapshot();
        ApplyFormatCore(start, end, delta);
        NormalizeRuns();
        PushUndo(before);
        if (!_isReplaying)
            FormattingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Toggles a single boolean format property over [start, end).
    /// If ALL characters already have it → turns it off; otherwise turns it on.
    /// </summary>
    public void ToggleBold(int start, int end)
        => ToggleBoolFormat(start, end, f => f.Bold == true, on => new RichCharFormat { Bold = on });

    public void ToggleItalic(int start, int end)
        => ToggleBoolFormat(start, end, f => f.Italic == true, on => new RichCharFormat { Italic = on });

    public void ToggleUnderline(int start, int end)
        => ToggleBoolFormat(start, end, f => f.Underline == true, on => new RichCharFormat { Underline = on });

    public void ToggleStrikethrough(int start, int end)
        => ToggleBoolFormat(start, end, f => f.Strikethrough == true, on => new RichCharFormat { Strikethrough = on });

    public void Undo()
    {
        if (!CanUndo) return;
        var current = TakeSnapshot();
        var prev = _undoStack.Pop();
        _redoStack.Push(current);
        RestoreSnapshot(prev);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var current = TakeSnapshot();
        var next = _redoStack.Pop();
        _undoStack.Push(current);
        RestoreSnapshot(next);
    }

    // ---- Private helpers --------------------------------------------------------

    private void ToggleBoolFormat(int start, int end, Func<RichCharFormat, bool> check, Func<bool, RichCharFormat> build)
    {
        start = Math.Clamp(start, 0, _buffer.Length);
        end = Math.Clamp(end, start, _buffer.Length);
        if (start >= end) return;

        bool allHave = true;
        for (int i = start; i < end; i++)
        {
            if (!check(GetFormatAt(i))) { allHave = false; break; }
        }
        ApplyFormat(start, end, build(!allHave));
    }

    private void ApplyFormatCore(int start, int end, RichCharFormat delta)
    {
        // Collect runs outside [start,end) unchanged; split those that cross boundaries
        var outside = new List<FormatRun>();
        foreach (var run in _runs)
        {
            if (run.End <= start || run.Start >= end)
            {
                outside.Add(run);
                continue;
            }
            if (run.Start < start)
                outside.Add(run with { End = start });
            if (run.End > end)
                outside.Add(run with { Start = end });
        }

        // Walk through [start,end), merging delta on top of existing format per sub-segment
        var inside = new List<FormatRun>();
        int cursor = start;
        foreach (var run in _runs.Where(r => r.End > start && r.Start < end).OrderBy(r => r.Start))
        {
            int segStart = Math.Max(run.Start, start);
            int segEnd = Math.Min(run.End, end);

            if (cursor < segStart)
                inside.Add(new FormatRun(cursor, segStart, delta)); // gap: only delta

            var merged = run.Format.MergeWith(delta);
            inside.Add(new FormatRun(segStart, segEnd, merged));
            cursor = segEnd;
        }
        if (cursor < end)
            inside.Add(new FormatRun(cursor, end, delta)); // trailing gap

        _runs.Clear();
        _runs.AddRange(outside);
        _runs.AddRange(inside);
    }

    private void NormalizeRuns()
    {
        var text = _buffer.ToString();
        int len = text.Length;
        var normalized = new List<FormatRun>();

        foreach (var run in _runs.OrderBy(r => r.Start))
        {
            int s = Math.Clamp(run.Start, 0, len);
            int e = Math.Clamp(run.End, s, len);
            if (e <= s || run.Format.IsEffectivelyEmpty) continue;

            // Split at paragraph separators — format never crosses \n/\r
            int segStart = s;
            for (int i = s; i < e; i++)
            {
                if (text[i] == '\n' || text[i] == '\r')
                {
                    if (segStart < i)
                        normalized.Add(new FormatRun(segStart, i, run.Format));
                    segStart = i + 1;
                }
            }
            if (segStart < e)
                normalized.Add(new FormatRun(segStart, e, run.Format));
        }

        // Merge adjacent runs with equal format
        _runs.Clear();
        FormatRun? prev = null;
        foreach (var run in normalized.OrderBy(r => r.Start))
        {
            if (prev == null) { prev = run; continue; }
            if (prev.End == run.Start && prev.Format.Equals(run.Format))
                prev = prev with { End = run.End };
            else
            {
                _runs.Add(prev);
                prev = run;
            }
        }
        if (prev != null) _runs.Add(prev);
    }

    private DocumentSnapshot TakeSnapshot() =>
        new(_buffer.ToString(), _runs.ToList(), _selectionStart, _selectionEnd);

    private void PushUndo(DocumentSnapshot before)
    {
        if (!_isReplaying)
        {
            _undoStack.Push(before);
            _redoStack.Clear();
        }
    }

    private void RestoreSnapshot(DocumentSnapshot snap)
    {
        _isReplaying = true;
        try
        {
            _buffer.Clear();
            _buffer.Append(snap.Text);
            _runs.Clear();
            _runs.AddRange(snap.Runs);
            var len = _buffer.Length;
            _selectionStart = Math.Clamp(snap.SelectionStart, 0, len);
            _selectionEnd = Math.Clamp(snap.SelectionEnd, _selectionStart, len);
        }
        finally { _isReplaying = false; }

        TextChanged?.Invoke(this, EventArgs.Empty);
        FormattingChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed record FormatRun(int Start, int End, RichCharFormat Format);
    private sealed record DocumentSnapshot(string Text, List<FormatRun> Runs, int SelectionStart, int SelectionEnd);
}
#endif
