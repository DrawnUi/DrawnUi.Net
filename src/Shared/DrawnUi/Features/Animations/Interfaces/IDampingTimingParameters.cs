namespace DrawnUi.Draw;

public interface IDampingTimingParameters : ITimingParameters
{
    float AmplitudeAt(float offsetSecs);
}