using System.Diagnostics;

namespace DrawnUi.Draw;


public partial class KeyboardManager
{

    public static event EventHandler<InputKey> KeyDown;

    public static event EventHandler<InputKey> KeyUp;

    public static event EventHandler<string> KeyChar;

    /// <summary>
    /// True while <see cref="KeyDown"/>, <see cref="KeyUp"/> or <see cref="KeyChar"/> is raised for a key that a focused
    /// element outside DrawnUI receives: a page input next to the canvas, a native text field. Every key is reported,
    /// but such a key is not for drawn controls: observe it, never act on it as typing or a shortcut. Drawn editors and
    /// text selection ignore it.
    /// </summary>
    public static bool IsKeyForOtherElement { get; private set; }

    /// <summary>
    /// True while <see cref="KeyDown"/> is raised for a key the canvas's keyboard navigation has already used: Tab moved
    /// the focus, Enter / Space activated the focused control, an arrow moved inside a list or a menu or stepped a
    /// slider, Escape let the drawn controls go. An app handler that would act on the same key (a menu that opens on
    /// Enter) skips it, so one press does one thing. The canvas handles the key first, then KeyDown is raised.
    /// </summary>
    public static bool IsKeyHandled { get; private set; }

    private static InputKey _keyUsedByCanvas = InputKey.Unknown;

    /// <summary>
    /// For heads: the canvas's keyboard navigation used <paramref name="key"/>, so the <see cref="KeyDown"/> raised next for
    /// that key reports <see cref="IsKeyHandled"/>. Call it before <see cref="KeyboardPressed(InputKey, bool)"/>; the key's
    /// release forgets it.
    /// </summary>
    public static void MarkKeyHandled(InputKey key) => _keyUsedByCanvas = key;

    public static void KeyboardChar(string ch) => KeyboardChar(ch, false);

    /// <param name="ch">The typed text.</param>
    /// <param name="forOtherElement">The key goes to a focused element outside DrawnUI, see <see cref="IsKeyForOtherElement"/>.</param>
    public static void KeyboardChar(string ch, bool forOtherElement)
    {
        IsKeyForOtherElement = forOtherElement;
        try
        {
            KeyChar?.Invoke(null, ch);
        }
        finally
        {
            IsKeyForOtherElement = false;
        }
    }

    public static void KeyboardPressed(InputKey key) => KeyboardPressed(key, false);

    /// <param name="key">The key.</param>
    /// <param name="forOtherElement">The key goes to a focused element outside DrawnUI, see <see cref="IsKeyForOtherElement"/>.</param>
    public static void KeyboardPressed(InputKey key, bool forOtherElement)
    {
        CheckAndApplyModifiers(key, true);

        IsKeyForOtherElement = forOtherElement;
        IsDispatchingKeyDown = true;
        try
        {
            CanvasKeyDown?.Invoke(key); // the canvas's own keys first (Escape closes a popup window)
            IsKeyHandled = key != InputKey.Unknown && _keyUsedByCanvas == key;
            _keyUsedByCanvas = InputKey.Unknown;
            KeyDown?.Invoke(null, key);
        }
        finally
        {
            IsKeyForOtherElement = false;
            IsKeyHandled = false;
            IsDispatchingKeyDown = false;
        }
    }

    /// <summary>
    /// True while a key press is handled, by the canvas and then by <see cref="KeyDown"/> handlers: what opens or closes
    /// now was done with the keyboard (a popup window opened by Alt+F takes the keyboard focus, one closed by Escape
    /// gives it back; a menu closed by Escape goes back one level).
    /// </summary>
    public static bool IsDispatchingKeyDown { get; private set; }

    /// <summary>
    /// The canvas's own key handling that comes before the app's <see cref="KeyDown"/> (Escape closing the top popup
    /// window); a handler that uses the key calls <see cref="MarkKeyHandled"/>.
    /// </summary>
    internal static event Action<InputKey> CanvasKeyDown;

    public static void KeyboardReleased(InputKey key) => KeyboardReleased(key, false);

    /// <param name="key">The key.</param>
    /// <param name="forOtherElement">The key goes to a focused element outside DrawnUI, see <see cref="IsKeyForOtherElement"/>.</param>
    public static void KeyboardReleased(InputKey key, bool forOtherElement)
    {
        CheckAndApplyModifiers(key, false);
        if (_keyUsedByCanvas == key)
            _keyUsedByCanvas = InputKey.Unknown;

        IsKeyForOtherElement = forOtherElement;
        try
        {
            KeyUp?.Invoke(null, key);
        }
        finally
        {
            IsKeyForOtherElement = false;
        }
    }

    public static bool IsShiftPressed
    {
        get
        {
            return IsLeftShiftDown || IsRightShiftDown;
        }
    }

    public static bool IsAltPressed
    {
        get
        {
            return IsLeftAltDown || IsRightAltDown;
        }
    }

    public static bool IsControlPressed
    {
        get
        {
            return IsLeftControlDown || IsRightControlDown;
        }
    }

    /// <summary>
    /// Command (Mac) / Windows key held.
    /// </summary>
    public static bool IsMetaPressed
    {
        get
        {
            return IsLeftMetaDown || IsRightMetaDown;
        }
    }

    static bool IsLeftMetaDown { get; set; }

    static bool IsRightMetaDown { get; set; }

    static bool IsLeftShiftDown { get; set; }

    static bool IsRightShiftDown { get; set; }

    static bool IsLeftAltDown { get; set; }

    static bool IsRightAltDown { get; set; }

    static bool IsLeftControlDown { get; set; }

    static bool IsRightControlDown { get; set; }

    static void CheckAndApplyModifiers(InputKey key, bool state)
    {
        if (key == InputKey.ShiftLeft)
        {
            IsLeftShiftDown = state;
        }
        else
        if (key == InputKey.ShiftRight)
        {
            IsRightShiftDown = state;
        }
        else
        if (key == InputKey.AltLeft)
        {
            IsLeftAltDown = state;
        }
        else
        if (key == InputKey.AltRight)
        {
            IsRightAltDown = state;
        }
        else
        if (key == InputKey.ControlLeft)
        {
            IsLeftControlDown = state;
        }
        else
        if (key == InputKey.ControlRight)
        {
            IsRightControlDown = state;
        }
        else
        if (key == InputKey.MetaLeft)
        {
            IsLeftMetaDown = state;
        }
        else
        if (key == InputKey.MetaRight)
        {
            IsRightMetaDown = state;
        }
    }


}