namespace DrawnUi.Draw;

public class ShimmerAnimator : RenderingAnimator
{
    public ShimmerAnimator(IDrawnBase control) : base(control)
    {
        IsPostAnimator = true;
        Speed = 1000;
        mMinValue = 0;
        mMaxValue = 1;
        Color = SKColor.Parse("#33FFFFFF");
        ShimmerAngle = 45;
        ShimmerWidth = 100;
    }

    protected static long count;

    public SKColor Color { get; set; }
    public double ShimmerWidth { get; set; }
    public double ShimmerAngle { get; set; }

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
        if (IsRunning)
        {
            var color = Color;

            // Original rectangle
            var selfDrawingLocation = GetSelfDrawingLocation(control);
            var originalRect = new SKRect(selfDrawingLocation.X, selfDrawingLocation.Y,
                selfDrawingLocation.X + control.DrawingRect.Width,
                selfDrawingLocation.Y + control.DrawingRect.Height);

            var maxSide = Math.Max(originalRect.Width, originalRect.Height);
            var centerX = originalRect.MidX;
            var centerY = originalRect.MidY;

            // Create a new square rectangle centered within the original rectangle
            var rect = new SKRect(centerX - maxSide / 2, centerY - maxSide / 2, centerX + maxSide / 2, centerY + maxSide / 2);

            //var shimmerWidth = ShimmerWidth * scale;
            var shimmerWidth = rect.Width;

            // Calculate the shimmer position based on the progress
            double diagonalLength = Math.Sqrt(Math.Pow(rect.Width, 2) + Math.Pow(rect.Height, 2));

            var shimmerStartX = (float)(rect.Left - shimmerWidth + (diagonalLength + shimmerWidth) * mValue);
            var shimmerEndX = shimmerStartX + (float)(shimmerWidth);

            var canvas = context.Context.Canvas;

            DrawWithClipping(context, control, selfDrawingLocation, () =>
            {
                Paint ??= new SKPaint();
                Paint.Style = SKPaintStyle.Fill;
                Paint.Color = color;
                Paint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(shimmerStartX, 0),
                    new SKPoint(shimmerEndX, 0),
                    new SKColor[]
                    {
                        SKColors.Transparent,
                        color,
                        SKColors.Transparent
                    },
                    new float[] { 0, 0.5f, 1 },
                    SKShaderTileMode.Clamp);

                if (ShimmerAngle == 0)
                {
                    canvas.DrawRect(rect, Paint);
                }
                else
                {
                    var saved = canvas.Save();
                    // Translate the canvas so that the center of the control is at the origin
                    var rotationX = rect.Left + rect.Width / 2f;
                    var rotationY = rect.Top + rect.Height / 2f;
                    canvas.Translate(rotationX, rotationY);
                    var m = SKMatrix.CreateRotationDegrees((float)ShimmerAngle);
                    canvas.Concat(ref m);
                    canvas.Translate(-rotationX, -rotationY);

                    canvas.DrawRect(rect, Paint);

                    canvas.RestoreToCount(saved);
                }
            });

            return true;
        }

        return false;
    }



}
