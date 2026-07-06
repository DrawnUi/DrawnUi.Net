namespace DrawnUi.Draw;

public class OuterGlowEffect : BaseImageFilterEffect
{
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color),
        typeof(Color),
        typeof(OuterGlowEffect),
        Colors.White,
        propertyChanged: NeedUpdate);

    public Color Color
    {
        get { return (Color)GetValue(ColorProperty); }
        set { SetValue(ColorProperty, value); }
    }

    public static readonly BindableProperty BlurProperty = BindableProperty.Create(
        nameof(Blur),
        typeof(double),
        typeof(OuterGlowEffect),
        5.0,
        propertyChanged: NeedUpdate);

    public double Blur
    {
        get { return (double)GetValue(BlurProperty); }
        set { SetValue(BlurProperty, value); }
    }

    public override SKImageFilter CreateFilter(SKRect destination)
    {
        if (NeedApply)
        {
            if (Filter == null)
            {
                Filter = SKImageFilter.CreateDropShadow(
                    0,
                    0,
                    (float)Blur,
                    (float)Blur,
                    Color.ToSKColor());
            }
        }
        return Filter;
    }

    public override bool NeedApply
    {
        get
        {
            return base.NeedApply && (this.Blur > 0);
        }
    }

    public override Thickness GetEffectMargin(float scale)
    {
        if (!NeedApply)
            return Thickness.Zero;

        var spread = Blur * 3.0; //~3 sigma, no offset (glow is symmetric)
        return new Thickness(spread);
    }
}
