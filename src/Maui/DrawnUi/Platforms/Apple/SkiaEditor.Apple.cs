using CoreGraphics;
using DrawnUi.Draw;
using Foundation;
using UIKit;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
    {
        private bool _updatingText;

        public class TextViewDelegate : UITextViewDelegate
        {
            private readonly SkiaEditor _editor;
            private bool _firstSynced;

            public TextViewDelegate(SkiaEditor editor) => _editor = editor;

            public override void Changed(UITextView textView)
            {
                if (_editor._updatingText)
                    return;

                _editor._updatingText = true;
                _editor.Text = textView.Text?.Replace("\r\n", "\n").Replace("\r", "\n");
                _editor._updatingText = false;
            }

            public override void SelectionChanged(UITextView textView)
            {
                if (!_firstSynced)
                {
                    _firstSynced = true;
                    return;
                }

                var range = textView.SelectedRange;
                var location = (int)range.Location;
                var length = (int)range.Length;

                _editor.SelectionLength = length;
                _editor.SetCursorPositionWithDelay(50, location + length);
            }

            public override bool ShouldChangeText(UITextView textView, NSRange range, string text)
            {
                if (!_editor.IsMultiline && text == "\n")
                {
                    _editor.Submit();
                    return false;
                }
                return true;
            }
        }

        protected NativeEntryView Control;
        private UIView _layout;

        public int NativeSelectionStart
        {
            get
            {
                if (Control == null) return 0;
                return (int)Control.SelectedRange.Location;
            }
        }

        public void SetCursorPositionNative(int position, int stop = -1)
        {
            if (Control == null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Control == null) return;
                var len = (int)(Control.Text?.Length ?? 0);
                var clampedPos = Math.Min(position, len);
                var clampedStop = stop >= 0 ? Math.Min(stop, len) : clampedPos;
                Control.SelectedRange = new NSRange(clampedPos, Math.Max(0, clampedStop - clampedPos));
            });
        }

        public void DisposePlatform()
        {
            if (Control != null)
            {
                Control.Delegate = null;
                Control.ResignFirstResponder();
                Control.RemoveFromSuperview();
                Control = null;
            }
            _layout = null;
        }

        public void UpdateNativePosition()
        {
            if (Control != null)
            {
                Control.InputAccessoryView = null;
                Control.AutocorrectionType = UITextAutocorrectionType.No;
                Control.Frame = new CGRect(DrawingRect.Right / RenderingScale, DrawingRect.Bottom / RenderingScale, 1, 1);
            }
        }

        void CreateNativeControl()
        {
            Control = new NativeEntryView
            {
                Frame = new CGRect(-10, -10, 1, 1),
                AccessibilityIdentifier = "NativeEntry" + GenerateUniqueId(),
                ScrollEnabled = false,
            };

            Control.TextContainerInset = UIEdgeInsets.Zero;
            Control.TextContainer.LineFragmentPadding = 0;

            _updatingText = true;
            Control.Text = this.Text ?? string.Empty;
            _updatingText = false;

            Control.Delegate = new TextViewDelegate(this);

            _layout.AddSubview(Control);
        }

        public void SetFocusNative(bool focus)
        {
            try
            {
                _layout = (UIView)Superview?.Handler?.PlatformView;

                System.Diagnostics.Debug.WriteLine("[SkiaEditor] SetFocusNative " + focus);

                if (focus)
                {
                    if (Control == null)
                        CreateNativeControl();

                    if (!_updatingText)
                    {
                        _updatingText = true;
                        Control.Text = this.Text ?? string.Empty;
                        _updatingText = false;
                    }

                    Control.IsFocused = true;
                    Control.BecomeFirstResponder();
                }
                else
                {
                    if (Control != null)
                    {
                        Control.IsFocused = false;
                        Control.ResignFirstResponder();
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
        }

        public void SetReturnType(ReturnType type)
        {
            if (Control == null) return;
            switch (type)
            {
                case ReturnType.Go:     Control.ReturnKeyType = UIReturnKeyType.Go;     break;
                case ReturnType.Next:   Control.ReturnKeyType = UIReturnKeyType.Next;   break;
                case ReturnType.Send:   Control.ReturnKeyType = UIReturnKeyType.Send;   break;
                case ReturnType.Search: Control.ReturnKeyType = UIReturnKeyType.Search; break;
                default:                Control.ReturnKeyType = UIReturnKeyType.Done;   break;
            }
        }

        public int GenerateUniqueId()
        {
            long currentTime = DateTime.Now.Ticks;
            int uniqueId = unchecked((int)currentTime);
            return uniqueId;
        }

        public class NativeEntryView : UITextView
        {
            public bool IsFocused { get; set; }

            public override bool CanResignFirstResponder => !IsFocused;
        }
    }
}
