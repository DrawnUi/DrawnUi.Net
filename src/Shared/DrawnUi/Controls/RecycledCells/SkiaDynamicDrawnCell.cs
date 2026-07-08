using System.Threading;

namespace DrawnUi.Controls;

/// <summary>
/// This cell can watch binding context property changing
/// </summary>
public class SkiaDynamicDrawnCell : SkiaDrawnCell
{
    public void UpdateVisibilityChanged()
    {
        if (Parent is SkiaLayout layout)
        {
            layout.ReportChildVisibilityChanged(this.ContextIndex, IsVisible);
        }
    }

    public override void OnVisibilityChanged(bool value)
    {
        UpdateVisibilityChanged();
    }

    protected SKSize LastMeasuredSizePixels = new SKSize(-1, -1);

    protected override void FreeContext()
    {
        if (Context != null)
        {
            Context.PropertyChanged -= ContextPropertyChanged;
        }

        base.FreeContext();
    }

    /// <summary>
    /// When the cell is bound to a NEW context the cache pixels show the OLD content, so by default we hard-
    /// destroy the cache (blank until rebaked) — correct for recycled lists where stale pixels would flash
    /// wrong content. Non-recycled cells (e.g. a chat where a rebind means the SAME message updated) can
    /// override to false: the stale front stays drawable for 1-2 frames while the async rebake swaps in,
    /// avoiding the blank blink entirely.
    /// </summary>
    protected virtual bool DestroyCacheOnContextChange => true;

    protected override void AttachContext(object ctx)
    {

        if (Context != null)
            Context.PropertyChanged -= ContextPropertyChanged;

        base.AttachContext(ctx);

        if (Context != null)
            Context.PropertyChanged += ContextPropertyChanged;

        if (DestroyCacheOnContextChange)
        {
            DestroyRenderingObject();
        }
        else
        {
            NeedUpdateFrontCache = true; // rebake async; old pixels bridge the gap instead of a blank
        }
    }

    protected virtual void ContextPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
    }
}
