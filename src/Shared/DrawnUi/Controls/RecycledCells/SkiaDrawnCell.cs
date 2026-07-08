using DrawnUi.Draw;
using System.ComponentModel;

namespace DrawnUi.Controls;

/// <summary>
/// Base ISkiaCell implementation
/// </summary>
public class SkiaDrawnCell : SkiaLayout, ISkiaCell
{
    protected virtual void SetContent(object ctx)
    {

    }

    private bool? _residentInPlanes;
    private bool _refreshingContent;

    /// <summary>
    /// Cleared when the cell is re-parented so planes-residency is recomputed on next Update.
    /// </summary>
    public override void OnParentChanged(IDrawnBase newvalue, IDrawnBase oldvalue)
    {
        _residentInPlanes = null;
        base.OnParentChanged(newvalue, oldvalue);
    }

    /*
    /// <summary>
    /// In tiled-planes virtualization a realized cell stays bound to the SAME model object across its life
    /// (it is not recycled while resident), so a model PROPERTY change never re-runs SetContent — the visual
    /// would keep stale text. Make Update() (the universal "refresh me") re-pull content from the model so
    /// `cell.Update()` after mutating the bound model just works, exactly like a non-virtualized list.
    /// Only inside a planes scroll; guarded against re-entrancy.
    /// </summary>
    public override void Update()
    {
        if (!_refreshingContent)
        {
            _residentInPlanes ??= Parent is SkiaControl p && p.Parent is SkiaScroll s && s.UseVirtual;
            if (_residentInPlanes == true && BindingContext != null)
            {
                _refreshingContent = true;
                try
                {
                    SetContent(BindingContext);
                }
                finally
                {
                    _refreshingContent = false;
                }
            }
        }

        base.Update();
    }
    */

    public virtual void OnScrolled()
    {

    }

    public virtual void Remeasure()
    {
        this.WasMeasured = false; //will be used by parent to measure 100%

        if (Parent is SkiaLayout layout)
        {
            var constraints = new SKRect(0, 0, _lastMeasuredForWidth, _lastMeasuredForHeight);
            var scale = RenderingScale;
            layout.MeasureSingleItem(this.ContextIndex, constraints, scale, default, false);
        }

        Parent?.InvalidateByChild(this);
    }

    public virtual TouchActionEventHandler LongPressingHandler => (sender, args) =>
    {
        args.PreventDefault = true;
    };

    private bool _isAttaching;

    public INotifyPropertyChanged Context { get; protected set; }

    public override void OnDisposing()
    {
        base.OnDisposing();

        FreeContext();
    }

    protected virtual void FreeContext()
    {
        Context = null;
    }

    protected virtual void AttachContext(object ctx)
    {
        if (ctx != null)
        {
            Context = ctx as INotifyPropertyChanged;
        }
    }

    private object LockContext = new();


    public override void ApplyBindingContext()
    {
        base.ApplyBindingContext();

        var ctx = BindingContext;

        if (ctx != Context && !_isAttaching)
        {
            _isAttaching = true;

            FreeContext();

            if (Context == null)
            {
                LockUpdate(true);

                SetContent(ctx);
                AttachContext(ctx);

                LockUpdate(false);
            }
            _isAttaching = false;
        }

    }






}
