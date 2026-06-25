using DrawnUi.Draw;

namespace DrawnUi.Draw;

public class SkiaCursor : SkiaShape
{
    private const long SolidOnMoveDurationNanos = 200_000_000L;

    private SkiaLabel _label;
    private long _solidUntilNanos;

    /// <summary>
    /// Called by SkiaEditor after creating the cursor. Parent chain is not yet complete at AddSubView time.
    /// </summary>
    public void Initialize(SkiaLabel label)
    {
        _label = label;
        if (_blinkAnimator == null)
        {
            _blinkAnimator = new ToggleAnimator(this)
            {
                Repeat = -1,
                Speed = 1000,
                Ratio = 0.5,
                OnUpdated = (value) =>
                {
                    var opacity = Super.GetCurrentTimeNanos() < _solidUntilNanos
                        ? 1.0
                        : (_blinkAnimator.State ? 0.01 : 1.0);

                    if (Opacity != opacity)
                        Opacity = opacity;
                }
            };
        }

        UpdateColors();
    }

    internal void OnMoved()
    {
        _solidUntilNanos = Super.GetCurrentTimeNanos() + SolidOnMoveDurationNanos;
        if (Opacity != 1.0)
            Opacity = 1.0;
        Repaint();
    }

    public override void Invalidate()
    {
        base.Invalidate();

        if (_blinkAnimator != null)
        {
            if (CanDraw)
                _blinkAnimator.Start();
            else
                _blinkAnimator.Stop();
        }
    }

    public override void OnVisibilityChanged(bool newvalue)
    {
        base.OnVisibilityChanged(newvalue);

        if (_blinkAnimator != null)
        {
            if (newvalue)
            {
                _blinkAnimator.Start();
            }
            else
            {
                _blinkAnimator.Stop();
            }
        }
    }

    private ToggleAnimator _blinkAnimator;

    //public override void OnBeforeDraw()
    //{
    //	base.OnBeforeDraw();

    //	if (_label != null)
    //	{
    //		this.HeightRequest = _label.LineHeightPixels / RenderingScale;
    //	}

    //}

    public static float BlinkAlpha = 0.0f;

    protected void UpdateColors()
    {
        BackgroundColor = Color;
        //todo add gradients
    }

    public override void OnDisposing()
    {
        _label = null;

        _blinkAnimator?.Dispose();

        base.OnDisposing();
    }

    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color),
        typeof(Color),
        typeof(SkiaCursor),
        Colors.Red, propertyChanged: (b, o, n) =>
        {
            if (b is SkiaCursor control)
            {
                control.UpdateColors();
            }
        });

    public Color Color
    {
        get { return (Color)GetValue(ColorProperty); }
        set { SetValue(ColorProperty, value); }
    }

}
