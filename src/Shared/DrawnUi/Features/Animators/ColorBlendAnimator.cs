namespace DrawnUi.Draw;

public class ColorBlendAnimator : ProgressAnimator
{
	public ColorBlendAnimator(IDrawnBase parent) : base(parent)
	{
		Repeat = 0;
		Speed = 250; //ms
		Color1 = Colors.Red;
		Color2 = Colors.Green;
		Easing = Easing.Linear;
	}


	protected override void OnProgressChanged(double progress)
	{
		Color = GetColor(Color1, Color2, (float)progress);
		OnColorChanged?.Invoke(Color);
	}

	public Color Color { get; protected set; }

	#region PARAMETERS

	public Color Color1 { get; set; }
	public Color Color2 { get; set; }

	public Action<Color> OnColorChanged { get; set; }

	#endregion

	public static Color GetColor(Color start, Color end, float progress)
	{
		if (progress < 0f || progress > 1f)
			throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0.0f and 1.0f.");

		float r = Blend(start.Red, end.Red, progress);
		float g = Blend(start.Green, end.Green, progress);
		float b = Blend(start.Blue, end.Blue, progress);
		float a = Blend(start.Alpha, end.Alpha, progress);

		return new Color(r, g, b, a);
	}

	// Helper function to blend individual components
	private static float Blend(float start, float end, float progress)
	{
		return start + (end - start) * progress;
	}

	CancellationTokenSource cancellationTokenSource;

	public Task AnimateAsync(Action<Color> callback, Color start, Color end, double msLength = 250, Easing easing = null, CancellationTokenSource cancel = default)
	{
		if (IsRunning)
			Stop();

		if (cancel == default)
			cancel = new CancellationTokenSource();

		var tcs = new TaskCompletionSource<bool>(cancel.Token);

		OnColorChanged = (value) =>
		{
			if (!cancel.IsCancellationRequested)
			{
				callback?.Invoke(value);
				//System.Diagnostics.Debug.WriteLine($"ColorBlendAnimator: {value:0.00}");
			}
			else
			{
				Stop();
			}
		};
		Color1 = start;
		Color2 = end;
		Speed = msLength;
		Easing = easing;
		OnStop = () =>
		{
			tcs.SetResult(true);
		};

		Start();

		return tcs.Task;
	}



}
