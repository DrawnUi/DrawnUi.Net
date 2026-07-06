namespace DrawnUi.Draw;

public class ProgressAnimator : SkiaValueAnimator
{
	public ProgressAnimator(IDrawnBase parent) : base(parent)
	{
		mMinValue = 0;
		mMaxValue = 1;
	}

	protected virtual void OnProgressChanged(double progress)
	{

	}

	protected override double TransformReportedValue(long deltaT)
	{
		var progress = base.TransformReportedValue(deltaT);

		OnProgressChanged(progress);

		return progress;
	}


}