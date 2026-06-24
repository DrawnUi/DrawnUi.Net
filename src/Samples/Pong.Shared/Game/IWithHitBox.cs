using SkiaSharp;

namespace Pong.Game;

public interface IWithHitBox
{
    void UpdateState(long time, bool forceRecalculate = false);
    SKRect HitBox { get; }
}