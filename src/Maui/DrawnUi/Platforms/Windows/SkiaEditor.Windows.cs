using DrawnUi.Draw;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics;
using TextChangedEventArgs = Microsoft.UI.Xaml.Controls.TextChangedEventArgs;
using Visibility = Microsoft.UI.Xaml.Visibility;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
    {
        private TextBox _hiddenTextBox;
        private bool _updatingText;
        private bool _suppressSelectionChanged;

        public int NativeSelectionStart
        {
            get
            {
                if (_hiddenTextBox != null)
                {
                    return _hiddenTextBox.SelectionStart;
                }
                return 0;
            }
        }

        public void SetCursorPositionNative(int position, int stop = -1)
        {
            if (_hiddenTextBox == null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (_hiddenTextBox == null)
                        return;
                    var len = _hiddenTextBox.Text?.Length ?? 0;
                    _hiddenTextBox.SelectionStart = Math.Min(position, len);
                    _hiddenTextBox.SelectionLength = stop >= 0
                        ? Math.Max(0, Math.Min(stop, len) - _hiddenTextBox.SelectionStart)
                        : 0;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[SetCursorPositionNative] {e}");
                }
            });
        }

        public void DisposePlatform()
        {
            try
            {
                if (_hiddenTextBox != null)
                {
                    _hiddenTextBox.TextChanged -= HiddenTextBox_TextChanged;
                    _hiddenTextBox.SelectionChanged -= HiddenTextBox_SelectionChanged;
                    _hiddenTextBox.GotFocus -= HiddenTextBox_GotFocus;
                    _hiddenTextBox.KeyDown -= HiddenTextBox_KeyDown;

                    var layout = (Panel)Superview?.Handler?.PlatformView;
                    if (layout != null)
                    {
                        layout.Children.Remove(_hiddenTextBox);
                    }
                    _hiddenTextBox = null;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[DisposePlatform] {e}");
            }
        }

        // TextBox is an off-screen keyboard sink — size and position are fixed.
        public void UpdateNativePosition() { }

        private void EnsureTextBox()
        {
            if (_hiddenTextBox != null)
                return;

            var layout = (Panel)Superview?.Handler?.PlatformView;
            Debug.WriteLine($"[SkiaEditor] EnsureTextBox layout={layout?.GetType().Name ?? "NULL"} superview={Superview?.GetType().Name ?? "NULL"}");
            if (layout == null)
                return;

            _hiddenTextBox = new TextBox
            {
                IsReadOnly = false,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Width = 1,
                Height = 1,
                Visibility = Visibility.Visible,
                Name = "HiddenTextBox" + GenerateUniqueId(),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0))
            };

            _hiddenTextBox.TextChanged += HiddenTextBox_TextChanged;
            _hiddenTextBox.SelectionChanged += HiddenTextBox_SelectionChanged;
            _hiddenTextBox.GotFocus += HiddenTextBox_GotFocus;
            _hiddenTextBox.KeyDown += HiddenTextBox_KeyDown;

            layout.Children.Add(_hiddenTextBox);

            // keep off-screen so WinUI native hit-testing never intercepts canvas taps
            _hiddenTextBox.Measure(new Windows.Foundation.Size(1, 1));
            _hiddenTextBox.Arrange(new Windows.Foundation.Rect(-10, -10, 1, 1));
        }

        public void SetFocusNative(bool focus)
        {
            Debug.WriteLine($"[SkiaEditor] SetFocusNative focus={focus} textBox={_hiddenTextBox != null}");
            try
            {
                if (focus)
                {
                    EnsureTextBox();

                    if (_hiddenTextBox == null)
                        return;

                    // always sync text before focusing — TextBox may be stale if Text changed while unfocused
                    if (!_updatingText)
                    {
                        _updatingText = true;
                        _hiddenTextBox.Text = this.Text ?? string.Empty;
                        _updatingText = false;
                    }

                    _suppressSelectionChanged = true;
                    _hiddenTextBox.Focus(FocusState.Programmatic);
                }
                // on defocus: do nothing — TextBox stays in tree, just loses WinUI focus naturally.
                // No destroy/recreate race possible.
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[SetFocusNative] {e}");
            }
        }

        private void HiddenTextBox_TextChanged(object sender, TextChangedEventArgs textChangedEventArgs)
        {
            //Debug.WriteLine($"[SkiaEditor] TextChanged updatingText={_updatingText} newText='{_hiddenTextBox?.Text}'");
            if (!_updatingText)
            {
                _updatingText = true;
                // normalize Windows line endings
                Text = _hiddenTextBox.Text?.Replace("\r\n", "\n").Replace("\r", "\n");
                _updatingText = false;
            }
        }

        private void HiddenTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !IsMultiline)
            {
                e.Handled = true;
                Submit();
                return;
            }

            if (IsMultiline && (e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down))
            {
                e.Handled = true;
                HandleVerticalArrow(e.Key == Windows.System.VirtualKey.Up);
                return;
            }

            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) && e.Key == Windows.System.VirtualKey.A)
            {
                e.Handled = true;
                SelectAll();
            }
        }

        private void HandleVerticalArrow(bool up)
        {
            if (Label?.Lines == null || Label.LinesCount <= 1) return;

            var curLine = GetCursorLine();
            var targetLine = up ? curLine - 1 : curLine + 1;

            if (targetLine < 0 || targetLine >= Label.LinesCount) return;

            // cursor X in content pixels (same coordinate space as glyph positions)
            var cursorXPixels = (float)(Cursor.Left * RenderingScale);

            // walk lines to find target line's start character index
            var lineStart = 0;
            for (var i = 0; i < targetLine; i++)
                lineStart = AdvanceLineTextIndex(lineStart, GetLineGlyphs(Label.Lines[i]).Length);

            // find character on target line closest to cursorXPixels
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

            // past end of target line — clamp back before any trailing '\n'
            var pos = lineStart + glyphs.Length;
            if (Text != null && pos > 0 && pos - 1 < Text.Length && Text[pos - 1] == '\n')
                pos--;
            CursorPosition = pos;
        }

        private void HiddenTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectionChanged) return;
            var start = _hiddenTextBox.SelectionStart;
            var length = _hiddenTextBox.SelectionLength;
            SelectionLength = length;
            // cursor at end of selection (forward selection is the common case)
            SetCursorPositionWithDelay(16, start + length);
        }

        private void HiddenTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"[SkiaEditor] GotFocus CursorPosition={CursorPosition} textLen={_hiddenTextBox?.Text?.Length ?? -1}");
            if (_hiddenTextBox == null) return;
            var pos = Math.Max(0, Math.Min(CursorPosition, _hiddenTextBox.Text?.Length ?? 0));
            _hiddenTextBox.SelectionStart = pos;
            _hiddenTextBox.SelectionLength = 0;
            SelectionLength = 0;
            _suppressSelectionChanged = false;
        }

        public int GenerateUniqueId()
        {
            long currentTime = DateTime.Now.Ticks;
            int uniqueId = unchecked((int)currentTime);
            return uniqueId;
        }
    }
}
