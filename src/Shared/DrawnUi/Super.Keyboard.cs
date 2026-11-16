namespace DrawnUi.Draw;

public partial class Super
{
    /// <summary>
    /// Top edge of the on-screen keyboard, in points from the top of the screen; NaN while no on-screen keyboard is
    /// shown. Tracked for the whole app (no page needed): iOS keyboard notifications, Android window IME insets, the browser's visual viewport on the web heads.
    /// </summary>
    public static double KeyboardTop { get; private set; } = double.NaN;

    /// <summary>
    /// Raised when the on-screen keyboard appears, moves or hides (<see cref="KeyboardTop"/> changed).
    /// </summary>
    public static event EventHandler KeyboardChanged;

    internal static void SetKeyboardTop(double top)
    {
        if (double.IsNaN(top) && double.IsNaN(KeyboardTop))
            return;
        if (Math.Abs(top - KeyboardTop) < 0.5)
            return;
        KeyboardTop = top;
        KeyboardChanged?.Invoke(null, EventArgs.Empty);
    }
}
