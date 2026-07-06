// Credits to https://github.com/super-ultra

using System.Numerics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public class ScrollFlingVectorAnimator : SkiaVectorAnimator
{
    public bool SelfFinished { get; set; }

    public Task RunAsync(Vector2 position, Vector2 velocity, float deceleration = 0.998f, float threshold = 0.5f, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Initialize(position, velocity, deceleration, threshold), cancellationToken);
    }

    public void Initialize(Vector2 position, Vector2 velocity, float deceleration = 0.998f, float threshold = 0.5f)
    {
        Parameters = new(position, velocity, deceleration, threshold);
        Speed = Parameters.DurationSecs;
    }

    public DecelerationTimingVectorParameters Parameters { get; set; }

    public Vector2 CurrentVelocity { get; protected set; }

    public override void Start(double delayMs = 0)
    {
        SelfFinished = false;

        base.Start(delayMs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override bool UpdateValue(long deltaT, long deltaFromStart)
    {
        var secs = deltaFromStart / 1_000_000_000.0f;

        if (secs > Speed)
        {
            SelfFinished = true;
        }

        Vector = Parameters.ValueAt(secs);
        CurrentVelocity = Parameters.VelocityAt(secs);

        return SelfFinished;
    }

    public ScrollFlingVectorAnimator(IDrawnBase parent) : base(parent)
    {
        Initialize(Vector2.Zero, Vector2.Zero);
    }

}