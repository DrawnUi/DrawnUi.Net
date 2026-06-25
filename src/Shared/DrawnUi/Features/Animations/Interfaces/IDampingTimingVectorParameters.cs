using System.Numerics;

namespace DrawnUi.Draw;

public interface IDampingTimingVectorParameters : ITimingVectorParameters
{
    Vector2 AmplitudeAt(float offsetSecs);
}