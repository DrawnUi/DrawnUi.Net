namespace DrawnUi.Draw;

public partial class SkiaEditor
{
    private int _stubSelectionStop = -1;
    // Active (moving) edge of a keyboard shift-selection. _stubSelectionStop is the fixed anchor;
    // this is the caret side that grows/shrinks. Tracked separately because CursorPosition is
    // pinned to the selection's LEFT edge (the overlay renders [CursorPosition, +SelectionLength]).
    private int _selectionMovingEdge = -1;

    public void SetCursorPositionNative(int position, int stop = -1)
    {
        _stubSelectionStop = stop;
    }

#if WEB
    // The pure WebAssembly head has no host that types into the focused editor (OpenTK and WPF call the canvas's
    // desktop editor methods): a focused editor takes the page's keys from KeyboardManager, as on Blazor.
    private bool _isSubscribedToKeyboard;

    public void DisposePlatform()
    {
        SubscribeToKeyboard(false);
    }

    public void SetFocusNative(bool focus)
    {
        // a late defocus after a refocus keeps the keys
        if (!focus && IsFocused)
            return;
        SubscribeToKeyboard(focus);
        WebTextInput.Focus(this, focus); // the page's text input: on-screen keyboard, input methods, key repeat, paste
    }

    private void SubscribeToKeyboard(bool subscribe)
    {
        if (subscribe == _isSubscribedToKeyboard)
            return;
        _isSubscribedToKeyboard = subscribe;
        if (subscribe)
        {
            KeyboardManager.KeyDown += OnKeyboardKeyDown;
            KeyboardManager.KeyChar += OnKeyboardChar;
        }
        else
        {
            KeyboardManager.KeyDown -= OnKeyboardKeyDown;
            KeyboardManager.KeyChar -= OnKeyboardChar;
        }
    }

    private void OnKeyboardKeyDown(object sender, InputKey key)
    {
        // a page element next to the canvas has the keyboard (an input, a button): its keys are not typing here
        if (KeyboardManager.IsKeyForOtherElement)
            return;

        var shift = KeyboardManager.IsShiftPressed;
        var ctrl = KeyboardManager.IsControlPressed || KeyboardManager.IsMetaPressed;
        var alt = KeyboardManager.IsAltPressed;
        switch (key)
        {
            case InputKey.Backspace: StubBackspace(); break;
            case InputKey.Delete: StubDelete(); break;
            case InputKey.Enter: StubPressEnter(alt, shift); break;
            case InputKey.ArrowLeft: StubArrow(false, shift); break;
            case InputKey.ArrowRight: StubArrow(true, shift); break;
            case InputKey.ArrowUp when IsMultiline: HandleVerticalArrow(true); break;
            case InputKey.ArrowDown when IsMultiline: HandleVerticalArrow(false); break;
            case InputKey.Home: StubMoveCursor(-CursorPosition, shift); break;
            case InputKey.End: StubMoveCursor((Text?.Length ?? 0) - CursorPosition, shift); break;
            case InputKey.KeyA when ctrl: StubSelectAll(); break;
            case InputKey.KeyC when ctrl: CopySelectionToClipboard(); break;
            case InputKey.KeyX when ctrl:
                if (CopySelectionToClipboard())
                    CutSelection();
                break;
            // Ctrl / Cmd + V: the browser's paste event types the text (WebTextInput)
        }
    }

    bool CopySelectionToClipboard()
    {
        var text = GetSelectedText();
        if (string.IsNullOrEmpty(text))
            return false;
        Super.SetClipboardText?.Invoke(text);
        return true;
    }

    private void OnKeyboardChar(object sender, string text)
    {
        if (KeyboardManager.IsKeyForOtherElement)
            return;
        StubTypeText(text);
    }
#else
    public void DisposePlatform()
    {
    }

    public void SetFocusNative(bool focus)
    {
    }
#endif

    // No native text input here: keyboard type, password mode and IsSpellCheckEnabled
    // have no platform counterpart to apply.
    public void ApplyKeyboardType()
    {
    }

    partial void PlatformClearFocusNow()
    {
    }

    public void UpdateNativePosition()
    {
    }

    public void StubTypeText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        ReplaceSelection(value);
    }

    public void StubPressEnter(bool splitLine = false, bool shift = false)
    {
        if (IsMultiline)
        {
            if (!splitLine && !shift && ShouldSubmitOnEnter)
            {
                ExecuteSubmit(clearFocus: false);
                return;
            }

            ReplaceSelection(GetEditorBreakText(splitLine));
            return;
        }

        ExecuteSubmit(clearFocus: false);
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

        var remove = CodeUnitsBeforeCaret(text, CursorPosition, count);
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

        var remove = CodeUnitsAfterCaret(text, CursorPosition, count);
        if (remove == 0)
            return;
        Text = text.Remove(CursorPosition, remove);
        SelectionLength = 0;
    }

    public void StubMoveCursor(int delta, bool extendSelection = false)
    {
        var textLength = Text?.Length ?? 0;

        if (extendSelection)
        {
            // Anchor stays put; only the moving edge follows the arrow. Re-seed both when there is
            // no live selection (including after a shift-select collapsed back onto the anchor).
            if (!HasSelection)
            {
                _stubSelectionStop = CursorPosition;
                _selectionMovingEdge = CursorPosition;
            }

            _selectionMovingEdge = Math.Clamp(_selectionMovingEdge + delta, 0, textLength);
            SelectionLength = Math.Abs(_selectionMovingEdge - _stubSelectionStop);
            CursorPosition = Math.Min(_selectionMovingEdge, _stubSelectionStop);
            return;
        }

        // Plain arrow with a live selection collapses to the edge in the move direction.
        if (HasSelection)
            CursorPosition = delta < 0 ? CursorPosition : CursorPosition + SelectionLength;
        else
            CursorPosition = Math.Clamp(CursorPosition + delta, 0, textLength);

        SelectionLength = 0;
        _stubSelectionStop = -1;
        _selectionMovingEdge = -1;
    }

    /// <summary>
    /// Left / Right arrow. On a line with right-to-left text the caret moves visually (<see cref="VisualArrowTarget"/>)
    /// and a plain arrow collapses a selection to its left or right edge on screen; elsewhere one step in text order.
    /// </summary>
    public void StubArrow(bool right, bool extendSelection = false)
    {
        if (!extendSelection && HasSelection)
        {
            var edge = SelectionEdgeByX(CursorPosition, CursorPosition + SelectionLength, right);
            if (edge < 0)
            {
                StubMoveCursor(right ? 1 : -1);
                return;
            }
            CursorPosition = edge;
            SelectionLength = 0;
            _stubSelectionStop = -1;
            _selectionMovingEdge = -1;
            return;
        }

        var from = extendSelection && HasSelection && _selectionMovingEdge >= 0 ? _selectionMovingEdge : CursorPosition;
        var target = VisualArrowTarget(from, right);
        if (target < 0)
            StubMoveCursor(right ? 1 : -1, extendSelection);
        else if (target != from)
            StubMoveCursor(target - from, extendSelection);
    }

    public void StubSelectRange(int start, int length)
    {
        var textLength = Text?.Length ?? 0;
        var normalizedStart = Math.Clamp(start, 0, textLength);
        var normalizedLength = Math.Clamp(length, 0, textLength - normalizedStart);

        CursorPosition = normalizedStart;
        SelectionLength = normalizedLength;
        _stubSelectionStop = normalizedStart;
        _selectionMovingEdge = normalizedStart + normalizedLength;
    }

    public void StubSelectAll()
    {
        StubSelectRange(0, Text?.Length ?? 0);
    }

    partial void SeedKeyboardSelection(int anchor, int movingEdge)
    {
        _stubSelectionStop = anchor;
        _selectionMovingEdge = movingEdge;
    }

    private bool HasSelection => SelectionLength > 0;

    private void ReplaceSelection(string insertedText)
    {
        var text = Text ?? string.Empty;
        var selectionStart = Math.Clamp(CursorPosition, 0, text.Length);
        var selectionLength = Math.Clamp(SelectionLength, 0, text.Length - selectionStart);
        var normalizedInsertedText = NormalizeEditorInput(insertedText);

        var updated = text.Remove(selectionStart, selectionLength).Insert(selectionStart, normalizedInsertedText);

        Text = updated;
        CursorPosition = selectionStart + normalizedInsertedText.Length;
        SelectionLength = 0;
        _stubSelectionStop = -1;
    }
}
