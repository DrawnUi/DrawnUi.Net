using Android.Content;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.AppCompat.Widget;
using DrawnUi.Draw;
using Java.Lang;
using System.Diagnostics;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
    {


        private int _hiddenEditTextId;
        private ViewGroup _layout;
        private MyTextWatcher _textListener;
        protected Android.Widget.EditText Control { get; set; }

        public void DisposePlatform()
        {
            try
            {
                //CloseKeyboard();

                _layout.RemoveView(Control);
                _layout = null;

                AddObservers(false);
                Control = null;
            }
            catch (System.Exception e)
            {
                Trace.WriteLine(e);
            }
        }

        public void UpdateNativePosition()
        {
            if (Control != null && _layout != null)
            {
                // Get the layout's position on the screen
                int[] layoutLocation = new int[2];
                _layout.GetLocationOnScreen(layoutLocation);

                // Calculate the absolute position of the pseudo-entry on the screen
                int pseudoEntryPositionX = layoutLocation[0] + (int)DrawingRect.Left;
                int pseudoEntryPositionY = layoutLocation[1] + (int)DrawingRect.Bottom;

                // Set the position of the hidden EditText
                Control.SetX(pseudoEntryPositionX);
                Control.SetY(pseudoEntryPositionY);
            }
        }

        protected void AddObservers(bool add)
        {
            if (add)
            {
                _textListener = new MyTextWatcher(this);
                Control.AddTextChangedListener(_textListener);
                Control.EditorAction += Control_EditorAction;
            }
            else
            {
                Control.RemoveTextChangedListener(_textListener);
                Control.EditorAction -= Control_EditorAction;
                _textListener.Dispose();
                _textListener = null;
            }
        }

        private void Control_EditorAction(object sender, TextView.EditorActionEventArgs e)
        {
            if (IsMultiline)
            {
                // Let the native EditText insert the line break and advance selection.
                e.Handled = false;
                return;
            }

            e.Handled = true;
            Submit();
            return;

            if (e.ActionId == ImeAction.Done ||
                (e.Event != null && e.Event.KeyCode == Keycode.Enter && e.Event.Action == KeyEventActions.Down))
            {
                // User has pressed the "Done" key or the "Enter" key.
                // Insert your own logic here.
                e.Handled = true;
                Submit();
            }
            else
            {
                e.Handled = false;
            }
        }

        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            //todo use mapper

            if (propertyName == nameof(MaxLines))
            {
                if (MaxLines == 1)
                {
                    Control.SetMaxLines(1);
                    Control.SetSingleLine(true);
                }
                else
                {
                    if (MaxLines > 1)
                    {
                        Control.SetMaxLines(1);
                    }
                    else
                    {
                        Control.SetMaxLines(0);
                    }
                    Control.SetSingleLine(false);
                }
            }
        }


        void CreateNativeControl()
        {
            _hiddenEditTextId = GenerateUniqueId();
            _layout = (ViewGroup)Superview.Handler?.PlatformView;
            if (_layout != null)
            {
                // Create the hidden EditText if it does not exist
                Control = new AppCompatEditText(Platform.AppContext);
                Control.LayoutParameters = new ViewGroup.LayoutParams(0, 0);
                Control.Id = _hiddenEditTextId;
                Control.Text = this.Text;

                UpdateNativePosition();

                AddObservers(true);

                // Add the EditText to your layout
                _layout.AddView(Control);
            }
        }

        public void SetFocusNative(bool focus, bool closeKeyboard = true)
        {
            try
            {
                if (Control == null)
                {
                    CreateNativeControl();
                }

                if (focus)
                {
                    SetReturnType(this.ReturnType);

                    // Request focus and show the keyboard
                    Control.RequestFocus();

                    InputMethodManager imm = (InputMethodManager)Platform.AppContext.GetSystemService(Context.InputMethodService);
                    imm.ShowSoftInput(Control, ShowFlags.Forced);
                    imm.ToggleSoftInput(ShowFlags.Forced, HideSoftInputFlags.ImplicitOnly);
                }
                else
                {
                    Control.ClearFocus();
                    if (closeKeyboard)
                        CloseKeyboard();
                }
            }
            catch (System.Exception e)
            {
                Trace.WriteLine(e);
            }

        }

        public void SetCursorPositionNative(int position, int stop = -1)
        {
            if (Control == null)
                return;
            try
            {
                if (stop > 0)
                {
                    Control.SetSelection(position, stop);
                }
                else
                {
                    Debug.WriteLine($"[EDITOR] Native trying to set cursor at {position} for a text width {Control.Text.Length} length");
                    Control.SetSelection(position);
                }
            }
            catch (System.Exception e)
            {
                //todo investigate why and when this rarely happens trying to set out of bounds
                Trace.WriteLine(e);
                CursorPosition = 0;
            }
        }

        protected void CloseKeyboard()
        {
            InputMethodManager imm = (InputMethodManager)Platform.AppContext.GetSystemService(Context.InputMethodService);
            imm.HideSoftInputFromWindow(Control.WindowToken, HideSoftInputFlags.None);
        }

        public int GenerateUniqueId()
        {
            long currentTime = DateTime.Now.Ticks;
            int uniqueId = unchecked((int)currentTime);
            return uniqueId;
        }

        public void SetReturnType(ReturnType type)
        {

            switch (type)
            {
            case ReturnType.Go:
            Control.ImeOptions = ImeAction.Go;
            Control.SetSingleLine(true);
            Control.SetImeActionLabel(ActionGo, ImeAction.Go);
            break;
            case ReturnType.Next:
            Control.ImeOptions = ImeAction.Next;
            Control.SetSingleLine(true);
            Control.SetImeActionLabel(ActionNext, ImeAction.Next);
            break;
            case ReturnType.Send:
            Control.SetSingleLine(true);
            Control.ImeOptions = ImeAction.Send;
            Control.SetImeActionLabel(ActionSend, ImeAction.Send);
            break;
            case ReturnType.Search:
            Control.SetSingleLine(true);
            Control.ImeOptions = ImeAction.Search;
            Control.SetImeActionLabel(ActionSearch, ImeAction.Search);
            break;
            default:
            Control.ImeOptions = ImeAction.Done;
            Control.SetSingleLine(this.MaxLines == 1);
            Control.SetImeActionLabel(ActionDone, ImeAction.Done);
            break;
            }
        }

        public class MyTextWatcher : Java.Lang.Object, ITextWatcher
        {
            bool firstRun = true;
            protected override void Dispose(bool disposing)
            {
                _parent = null;

                base.Dispose(disposing);
            }

            private SkiaEditor _parent;

            public MyTextWatcher(SkiaEditor parent)
            {
                _parent = parent;
            }


            public void AfterTextChanged(IEditable s)
            {

                // Called when the text has been changed and the editing process is over.
                // This is where you can check the new cursor position.
                //_parent.Text = s.ToString();

                int selectionStart = _parent.Control.SelectionStart;
                int selectionEnd = _parent.Control.SelectionEnd;

                if (selectionStart != selectionEnd) // there is a text selection
                {
                    //todo process selection, not implemented yet
                    _parent.SetSelection(selectionStart, selectionEnd);
                }
            }


            /// <summary>
            /// This will be read by the parent to check the cursor position at will
            /// </summary>
            public int NativeSelectionStart
            {
                get
                {
                    return _parent.Control.SelectionStart;
                }
            }

            public void BeforeTextChanged(ICharSequence s, int start, int count, int after)
            {
                // Called before the text is changed.
            }


            public void OnTextChanged(ICharSequence s, int start, int before, int count)
            {
                _parent.Text = s.ToString();

                // Called when the text is being changed.
                if (firstRun)
                {
                    //text was set from code, not from user input
                    firstRun = false;
                    return;
                }

                int selectionStart = _parent.Control.SelectionStart;
                int selectionEnd = _parent.Control.SelectionEnd;

                if (selectionStart == selectionEnd) // there is a text selection
                {
                    _parent.SetCursorPositionWithDelay(50, selectionStart);
                }
            }
        }

    }
}
