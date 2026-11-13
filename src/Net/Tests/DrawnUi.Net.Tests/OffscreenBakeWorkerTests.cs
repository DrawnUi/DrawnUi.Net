using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// Offscreen bakes run on dedicated threads so they never wait for the thread pool. A control enqueued again while
/// one of those threads baked it made the other threads wait for it asynchronously, and that wait ended on a pool
/// thread: with a busy pool every bake thread hung there and no control was baked until the pool got to it.
/// </summary>
[Collection("Multithreaded")] // saturates the thread pool
public class OffscreenBakeWorkerTests
{
    [Fact]
    public void ControlEnqueuedAgainWhileBaking_PoolBusy_OtherBakesStillRun()
    {
        var busy = new SkiaControl();
        var other = new SkiaControl();
        // not disposed: work items still queued after the test wait on them
        var bakeGate = new ManualResetEventSlim();
        var poolGate = new ManualResetEventSlim();
        var otherBaked = new ManualResetEventSlim();
        var baking = false;
        try
        {
            // one bake thread bakes "busy" until the gate opens, the other bake threads take "busy" again and wait
            busy.PushToOffscreenRendering(() =>
            {
                Volatile.Write(ref baking, true);
                bakeGate.Wait(5000);
            });
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref baking), 3000)); // a queue keeps one action: wait before the next
            for (var i = 0; i < 4; i++)
                busy.PushToOffscreenRendering(() => { });
            Thread.Sleep(200);

            // every pool thread blocked, more work queued behind them
            for (var i = 0; i < 300; i++)
                ThreadPool.QueueUserWorkItem(_ => poolGate.Wait(10000), (object)null, preferLocal: false);

            other.PushToOffscreenRendering(() => otherBaked.Set());
            bakeGate.Set();

            Assert.True(otherBaked.Wait(1000), "no bake ran while the thread pool was busy");
        }
        finally
        {
            bakeGate.Set();
            poolGate.Set();
        }
    }
}
