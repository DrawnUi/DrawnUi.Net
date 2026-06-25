// Credits to https://github.com/super-ultra

using System.Numerics;

namespace DrawnUi.Draw;

public class SpringTimingVectorParameters : IDampingTimingVectorParameters
{
    public Spring Spring { get; }
    public Vector2 Displacement { get; }
    public Vector2 InitialVelocity { get; }
    public float Threshold { get; }
    private IDampingTimingVectorParameters Impl { get; }

    public SpringTimingVectorParameters(Spring spring, Vector2 displacement, Vector2 initialVelocity, float threshold)
    {
        Spring = spring;
        Displacement = displacement;
        InitialVelocity = initialVelocity;
        Threshold = threshold;

        if (spring.DampingRatio >= 1)
        {
            Impl = new CriticallyDampedSpringTimingVectorParameters(spring, displacement, initialVelocity, threshold);
        }
        else if (spring.DampingRatio > 0 && spring.DampingRatio < 1)
        {
            Impl = new UnderdampedSpringTimingVectorParameters(spring, displacement, initialVelocity, threshold);
        }
        else
        {
            throw new ArgumentException("dampingRatio should be greater than 0 and less than or equal to 1");
        }
    }

    public float DurationSecs => Impl.DurationSecs;

    public Vector2 ValueAt(float offsetSecs)
    {
        return Impl.ValueAt(offsetSecs);
    }

    public Vector2 AmplitudeAt(float offsetSecs)
    {
        return Impl.AmplitudeAt(offsetSecs);
    }
}


