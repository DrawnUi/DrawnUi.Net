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
            if (CanDraw && IsVisibleInViewTree())
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
            if (newvalue && IsVisibleInViewTree())
            {
                _blinkAnimator.Start();
            }
            else
            {
                _blinkAnimator.Stop();
            }
        }
    }

    /// <summary>
    /// An ancestor's visibility can change without the cursor's own <see cref="SkiaControl.IsVisible"/>
    /// flag changing (e.g. the editor is focused while sitting under a hidden/collapsed parent, so it
    /// force-shows the cursor regardless of on-screen state). The blink animator gates only on the
    /// cursor's own visibility, so without this override it would keep ticking — repainting the canvas
    /// non-stop for a cursor that can never be seen — and would fail to resume when the ancestor reappears.
    /// Re-evaluate the blink against the full view-tree visibility here.
    /// </summary>
    public override void OnParentVisibilityChanged(bool newvalue)
    {
        base.OnParentVisibilityChanged(newvalue);

        if (_blinkAnimator != null)
        {
            if (newvalue && IsVisible && IsVisibleInViewTree())
                _blinkAnimator.Start();
            else
                _blinkAnimator.Stop();
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
