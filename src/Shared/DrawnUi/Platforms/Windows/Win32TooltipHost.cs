#if WINDOWS || DRAWNUI_NET
using System.Runtime.InteropServices;

namespace DrawnUi.Draw;

/// <summary>
/// Shows canvas tooltips in a Win32 layered popup window: it can go past the edge of the app window, has per-pixel
/// alpha, never takes focus or clicks, and is filled once per tooltip (nothing per frame). Used by the MAUI Windows and
/// OpenTK heads on Windows.
/// </summary>
internal sealed class Win32TooltipHost : ITooltipHost, IDisposable
{
    private readonly Func<IntPtr> _owner;
    private readonly Func<(double X, double Y)> _canvasScreenOrigin;
    private readonly Action<Action> _post;
    private IntPtr _hwnd;
    private int _version;

    /// <param name="owner">The app window: the popup stays above it and closes with it.</param>
    /// <param name="canvasScreenOrigin">Top-left corner of the canvas on screen, in physical pixels.</param>
    /// <param name="post">Runs an action on the thread that owns the app window.</param>
    public Win32TooltipHost(Func<IntPtr> owner, Func<(double X, double Y)> canvasScreenOrigin, Action<Action> post)
    {
        _owner = owner;
        _canvasScreenOrigin = canvasScreenOrigin;
        _post = post;
    }

    public void ShowTooltip(SKImage image, SKPoint position, float scale)
    {
        int width = image.Width, height = image.Height;
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

        var version = Interlocked.Increment(ref _version);
        _post(() =>
        {
            if (version != Volatile.Read(ref _version))
                return;

            try
            {
                var origin = _canvasScreenOrigin();
                Show(pixels, width, height, (int)Math.Round(origin.X + position.X), (int)Math.Round(origin.Y + position.Y));
            }
            catch (Exception e)
            {
                Super.Log(e);
            }
        });
    }

    public void HideTooltip()
    {
        var version = Interlocked.Increment(ref _version);
        _post(() =>
        {
            if (version == Volatile.Read(ref _version) && _hwnd != IntPtr.Zero)
                ShowWindow(_hwnd, SW_HIDE);
        });
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _version);
        var hwnd = _hwnd;
        _hwnd = IntPtr.Zero;
        if (hwnd != IntPtr.Zero)
            _post(() => DestroyWindow(hwnd));
    }

    private void Show(byte[] pixels, int width, int height, int x, int y)
    {
        if (_hwnd == IntPtr.Zero)
        {
            EnsureClass();
            _hwnd = CreateWindowExW(
                WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TRANSPARENT,
                ClassName, string.Empty, WS_POPUP, x, y, width, height, _owner(), IntPtr.Zero,
                GetModuleHandleW(null), IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
                return;
        }

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
            UpdateLayeredWindow(_hwnd, screen, ref position, ref size, memory, ref source, 0, ref blend, ULW_ALPHA);

            SelectObject(memory, previous);
            SetWindowPos(_hwnd, HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
        finally
        {
            if (dib != IntPtr.Zero)
                DeleteObject(dib);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private const string ClassName = "DrawnUiTooltipWindow";
    private static WndProc _wndProc; // kept alive for the window class
    private static bool _classRegistered;

    private static void EnsureClass()
    {
        if (_classRegistered)
            return;

        _wndProc = DefWindowProcW;
        var wc = new WNDCLASSEXW
        {
            cbSize = Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandleW(null),
            lpszClassName = ClassName,
        };
        RegisterClassExW(ref wc); // fails harmlessly when another head registered it in this process
        _classRegistered = true;
    }

    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int SW_HIDE = 0;
    private const int ULW_ALPHA = 2;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

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
    private struct POINT { public int X, Y; }

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

    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(int exStyle, string className, string windowName, int style, int x, int y,
        int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
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
