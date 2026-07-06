namespace DrawnUi.Draw;

public class SkiaEffect : BindableObject, IDisposable, ICanBeUpdatedWithContext
{
    /// <summary>
    /// For public set use Attach/Detach
    /// </summary>
    public SkiaControl Parent { get; protected set; }

    public new string Tag { get; set; }
    protected virtual void OnDisposing()
    {

    }

    public virtual void Attach(SkiaControl parent)
    {
        this.Parent = parent;
        this.BindingContext = parent.BindingContext;
    }

    public virtual void Dettach()
    {
        this.BindingContext = null;
        this.Parent = null;
    }

    public void Dispose()
    {
        OnDisposing();
        Parent = null;
    }

    /// <summary>
    /// You MUST clear any cached resources as soon as this is called. Not on next draw!
    /// </summary>
    public virtual void Update()
    {
        Parent?.InvalidateEffectsMargin();
        Parent?.Update();
    }

    /// <summary>
    /// Extra space in PIXELS this effect paints beyond the control's DrawingRect (drop shadow, glow, etc.).
    /// Return Thickness.Zero (default) for effects that stay within bounds.
    /// The engine reads this to auto-expand the cache surface and clip so out-of-bounds effects are not clipped.
    /// Order is (left, top, right, bottom). Blur is treated as pixels, offsets are scaled by the passed scale.
    /// </summary>
    public virtual Thickness GetEffectMargin(float scale) => Thickness.Zero;

    protected static void NeedUpdate(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaEffect effect)
        {
            effect.Update();
        }
    }

    public virtual bool NeedApply
    {
        get
        {
            return Parent != null;
        }
    }

}
