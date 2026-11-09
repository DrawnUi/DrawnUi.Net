using System.Runtime.CompilerServices;

namespace DrawnUi.Net.Tests;

/// <summary>
/// xUnit runs the test bodies on thread pool threads, as many at once as there are processors, and every body blocks
/// its thread (frames, Thread.Sleep). In an app the UI thread is not a pool thread, so the pool stays free for the
/// library's background work: timers (scroll bar hide), background measurement, image loads. Here the bodies took
/// every worker and that work waited seconds in the queue behind them. Reserving as many extra workers as xUnit runs
/// bodies gives the library the pool an app gives it.
/// </summary>
static class TestHostThreadPool
{
    [ModuleInitializer]
    internal static void ReserveWorkersForTestBodies()
    {
        ThreadPool.GetMinThreads(out var workers, out var io);
        ThreadPool.SetMinThreads(workers + Environment.ProcessorCount, io);
    }
}
