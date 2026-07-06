using AppoMobi.Specials;
using DrawnUi.Draw;
using System.Diagnostics;

namespace DrawnUi.Draw;

public class PendulumAnimator : SkiaValueAnimator
{

    protected virtual Pendulum CreatePendulum()
    {
        return new Pendulum();
    }

    public override void Start(double delayMs = 0)
    {
        if (delayMs > 0)
        {
            runDelayMs = delayMs;
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(delayMs), () =>
            {
                Start();
            });
            return;
        }

        if (pendulum == null)
        {
            pendulum = CreatePendulum();
        }

        pendulum.setInitialVelocity(InitialVelocity);
        pendulum.setInitialAngle(Radians(InitialAngle));
        pendulum.Gravity = this.Gravity;
        pendulum.AirResistance = this.AirResistance;
        pendulum.SetAmplitude(Amplitude);
        pendulum.Reset();

        instances++;

        base.Start();
    }


    /// <summary>
    /// Returns absolute value, instead of going -/+ along the axis. Basically if true simulates bouncing.
    /// </summary>
    public bool IsOneDirectional { get; set; }

    public double AirResistance { get; set; } = 0.35;

    /// <summary>
    /// the higher the faster will stop
    /// </summary>
    public double InitialVelocity { get; set; } = -0.250;

    public double InitialAngle { get; set; } = 90;

    public PendulumAnimator SetAmplitude(double value)
    {
        Amplitude = value;
        return this;
    }

    public double Amplitude { get; set; } = 100;

    public double Gravity { get; set; } = 3;

    private static long instances;

    public override bool TickFrame(long frameTimeNanos)
    {
        if (!IsRunning)
            return false; //sanity check

        if (LastFrameTimeNanos == 0)
        {
            //  First frame.
            LastFrameTimeNanos = frameTimeNanos;
            StartFrameTimeNanos = frameTimeNanos;
        }

        //ms
        var ms = (frameTimeNanos - LastFrameTimeNanos) / 1000000.0f;

        if (Speed == 0)
            Speed = 1;
        var timeRatio = 1000.0 / Speed;
        pendulum.Update(ms / timeRatio);

        double x;
        if (IsOneDirectional)
        {
            x = Math.Abs(pendulum.getWireVector().getXComp());
        }
        else
        {
            x = pendulum.getWireVector().getXComp();
        }

        if (x < mMinValue)
            x = mMinValue;
        else
        if (x > mMaxValue)
            x = mMaxValue;
        _action?.Invoke(x);

        LastFrameTimeNanos = frameTimeNanos;

        var finished = Math.Abs(pendulum.AngularVelocity) < 0.01 && x < 0.01;

        if (finished)
        {
            Stop();
        }

        return finished;
    }

    public override void Stop()
    {
        base.Stop();

        instances--;
    }

    private Pendulum pendulum;
    private readonly Action<double> _action;

    public PendulumAnimator(SkiaControl parent, Action<double> valueUpdated) : base(parent)
    {
        _action = valueUpdated;
    }
}
