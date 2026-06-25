// Credits to https://github.com/super-ultra

using System.Numerics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public class DecelerationTimingVectorParameters : ITimingVectorParameters
{

    public Vector2 InitialValue { get; set; }
    public Vector2 InitialVelocity { get; set; }
    public float DecelerationRate { get; protected set; }
    public float DecelerationK { get; protected set; }
    public float Threshold { get; set; }

    public DecelerationTimingVectorParameters(Vector2 initialValue, Vector2 initialVelocity, float decelerationRate, float threshold)
    {

        if (decelerationRate <= 0 || decelerationRate >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(decelerationRate), "Deceleration rate must be greater than 0 and less than 1.");
        }

        InitialValue = initialValue;
        InitialVelocity = initialVelocity;
        Threshold = threshold;
        DecelerationRate = decelerationRate;
        DecelerationK = 1000 * (float)Math.Log(DecelerationRate);
    }

    public Vector2 Destination
    {
        get
        {
            return InitialValue - InitialVelocity / DecelerationK;
        }
    }

    public float DurationSecs
    {
        get
        {
            if (InitialVelocity.Length() == 0)
                return 0;

            return (float)Math.Log(-DecelerationK * Threshold / InitialVelocity.Length()) / DecelerationK;
        }
    }

    /// <summary>
    /// time is in seconds
    /// </summary>
    /// <param name="offsetSecs"></param>
    /// <param name="time"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ValueAt(float offsetSecs)
    {
        float time = offsetSecs;

        float factor = (float)((Math.Pow(DecelerationRate, (float)(1000 * time)) - 1) / DecelerationK);
        return InitialValue + InitialVelocity * factor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 VelocityAt(double time)
    {
        return InitialVelocity * (float)Math.Pow(DecelerationRate, (float)(1000 * time));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double DurationToValue(Vector2 value)
    {
        //if (DistanceToSegment(value, InitialValue, Destination) >= Threshold)
        //    return null;

        return Math.Log(1 + DecelerationK * (value - InitialValue).Length() / InitialVelocity.Length()) / DecelerationK;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 VelocityTo(Vector2 startingPoint, Vector2 targetPoint, double time)
    {
        float factor = (float)(Math.Pow(DecelerationRate, 1000 * time) - 1);

        return (targetPoint - startingPoint) * DecelerationK / factor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 VelocityToZero(Vector2 startingPoint, Vector2 targetPoint, float maxTimeSecs = 0, float epsilon = 1e-6f)
    {
        Vector2 distance = targetPoint - startingPoint;
        float distanceMagnitude = distance.Length();

        // Calculate the time at which the velocity will be epsilon
        float optimalTime = (float)(Math.Log(epsilon) - Math.Log(distanceMagnitude)) / DecelerationK;

        if (maxTimeSecs > 0 && optimalTime > maxTimeSecs)
            optimalTime = maxTimeSecs;

        // Calculate the initial velocity needed to reach the target point in optimalTime
        Vector2 initialVelocity = VelocityTo(startingPoint, targetPoint, optimalTime);

        return initialVelocity;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        Vector2 v = point - segmentStart;
        float t = Vector2.Dot(v, segment) / Vector2.Dot(segment, segment);

        if (t <= 0)
            return Vector2.Distance(point, segmentStart);
        if (t >= 1)
            return Vector2.Distance(point, segmentEnd);

        Vector2 projection = segmentStart + t * segment;
        return Vector2.Distance(point, projection);
    }
}