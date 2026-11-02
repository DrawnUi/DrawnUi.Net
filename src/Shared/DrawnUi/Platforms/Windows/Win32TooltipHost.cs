#if WINDOWS || DRAWNUI_NET
namespace DrawnUi.Draw;

/// <summary>
/// Shows canvas tooltips in a Win32 layered popup window: it can go past the edge of the app window, has per-pixel
/// alpha, never takes focus or clicks, and is filled once per tooltip (nothing per frame). Used by the MAUI Windows and
/// OpenTK heads on Windows.
/// </summary>
internal sealed class Win32TooltipHost : ITooltipHost, IDisposable
{
    private const string ClassName = "DrawnUiTooltipWindow";

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
        var pixels = Win32Layered.ReadPixels(image, out var width, out var height);
        var version = Interlocked.Increment(ref _version);
        _post(() =>
        {
            if (version != Volatile.Read(ref _version))
                return;

            try
            {
                if (_hwnd == IntPtr.Zero)
                {
                    _hwnd = Win32Layered.Create(ClassName, Win32Layered.WS_EX_LAYERED | Win32Layered.WS_EX_TOOLWINDOW
                        | Win32Layered.WS_EX_NOACTIVATE | Win32Layered.WS_EX_TOPMOST | Win32Layered.WS_EX_TRANSPARENT, _owner());
                    if (_hwnd == IntPtr.Zero)
                        return;
                }

                var origin = _canvasScreenOrigin();
                Win32Layered.Fill(_hwnd, pixels, width, height,
                    (int)Math.Round(origin.X + position.X), (int)Math.Round(origin.Y + position.Y));
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
                Win32Layered.ShowWindow(_hwnd, 0); // SW_HIDE
        });
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _version);
        var hwnd = _hwnd;
        _hwnd = IntPtr.Zero;
        if (hwnd != IntPtr.Zero)
            _post(() => Win32Layered.DestroyWindow(hwnd));
    }
}
#endif
