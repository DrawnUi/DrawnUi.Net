namespace DrawnUi.Draw;

public class RippleAnimator : RenderingAnimator
{
    public RippleAnimator(IDrawnBase control) : base(control)
    {
        IsPostAnimator = true;
        Speed = 500; //250;
        mMinValue = 0;
        mMaxValue = 1;
        Color = SKColor.Parse("#FFFFFF");
        Easing = Easing.CubicIn;
    }

    protected static long count;
    public SKColor Color { get; set; }
    public static double DiameterDefault = 300.0;
    public static double OpacityDefault = 0.20;

    /// <summary>
    /// In pts relative to control X,Y. These are coords inside the control and not inside the canvas. 
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// In pts relative to control X,Y. These are coords inside the control and not inside the canvas. 
    /// </summary>
    public double Y { get; set; }

    public double Diameter { get; set; }
    public double Opacity { get; set; }

    public override void Dispose()
    {
        if (Parent != null)
        {
            Parent.DisposeObject(Paint);
        }

        base.Dispose();
    }

    protected SKPaint Paint;

    protected override bool OnRendering(DrawingContext context, IDrawnBase control)
    {
        if (IsRunning && control != null && !control.IsDisposed && !control.IsDisposing)
        {
            var touchOffset = new SKPoint((float)(X * context.Scale), (float)(Y * context.Scale));

            if (control is SkiaControl skia && skia.ClippedEffectsWith != null)
            {
                control = skia.ClippedEffectsWith;
            }

            var selfDrawingLocation = GetSelfDrawingLocation(control);

            DrawWithClipping(context, control, selfDrawingLocation, () =>
            {
                Paint ??= new SKPaint();
                Paint.Style = SKPaintStyle.Fill;
                Paint.Color = Color.WithAlpha((byte)(Opacity * 255));

                touchOffset.Offset(selfDrawingLocation);
                context.Context.Canvas.DrawCircle(touchOffset.X, touchOffset.Y, (float)(Diameter * context.Scale),
                    Paint);
            });

            return true;
        }

        return false;
    }

    protected override double TransformReportedValue(long deltaT)
    {
        var progress = base.TransformReportedValue(deltaT);

        var opacityProgress = progress * 1.15;
        if (opacityProgress <= 1)
        {
            Opacity = OpacityDefault - OpacityDefault * opacityProgress;
        }

        Diameter = DiameterDefault * progress;

        return progress;
    }
}
