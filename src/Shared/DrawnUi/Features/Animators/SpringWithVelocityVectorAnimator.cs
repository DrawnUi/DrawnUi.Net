using System.Numerics;

namespace DrawnUi.Draw;

public class SpringWithVelocityVectorAnimator : SkiaVectorAnimator
{

    Vector2 Origin { get; set; }

    // in: see SkiaControl.ContractPixelsRect, a Spring here starts past the FP argument registers on arm64
    public void Initialize(Vector2 restOffset, Vector2 position, Vector2 velocity, in Spring spring, float thresholdStop = 0.5f)
    {
        Origin = restOffset;

        Parameters = new SpringTimingVectorParameters(spring, position, velocity, thresholdStop);
    }

    public SpringTimingVectorParameters Parameters { get; set; }

    protected override bool UpdateValue(long deltaT, long deltaFromStart)
    {
        var secs = deltaFromStart / 1_000_000_000.0f;

        if (secs > Parameters.DurationSecs)
        {
            Vector = Origin;
            return true;
        }

        Vector = Origin + Parameters.ValueAt(secs);
        return false;
    }

    public SpringWithVelocityVectorAnimator(IDrawnBase parent) : base(parent)
    {
    }
}