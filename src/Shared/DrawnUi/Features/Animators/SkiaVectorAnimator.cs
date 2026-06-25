using System.Numerics;

namespace DrawnUi.Draw;

public class SkiaVectorAnimator : SkiaValueAnimator
{
	public Action<Vector2> OnVectorUpdated { get; set; }

	public Vector2 Vector { get; set; }

	protected override double TransformReportedValue(long deltaT)
	{
		OnVectorUpdated?.Invoke(Vector);

		return base.TransformReportedValue(deltaT);
	}

	public SkiaVectorAnimator(IDrawnBase parent) : base(parent)
	{
	}
}