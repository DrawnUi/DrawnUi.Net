using DrawnUi.Draw;
using System.Numerics;

namespace DrawnUi.Draw;

public struct LinearInterpolationTimingParameters : ITimingVectorParameters
{
    private readonly Vector2 start;
    private readonly Vector2 end;
    private readonly float duration;
    private readonly Easing easing;

    public LinearInterpolationTimingParameters(Vector2 start, Vector2 end, float duration, Easing easing)
    {
        this.easing = easing;
        this.start = start;
        this.end = end;
        this.duration = duration;
    }

    public float DurationSecs => duration;

    public Vector2 ValueAt(float offsetSecs)
    {
        float t = offsetSecs / duration;  // normalize time to [0, 1]
        t = (float)easing.Ease(t);  // apply the easing function
        return Vector2.Lerp(start, end, t);  // interpolate between start and end
    }

}


public class RangeVectorAnimator : SkiaVectorAnimator
{

    public void Initialize(Vector2 start, Vector2 end, float durationSecs, Easing easing)
    {
        _end = end;
        Parameters = new LinearInterpolationTimingParameters(start, end, durationSecs, easing);
    }

    private Vector2 _end;

    public LinearInterpolationTimingParameters Parameters { get; set; }

    protected override bool UpdateValue(long deltaT, long deltaFromStart)
    {
        var secs = deltaFromStart / 1_000_000_000.0f;

        if (secs >= Parameters.DurationSecs)
        {
            Vector = _end; //Parameters.ValueAt(Parameters.DurationSecs);
            return true;
        }

        Vector = Parameters.ValueAt(secs);
        return false;
    }

    public RangeVectorAnimator(IDrawnBase parent) : base(parent)
    {

    }
}