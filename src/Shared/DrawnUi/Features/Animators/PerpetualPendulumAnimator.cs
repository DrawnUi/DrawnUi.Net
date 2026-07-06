namespace DrawnUi.Draw;

public class PerpetualPendulumAnimator : PendulumAnimator
{
    public PerpetualPendulumAnimator(SkiaControl parent, Action<double> valueUpdated) : base(parent, valueUpdated)
    {
    }

    protected override Pendulum CreatePendulum()
    {
        return new PerpetualPendulum();
    }
}