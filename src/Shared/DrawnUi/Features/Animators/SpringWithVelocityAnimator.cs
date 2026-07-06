namespace DrawnUi.Draw;

public class SpringWithVelocityAnimator : SkiaValueAnimator
{

    float Origin { get; set; }

    public void Initialize(float restOffset, float position, float velocity, Spring spring, float thresholdStop = 0.5f)
    {
        Origin = restOffset;

        Parameters = new SpringTimingParameters(spring, position, velocity, thresholdStop);
    }

    public SpringTimingParameters Parameters { get; set; }

    protected override bool UpdateValue(long deltaT, long deltaFromStart)
    {
        var secs = deltaFromStart / 1_000_000_000.0f;

        if (secs > Parameters.DurationSecs)
        {
            mValue = Origin;
            return true;
        }

        mValue = Origin + Parameters.ValueAt(secs);
        return false;
    }

    public SpringWithVelocityAnimator(IDrawnBase parent) : base(parent)
    {
    }
}