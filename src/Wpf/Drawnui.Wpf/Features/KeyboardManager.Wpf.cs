using System.Windows.Input;

namespace DrawnUi.Draw;

/// <summary>
/// WPF side of the shared <see cref="KeyboardManager"/>: maps WPF keys to <see cref="InputKey"/>.
/// <see cref="DrawnUi.Wpf.DrawnUiElement"/> feeds every key it sees, so
/// <see cref="KeyboardManager.KeyDown"/>, <see cref="KeyboardManager.KeyUp"/> and
/// <see cref="KeyboardManager.KeyChar"/> fire while the element has keyboard focus.
/// </summary>
public partial class KeyboardManager
{
    /// <summary>Maps a WPF key (resolve <c>Key.System</c> to <c>SystemKey</c> first) to the shared key code.</summary>
    public static InputKey MapKey(Key key) => key switch
    {
        Key.Space => InputKey.Space,
        Key.Left => InputKey.ArrowLeft,
        Key.Up => InputKey.ArrowUp,
        Key.Right => InputKey.ArrowRight,
        Key.Down => InputKey.ArrowDown,
        >= Key.D0 and <= Key.D9 => InputKey.Digit0 + (key - Key.D0),
        >= Key.A and <= Key.Z => InputKey.KeyA + (key - Key.A),
        >= Key.NumPad0 and <= Key.NumPad9 => InputKey.Numpad0 + (key - Key.NumPad0),
        >= Key.F1 and <= Key.F12 => InputKey.F1 + (key - Key.F1),
        Key.CapsLock => InputKey.CapsLock,
        Key.Insert => InputKey.Insert,
        Key.Delete => InputKey.Delete,
        Key.Snapshot => InputKey.PrintScreen,
        Key.Home => InputKey.Home,
        Key.End => InputKey.End,
        Key.PageDown => InputKey.PageDown,
        Key.PageUp => InputKey.PageUp,
        Key.Escape => InputKey.Escape,
        Key.Pause => InputKey.Pause,
        Key.LeftAlt => InputKey.AltLeft,
        Key.RightAlt => InputKey.AltRight,
        Key.LeftShift => InputKey.ShiftLeft,
        Key.RightShift => InputKey.ShiftRight,
        Key.LeftCtrl => InputKey.ControlLeft,
        Key.RightCtrl => InputKey.ControlRight,
        Key.Enter => InputKey.Enter,
        Key.Tab => InputKey.Tab,
        Key.Back => InputKey.Backspace,
        Key.NumLock => InputKey.NumLock,
        Key.Scroll => InputKey.ScrollLock,
        Key.LWin => InputKey.MetaLeft,
        Key.RWin => InputKey.MetaRight,
        Key.Apps => InputKey.ContextMenu,
        Key.Divide => InputKey.NumpadDivide,
        Key.Multiply => InputKey.NumpadMultiply,
        Key.Subtract => InputKey.NumpadSubtract,
        Key.Add => InputKey.NumpadAdd,
        Key.Decimal => InputKey.NumpadDecimal,
        Key.VolumeMute => InputKey.AudioVolumeMute,
        Key.VolumeDown => InputKey.AudioVolumeDown,
        Key.VolumeUp => InputKey.AudioVolumeUp,
        Key.SelectMedia => InputKey.LaunchMediaPlayer,
        Key.LaunchApplication1 => InputKey.LaunchApplication1,
        Key.LaunchApplication2 => InputKey.LaunchApplication2,
        Key.OemPlus => InputKey.Equal,
        Key.OemMinus => InputKey.Minus,
        Key.OemTilde => InputKey.Backquote,
        Key.OemComma => InputKey.Comma,
        Key.OemPeriod => InputKey.Period,
        Key.OemQuestion => InputKey.Slash,
        Key.OemOpenBrackets => InputKey.BracketLeft,
        Key.OemCloseBrackets => InputKey.BracketRight,
        Key.OemPipe => InputKey.Backslash,
        Key.OemSemicolon => InputKey.Semicolon,
        Key.OemQuotes => InputKey.Quote,
        Key.OemBackslash => InputKey.IntBackslash,
        _ => InputKey.Unknown,
    };
}
