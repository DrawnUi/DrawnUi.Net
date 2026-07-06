// Credits to https://github.com/super-ultra

using System.Numerics;

namespace DrawnUi.Draw;

public struct CriticallyDampedSpringTimingVectorParameters : IDampingTimingVectorParameters
{
    private readonly Spring spring;
    private readonly Vector2 displacement;
    private readonly Vector2 initialVelocity;
    private readonly float threshold;

    public CriticallyDampedSpringTimingVectorParameters(Spring spring, Vector2 displacement, Vector2 initialVelocity, float threshold)
    {
        this.spring = spring;
        this.displacement = displacement;
        this.initialVelocity = initialVelocity;
        this.threshold = threshold;
    }

    public float DurationSecs
    {
        get
        {
            if (displacement.Length() == 0 && initialVelocity.Length() == 0)
            {
                return 0;
            }

            float b = spring.Beta();
            float e = MathF.Exp(1);

            float t1 = 1 / b * MathF.Log(2 * c1.Length() / threshold);
            float t2 = 2 / b * MathF.Log(4 * c2.Length() / (e * b * threshold));

            return MathF.Max(t1, t2);
        }
    }

    public Vector2 ValueAt(float offsetSecs)
    {
        float t = offsetSecs;
        return MathF.Exp(-spring.Beta() * t) * (c1 + c2 * t);
    }

    public Vector2 AmplitudeAt(float offsetSecs)
    {
        var value = ValueAt(offsetSecs);
        return new(Math.Abs(value.X), Math.Abs(value.Y));
    }

    private Vector2 c1 => displacement;
    private Vector2 c2 => initialVelocity + spring.Beta() * displacement;
}