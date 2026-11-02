#if WINDOWS || DRAWNUI_NET
using System.Runtime.InteropServices;

namespace DrawnUi.Draw;

/// <summary>What the mouse did over a popup window shown by <see cref="Win32PopupHost"/>.</summary>
internal enum PopupPointerKind
{
    Move,
    Down,
    Up,
    Wheel,
}

/// <summary>
/// Shows the canvas' popup windows (menus, dropdowns) in Win32 layered windows of their own, owned by the app window:
/// they can go past its edge, never take the focus from it, and take the mouse, which goes back into the canvas at
/// the matching canvas point. Each window is filled from a picture of the popup, redrawn only when the popup changes.
/// Used by MAUI Windows, WPF and OpenTK on Windows.
/// </summary>
internal sealed class Win32PopupHost : IPopupWindowHost, IDisposable
{
    private readonly Func<IntPtr> _owner;
    private readonly Func<(double X, double Y)> _canvasScreenOrigin;
    private readonly Action<Action> _post;
    private readonly Action<PopupPointerKind, SKPoint, float> _input;
    private readonly Dictionary<int, (IntPtr Hwnd, SKRect Rect)> _windows = new();
    private bool _pressed;

    /// <param name="owner">The app window: the popups stay above it.</param>
    /// <param name="canvasScreenOrigin">Top-left corner of the canvas on screen, in physical pixels.</param>
    /// <param name="post">Runs an action on the thread that owns the app window.</param>
    /// <param name="input">Feeds mouse input into the canvas: kind, canvas pixels, wheel delta (120 a notch).</param>
    public Win32PopupHost(Func<IntPtr> owner, Func<(double X, double Y)> canvasScreenOrigin, Action<Action> post,
        Action<PopupPointerKind, SKPoint, float> input)
    {
        _owner = owner;
        _canvasScreenOrigin = canvasScreenOrigin;
        _post = post;
        _input = input;
    }

    public SKRect ScreenBounds()
    {
        var owner = _owner();
        if (owner == IntPtr.Zero)
            return SKRect.Empty;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfoW(MonitorFromWindow(owner, MONITOR_DEFAULTTONEAREST), ref info))
            return SKRect.Empty;

        var origin = _canvasScreenOrigin();
        return new SKRect(
            (float)(info.rcWork.Left - origin.X), (float)(info.rcWork.Top - origin.Y),
            (float)(info.rcWork.Right - origin.X), (float)(info.rcWork.Bottom - origin.Y));
    }

    public void ShowPopup(PopupId id, SKImage image, SKRect canvasRect)
    {
        var pixels = Win32Layered.ReadPixels(image, out var width, out var height);
        _post(() =>
        {
            try
            {
                if (!_windows.TryGetValue(id.Value, out var entry) || entry.Hwnd == IntPtr.Zero)
                {
                    var hwnd = Win32Layered.Create(ClassName, Win32Layered.WS_EX_LAYERED | Win32Layered.WS_EX_TOOLWINDOW
                        | Win32Layered.WS_EX_NOACTIVATE | Win32Layered.WS_EX_TOPMOST, _owner(), WndProcPointer);
                    if (hwnd == IntPtr.Zero)
                        return;
                    entry = (hwnd, canvasRect);
                    Hosts[hwnd] = this;
                }

                _windows[id.Value] = (entry.Hwnd, canvasRect);
                var origin = _canvasScreenOrigin();
                Win32Layered.Fill(entry.Hwnd, pixels, width, height,
                    (int)Math.Round(origin.X + canvasRect.Left), (int)Math.Round(origin.Y + canvasRect.Top));
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        });
    }

    public void HidePopup(PopupId id)
    {
        _post(() =>
        {
            if (!_windows.Remove(id.Value, out var entry))
                return;
            Hosts.Remove(entry.Hwnd);
            Win32Layered.DestroyWindow(entry.Hwnd);
        });
    }

    public void Dispose()
    {
        foreach (var entry in _windows.Values.ToList())
        {
            var hwnd = entry.Hwnd;
            _post(() =>
            {
                Hosts.Remove(hwnd);
                Win32Layered.DestroyWindow(hwnd);
            });
        }
        _windows.Clear();
    }

    private void OnMouse(IntPtr hwnd, PopupPointerKind kind, SKPoint local, float wheel)
    {
        foreach (var entry in _windows.Values)
        {
            if (entry.Hwnd != hwnd)
                continue;

            if (kind == PopupPointerKind.Down)
            {
                _pressed = true;
                Win32Layered.SetCapture(hwnd);
            }
            else if (kind == PopupPointerKind.Up && _pressed)
            {
                _pressed = false;
                Win32Layered.ReleaseCapture();
            }

            _input(kind, new SKPoint(entry.Rect.Left + local.X, entry.Rect.Top + local.Y), wheel);
            return;
        }
    }

    // one window class for every host in the process; messages find their host by window
    private const string ClassName = "DrawnUiPopupWindow";
    private static readonly Dictionary<IntPtr, Win32PopupHost> Hosts = new();
    private static readonly Win32Layered.WndProc WndProcDelegate = WndProc;
    private static readonly IntPtr WndProcPointer = Marshal.GetFunctionPointerForDelegate(WndProcDelegate);

    private static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const uint WM_MOUSEACTIVATE = 0x0021, WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202,
            WM_MOUSEWHEEL = 0x020A;
        const int MA_NOACTIVATE = 3;

        if (msg == WM_MOUSEACTIVATE)
            return new IntPtr(MA_NOACTIVATE); // a click on a menu never takes the focus from the app window

        if (Hosts.TryGetValue(hwnd, out var host))
        {
            var local = new SKPoint((short)((long)lParam & 0xFFFF), (short)(((long)lParam >> 16) & 0xFFFF));
            switch (msg)
            {
                case WM_MOUSEMOVE:
                    host.OnMouse(hwnd, PopupPointerKind.Move, local, 0);
                    return IntPtr.Zero;
                case WM_LBUTTONDOWN:
                    host.OnMouse(hwnd, PopupPointerKind.Down, local, 0);
                    return IntPtr.Zero;
                case WM_LBUTTONUP:
                    host.OnMouse(hwnd, PopupPointerKind.Up, local, 0);
                    return IntPtr.Zero;
                case WM_MOUSEWHEEL:
                    // screen coordinates here
                    var screen = new Win32Layered.POINT { X = (short)((long)lParam & 0xFFFF), Y = (short)(((long)lParam >> 16) & 0xFFFF) };
                    Win32Layered.ScreenToClient(hwnd, ref screen);
                    host.OnMouse(hwnd, PopupPointerKind.Wheel, new SKPoint(screen.X, screen.Y), (short)(((long)wParam >> 16) & 0xFFFF));
                    return IntPtr.Zero;
            }
        }

        return Win32Layered.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFO info);
}

/// <summary>Win32 layered windows filled from a premultiplied picture, shared by the tooltip and popup hosts.</summary>
internal static class Win32Layered
{
    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    private const int ULW_ALPHA = 2;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly HashSet<string> Registered = new();

    public delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static readonly WndProc DefaultProc = DefWindowProcW;
    private static readonly IntPtr DefaultProcPointer = Marshal.GetFunctionPointerForDelegate(DefaultProc);

    /// <summary>The picture's pixels as premultiplied BGRA rows; disposes the image.</summary>
    public static byte[] ReadPixels(SKImage image, out int width, out int height)
    {
        width = image.Width;
        height = image.Height;
        var pixels = new byte[width * height * 4];
        using (image)
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                image.ReadPixels(info, handle.AddrOfPinnedObject(), width * 4, 0, 0);
            }
            finally
            {
                handle.Free();
            }
        }

        return pixels;
    }

    /// <summary>A popup window of the class, registered once with the window procedure (null: no input handling).</summary>
    public static IntPtr Create(string className, int exStyle, IntPtr owner, IntPtr wndProc = default)
    {
        if (Registered.Add(className))
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = wndProc != IntPtr.Zero ? wndProc : DefaultProcPointer,
                hInstance = GetModuleHandleW(null),
                hCursor = LoadCursorW(IntPtr.Zero, new IntPtr(32512)), // IDC_ARROW
                lpszClassName = className,
            };
            RegisterClassExW(ref wc); // fails harmlessly when it was registered already in this process
        }

        return CreateWindowExW(exStyle, className, string.Empty, WS_POPUP, 0, 0, 1, 1, owner, IntPtr.Zero,
            GetModuleHandleW(null), IntPtr.Zero);
    }

    /// <summary>Puts the picture in the window at the screen position and shows it, without activating it.</summary>
    public static void Fill(IntPtr hwnd, byte[] pixels, int width, int height, int x, int y)
    {
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // top-down rows, as Skia writes them
            biPlanes = 1,
            biBitCount = 32,
        };
        var dib = CreateDIBSection(screen, ref header, 0, out var bits, IntPtr.Zero, 0);
        try
        {
            if (dib == IntPtr.Zero)
                return;

            Marshal.Copy(pixels, 0, bits, pixels.Length);
            var previous = SelectObject(memory, dib);
            var position = new POINT { X = x, Y = y };
            var size = new SIZE { cx = width, cy = height };
            var source = new POINT();
            var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 }; // AC_SRC_ALPHA
            UpdateLayeredWindow(hwnd, screen, ref position, ref size, memory, ref source, 0, ref blend, ULW_ALPHA);
            SelectObject(memory, previous);
            SetWindowPos(hwnd, HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
        finally
        {
            if (dib != IntPtr.Zero)
                DeleteObject(dib);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")] public static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] public static extern IntPtr SetCapture(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hwnd, ref POINT point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(int exStyle, string className, string windowName, int style, int x, int y,
        int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dst, ref POINT position, ref SIZE size, IntPtr src,
        ref POINT source, int key, ref BLENDFUNCTION blend, int flags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER header, uint usage, out IntPtr bits,
        IntPtr section, uint offset);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
}
#endif
