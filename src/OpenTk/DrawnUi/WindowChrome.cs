using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DrawnUi.OpenTk;

/// <summary>
/// DWM title bar / border styling and system menu helpers (Windows).
/// Caption and border color require Win11 build 22000+; dark mode works on Win10 20H1+.
/// Note: when a custom caption color is set, Windows auto-picks white or black text
/// based on luminance — calling SetDarkMode is not needed and overrides user preference.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowChrome
{
    // ── DWM ──────────────────────────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_DONOTROUND = 1;

    // COLORREF = 0x00BBGGRR
    public static int ToColorRef(byte r, byte g, byte b) => r | (g << 8) | (b << 16);

    public static void SetDarkMode(IntPtr hwnd, bool dark = true)
    {
        int val = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref val, sizeof(int));
    }

    public static void SetCaptionColor(IntPtr hwnd, byte r, byte g, byte b)
    {
        int color = ToColorRef(r, g, b);
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref color, sizeof(int));
    }

    public static void SetBorderColor(IntPtr hwnd, byte r, byte g, byte b)
    {
        int color = ToColorRef(r, g, b);
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref color, sizeof(int));
    }

    public static void SetRoundedCorners(IntPtr hwnd, bool rounded = true)
    {
        int val = rounded ? DWMWCP_ROUND : DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref val, sizeof(int));
    }

    // ── System menu + WndProc subclassing ────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern nint GetSystemMenu(nint hwnd, bool bRevert);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(nint hMenu, uint uFlags, nint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    internal static extern nint SetWindowLongPtr(nint hwnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    internal static extern nint CallWindowProc(nint lpPrevWndFunc, nint hwnd, uint msg, nint wParam, nint lParam);

    private const uint MF_STRING    = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const int  GWLP_WNDPROC = -4;

    internal const uint WM_SYSCOMMAND         = 0x0112;
    internal const nint ID_TOGGLE_FULLSCREEN  = 0x0100;

    public static void AddFullscreenMenuItem(nint hwnd, string label = "Fullscreen\tF11")
    {
        var hMenu = GetSystemMenu(hwnd, false);
        AppendMenu(hMenu, MF_SEPARATOR, 0, null);
        AppendMenu(hMenu, MF_STRING, ID_TOGGLE_FULLSCREEN, label);
    }
}
