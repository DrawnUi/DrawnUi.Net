namespace DrawnUi.Draw;

/// <summary>
/// Just register this animator to run custom code on every frame creating a kind of game loop if needed.
/// </summary>
public class ActionOnTickAnimator : AnimatorBase
{
    private readonly Action<long> _action;

    public override bool TickFrame(long frameTimeNanos)
    {
        _action?.Invoke(frameTimeNanos);
        return base.TickFrame(frameTimeNanos);
    }

    public ActionOnTickAnimator(SkiaControl parent, Action<long> action) : base(parent)
    {
        _action = action;
    }
}