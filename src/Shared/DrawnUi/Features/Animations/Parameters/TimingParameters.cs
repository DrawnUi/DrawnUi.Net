namespace DrawnUi.Draw;

public interface ITimingParameters
{
    float DurationSecs { get; }
    float ValueAt(float offsetSecs);
}