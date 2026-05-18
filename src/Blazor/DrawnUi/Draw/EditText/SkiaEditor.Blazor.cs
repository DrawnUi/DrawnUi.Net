namespace DrawnUi.Draw;

public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
{
    private bool _isSubscribedToKeyboard;
    private int _stubSelectionStop = -1;
    private bool _deferringCursorUpdate;

    // On Blazor, Label.Lines is populated during the render pass, which happens after
    // property-change events. This schedules a visual cursor re-placement once the
    // label has had a chance to re-render with the new text.
    private async void DeferVisualCursorUpdate()
    {
        if (_deferringCursorUpdate) return;
        _deferringCursorUpdate = true;
        await Task.Delay(30);
        _deferringCursorUpdate = false;
        MoveInternalCursor();
    }

    public int NativeSelectionStart => CursorPosition;

    public void SetCursorPositionNative(int position, int stop = -1)
    {
        _stubSelectionStop = stop;
    }

    public void ApplyKeyboardType() { }

    public void DisposePlatform()
    {
        SubscribeToKeyboard(false);
    }

    public void SetFocusNative(bool focus)
    {
        SubscribeToKeyboard(focus);
    }

    public void UpdateNativePosition() { }

    private void SubscribeToKeyboard(bool subscribe)
    {
        if (subscribe == _isSubscribedToKeyboard)
            return;
        _isSubscribedToKeyboard = subscribe;
        if (subscribe)
        {
            KeyboardManager.KeyDown += OnKeyDown;
            KeyboardManager.KeyChar += OnKeyChar;
        }
        else
        {
            KeyboardManager.KeyDown -= OnKeyDown;
            KeyboardManager.KeyChar -= OnKeyChar;
        }
    }

    private void OnKeyDown(object? sender, InputKey key)
    {
        var shift = KeyboardManager.IsShiftPressed;
        var ctrl = KeyboardManager.IsControlPressed;

        switch (key)
        {
            case InputKey.Backspace:
                StubBackspace();
                break;
            case InputKey.Delete:
                StubDelete();
                break;
            case InputKey.Enter:
                StubPressEnter();
                break;
            case InputKey.ArrowLeft:
                StubMoveCursor(-1, shift);
                break;
            case InputKey.ArrowRight:
                StubMoveCursor(1, shift);
                break;
            case InputKey.ArrowUp when IsMultiline:
                HandleVerticalArrow(true);
                break;
            case InputKey.ArrowDown when IsMultiline:
                HandleVerticalArrow(false);
                break;
            case InputKey.Home:
                StubMoveCursor(-CursorPosition, shift);
                break;
            case InputKey.End:
                StubMoveCursor((Text?.Length ?? 0) - CursorPosition, shift);
                break;
            case InputKey.KeyA when ctrl:
                StubSelectAll();
                break;
            case InputKey.KeyV when ctrl:
                // paste not implemented for Blazor yet
                break;
        }
    }

    private void OnKeyChar(object? sender, string ch)
    {
        StubTypeText(ch);
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
        DeferVisualCursorUpdate();
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
        DeferVisualCursorUpdate();
    }

    public void StubMoveCursor(int delta, bool extendSelection = false)
    {
        var textLength = Text?.Length ?? 0;
        var target = Math.Clamp(CursorPosition + delta, 0, textLength);

        if (extendSelection)
        {
            if (!HasSelection)
                _stubSelectionStop = CursorPosition;

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

    private bool HasSelection => SelectionLength > 0;

    private void ReplaceSelection(string insertedText)
    {
        var text = Text ?? string.Empty;
        var selectionStart = Math.Clamp(CursorPosition, 0, text.Length);
        var selectionLength = Math.Clamp(SelectionLength, 0, text.Length - selectionStart);
        var normalized = NormalizeLineBreaks(insertedText);

        Text = text.Remove(selectionStart, selectionLength).Insert(selectionStart, normalized);
        CursorPosition = selectionStart + normalized.Length;
        SelectionLength = 0;
        _stubSelectionStop = -1;
        DeferVisualCursorUpdate();
    }

    private static string NormalizeLineBreaks(string? value)
        => value?.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n') ?? string.Empty;
}
