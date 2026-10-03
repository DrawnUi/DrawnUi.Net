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
        try
        {
            KeyDown?.Invoke(null, key);
        }
        finally
        {
            IsKeyForOtherElement = false;
        }
    }

    public static void KeyboardReleased(InputKey key) => KeyboardReleased(key, false);

    /// <param name="key">The key.</param>
    /// <param name="forOtherElement">The key goes to a focused element outside DrawnUI, see <see cref="IsKeyForOtherElement"/>.</param>
    public static void KeyboardReleased(InputKey key, bool forOtherElement)
    {
        CheckAndApplyModifiers(key, false);

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