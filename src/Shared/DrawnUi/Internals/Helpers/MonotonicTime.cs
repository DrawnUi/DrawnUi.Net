using System.Diagnostics;

namespace DrawnUi.Draw;

/// <summary>
/// Milliseconds on a clock that only goes forward, for deadlines, timers and "has enough time passed" checks.
/// Environment.TickCount64 is not that clock on Windows: it follows the unbiased interrupt time, which a PC with Modern
/// Standby winds back by hours after it sleeps, so a deadline set before the sleep would not expire for hours.
/// The performance counter never goes back.
/// </summary>
internal static class MonotonicTime
{
    public static long Ms => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
}
