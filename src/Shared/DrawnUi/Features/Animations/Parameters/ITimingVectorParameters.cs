using System.Numerics;

namespace DrawnUi.Draw;

public interface ITimingVectorParameters
{
    float DurationSecs { get; }
    Vector2 ValueAt(float offsetSecs);
}