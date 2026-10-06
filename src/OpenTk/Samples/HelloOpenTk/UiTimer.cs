using DrawnUi.Draw;

namespace HelloOpenTk;

/// <summary>
/// A repeating timer that ticks on the window thread, the OpenTK head's counterpart of WPF's DispatcherTimer and
/// MAUI's IDispatcherTimer in the other Hello apps' pages.
/// </summary>
public sealed class UiTimer
{
    private Timer _timer;

    /// <summary>Time between ticks.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Raised on the window thread.</summary>
    public event EventHandler Tick;

    /// <summary>Starts ticking.</summary>
    public void Start()
    {
        _timer?.Dispose();
        _timer = new Timer(_ => DrawnUi.MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_timer != null)
                Tick?.Invoke(this, EventArgs.Empty);
        }), null, Interval, Interval);
    }

    /// <summary>Stops ticking.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
