namespace DrawnUi.Draw;

public static class SpringExtensions
{
	public static float Damping(this Spring spring)
	{
		return 2 * spring.DampingRatio * MathF.Sqrt(spring.Mass * spring.Stiffness);
	}

	public static float Beta(this Spring spring)
	{
		return spring.Damping() / (2 * spring.Mass);
	}

	public static float DampedNaturalFrequency(this Spring spring)
	{
		return MathF.Sqrt(spring.Stiffness / spring.Mass) * MathF.Sqrt(1 - spring.DampingRatio * spring.DampingRatio);
	}
}