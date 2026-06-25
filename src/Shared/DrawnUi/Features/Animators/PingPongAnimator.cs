namespace DrawnUi.Draw;

public class PingPongAnimator : RangeAnimator
{
    public PingPongAnimator(SkiaControl player) : base(player)
    {
        Repeat = -1;
    }

    protected override bool FinishedRunning()
    {


        if (Repeat < 0) //forever
        {
            CycleFInished?.Invoke();

            (mMaxValue, mMinValue) = (mMinValue, mMaxValue);
            Distance = mMaxValue - mMinValue;

            mValue = mMinValue;
            LastFrameTimeNanos = 0;
            StartFrameTimeNanos = 0;
            return false;
        }
        else if (Repeat > 0)
        {
            CycleFInished?.Invoke();

            Repeat--;

            (mMaxValue, mMinValue) = (mMinValue, mMaxValue);
            Distance = mMaxValue - mMinValue;

            mValue = mMinValue;
            LastFrameTimeNanos = 0;
            StartFrameTimeNanos = 0;
            return false;
        }

        return base.FinishedRunning();
    }
}