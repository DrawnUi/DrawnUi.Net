using System.Numerics;

namespace DrawnUi.Draw;

public struct ScrollToIndexOrder
{
    public static ScrollToIndexOrder Default => new()
    {
        Index = -1
    };
    public bool IsSet
    {
        get
        {
            return Index >= 0;
        }
    }
    public bool Animated { get; set; }
    public float MaxTimeSecs { get; set; }
    public RelativePositionType RelativePosition { get; set; }
    public int Index { get; set; }
    public bool Clamp { get; set; }
}

public class VelocityAccumulator
{
    private List<(Vector2 velocity, DateTime time)> velocities = new List<(Vector2 velocity, DateTime time)>();
    private const double Threshold = 10.0; // Minimum significant movement
    private const int MaxSampleSize = 5; // Number of samples for weighted average
    private const int ConsiderationTimeframeMs = 150; // Timeframe in ms for velocity consideration

    public void Clear()
    {
        velocities.Clear();
    }

    /// <param name="velocity">Sampled velocity.</param>
    /// <param name="arrivedTimeNanos">
    /// When the gesture that produced this sample ARRIVED from the platform
    /// (<see cref="SkiaGesturesParameters.ArrivedTimeNanos"/>). Pass it whenever available: gestures are
    /// processed one frame later, so during a hitch a whole burst is drained in a single frame and
    /// processing time makes seconds-old samples look fresh — the stale flick then starts a full-speed
    /// fling after the frame unfreezes. With the real arrival time the age window below drops them and
    /// the final velocity comes out zero, which is what "the user's flick is long over" should mean.
    /// Zero/omitted falls back to now (synthetic gestures, non-postponed callers).
    /// </param>
    public void CaptureVelocity(Vector2 velocity, long arrivedTimeNanos = 0)
    {
        var time = arrivedTimeNanos > 0
            ? DateTime.UtcNow.AddTicks(-(Super.GetCurrentTimeNanos() - arrivedTimeNanos) / 100)
            : DateTime.UtcNow;
        if (velocities.Count == MaxSampleSize) velocities.RemoveAt(0);
        velocities.Add((velocity, time));
    }

    public Vector2 CalculateFinalVelocity(float clampAbsolute = 0)
    {
        var now = DateTime.UtcNow;
        var relevantVelocities = velocities.Where(v => (now - v.time).TotalMilliseconds <= ConsiderationTimeframeMs).ToList();
        if (!relevantVelocities.Any()) return Vector2.Zero;

        // Calculate weighted average for both X and Y components
        float weightedSumX = relevantVelocities.Select((v, i) => v.velocity.X * (i + 1)).Sum();
        float weightedSumY = relevantVelocities.Select((v, i) => v.velocity.Y * (i + 1)).Sum();
        var weightSum = Enumerable.Range(1, relevantVelocities.Count).Sum();

        if (clampAbsolute != 0)
        {
            return new Vector2(Math.Clamp(weightedSumX / weightSum, -clampAbsolute, clampAbsolute),
                Math.Clamp(weightedSumY / weightSum, -clampAbsolute, clampAbsolute));
        }

        return new Vector2(weightedSumX / weightSum, weightedSumY / weightSum);
    }
}

public struct ScrollToPointOrder
{
    public bool IsValid
    {
        get
        {
            return !float.IsNaN(Location.X) && !float.IsNaN(Location.Y); ;
        }
    }

    public static ScrollToPointOrder NotValid => new()
    {
        Location = new SKPoint(float.NaN, float.NaN)
    };


    public static ScrollToPointOrder ToPoint(SKPoint point, bool animated)
    {
        return new()
        {
            Location = point,
            Animated = animated
        };
    }

    public static ScrollToPointOrder ToCoords(float x, float y, bool animated)
    {
        return new()
        {
            Location = new SKPoint(x, y),
            Animated = animated
        };
    }

    public static ScrollToPointOrder ToCoords(float x, float y, float maxTimeSecs)
    {
        return new()
        {
            Location = new SKPoint(x, y),
            MaxTimeSecs = maxTimeSecs
        };
    }

    public bool Animated { get; set; }
    public SKPoint Location { get; set; }
    public float MaxTimeSecs { get; set; }

}
