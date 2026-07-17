namespace DrawnUi.Draw;

/// <summary>
/// Generic overlay effect rendered at the post-effects stage (the same stage where the
/// ripple draws), above the control's own content and children. Wraps a delegate so apps
/// can draw custom overlay chrome (selection frames, debug bounds, badges) without
/// subclassing: <c>control.PostAnimators.Add(new ActionOverlayEffect(control, (ctx, c) =&gt; { ... return false; }))</c>
/// or the fluent <c>.WhenPainted((ctx, c) =&gt; ...)</c>.
/// The delegate returns true to request continuous repaint (animated overlays),
/// false for static overlays that just draw whenever the control paints.
/// For a reusable custom effect, subclass <see cref="RenderingAnimator"/> instead and
/// override <c>OnRendering</c> — same contract, no library changes needed.
/// </summary>
public class ActionOverlayEffect : RenderingAnimator
{
    private readonly Func<DrawingContext, IDrawnBase, bool> _render;

    /// <summary>
    /// Creates the overlay for the given control. No animation is started — adding the
    /// effect to <see cref="SkiaControl.PostAnimators"/> is enough for it to render.
    /// </summary>
    /// <param name="control">Owner control the overlay is drawn for.</param>
    /// <param name="render">Draw callback; return true to request continuous repaint.</param>
    public ActionOverlayEffect(IDrawnBase control, Func<DrawingContext, IDrawnBase, bool> render) : base(control)
    {
        IsPostAnimator = true;
        _render = render;
    }

    protected override bool OnRendering(DrawingContext context, IDrawnBase control)
    {
        if (control == null || control.IsDisposed || control.IsDisposing)
            return false;

        return _render?.Invoke(context, control) ?? false;
    }
}
