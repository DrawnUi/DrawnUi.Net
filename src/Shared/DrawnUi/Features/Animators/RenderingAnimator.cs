namespace DrawnUi.Draw;

/// <summary>
/// This animator renders on canvas instead of just updating a value
/// </summary>
public class RenderingAnimator : SkiaValueAnimator, IOverlayEffect
{
    public override void Stop()
    {
        var parent = Parent;

        base.Stop();

        parent?.Repaint();
    }

    public bool Render(DrawingContext context, IDrawnBase control)
	{
		return OnRendering(context, control);
	}

	/// <summary>
	/// return true if has drawn something and rendering needs to be applied
	/// </summary>
	/// <param name="control"></param>
	/// <param name="context"></param>
	/// <param name="scale"></param>
	/// <returns></returns>
	protected virtual bool OnRendering(DrawingContext context, IDrawnBase control)
	{
		return false;
	}


	public RenderingAnimator(IDrawnBase parent) : base(parent)
	{

	}

	protected static SKPoint GetSelfDrawingLocation(IDrawnBase control)
	{
		SKPoint position;
		if (control is SkiaControl skia)
		{
			position = skia.GetSelfDrawingPosition();
		}
		else
		{
			//legacy case for drawnview
			position = new SKPoint(
				(float)(control.X * control.RenderingScale),
				(float)(control.Y * control.RenderingScale)
				);
		}
		return position;
	}

	protected static void DrawWithClipping(DrawingContext context, IDrawnBase control, SKPoint selfDrawingLocation, Action draw)
	{

		void Render()
		{
			if (control.ClipEffects)
			{
                using (SKPath clipInsideParent = new SKPath())
				{
					ApplyControlClipping(control, clipInsideParent, selfDrawingLocation);

					var count = context.Context.Canvas.Save();

					control.ClipSmart(context.Context.Canvas, clipInsideParent);
					draw();

					context.Context.Canvas.RestoreToCount(count);
				}
			}
			else
			{
				draw();
			}
		}

		if (control is SkiaControl skiaControl)
		{
			skiaControl.DrawWithClipAndTransforms(context.WithDestination( context.Context.Canvas.LocalClipBounds), control.DrawingRect, false,
				true, (ctx) =>
				{
					Render();
				});
		}
		else
		{
			Render();
		}

	}

	protected static void ApplyControlClipping(IDrawnBase control, SKPath clipInsideParent, SKPoint selfDrawingLocation)
	{
		SKPath clipContent;
		if (control is SkiaControl skia)
		{
            clipContent = skia.CreateClip(null, false);
			clipContent.Offset(selfDrawingLocation);
			clipInsideParent.AddPath(clipContent);
		}
		else
		{
			//legacy case for drawnview
			clipContent = control.CreateClip(null, true);
			clipContent.Offset((float)(control.TranslationX * control.RenderingScale), (float)(control.TranslationY * control.RenderingScale));
			clipInsideParent.AddPath(clipContent);
		}
		clipContent.Dispose();
	}
}
