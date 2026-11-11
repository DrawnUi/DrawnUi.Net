#nullable disable
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace DrawnUi.Add3d;

/// <summary>A 3D control that can render once ahead of being seen (<see cref="Prefetch3D"/>).</summary>
internal interface IPrefetch3D
{
    /// <summary>True once it rendered (ahead or on screen): nothing left to warm.</summary>
    bool GpuReady { get; }

    /// <summary>Renders once off screen at its measured size: uploads its model and images, makes its target.</summary>
    void Prefetch(GRContext gr);
}

/// <summary>
/// Warms 3D controls before they scroll into view, the way images are preloaded: once a control comes within one
/// viewport height of the screen (above or below), it renders once off screen in an idle moment after a frame, one
/// control per frame. Its first frame on screen then only shows that image (or draws with everything uploaded).
/// Controls never scrolled near take nothing. The same rule as DrawnUI for Rust's add-on.
/// </summary>
internal static class Prefetch3D
{
    sealed class Waiting
    {
        public readonly List<WeakReference<SkiaControl>> Controls = new();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DrawnView, Waiting> Canvases = new();

    /// <summary>Queues a measured control that has not rendered yet; false when it is on no canvas yet.</summary>
    public static bool Add(SkiaControl control)
    {
        if (control.Superview is not DrawnView canvas)
            return false;
        var waiting = Canvases.GetValue(canvas, c =>
        {
            var w = new Waiting();
            c.WasDrawn += (sender, _) => Tick(sender as DrawnView, w);
            return w;
        });
        lock (waiting.Controls)
            waiting.Controls.Add(new WeakReference<SkiaControl>(control));
        return true;
    }

    /// <summary>After a frame: renders the first waiting control that is near the screen.</summary>
    static void Tick(DrawnView canvas, Waiting waiting)
    {
        if (canvas == null || waiting.Controls.Count == 0)
            return;
        var gr = canvas.GetGRContext();
        // ponytail: GL only (web, desktop, Android GL); Vulkan and Metal render on first sight, add a prefetch there
        // when their first frames show up in measurements.
        if (gr == null || gr.Backend != GRBackend.OpenGL)
            return;
        // The 3D program compiles in the GPU driver while the next frames go on (its status is read at the first draw):
        // nothing is rendered ahead in the frame that starts it, which would wait for the compile.
        if (Renderer3D.StartProgram(gr))
            return;
        var screen = (float)(canvas.Height * canvas.RenderingScale);
        if (screen <= 0)
            return;
        SkiaControl pick = null;
        lock (waiting.Controls)
        {
            for (var i = waiting.Controls.Count - 1; i >= 0; i--)
            {
                if (!waiting.Controls[i].TryGetTarget(out var control) || control.IsDisposed || control is not IPrefetch3D p || p.GpuReady)
                {
                    waiting.Controls.RemoveAt(i);
                    continue;
                }
                var at = Estimate(control);
                if (at.Height > 0 && at.Bottom > -screen && at.Top < 2 * screen)
                {
                    waiting.Controls.RemoveAt(i);
                    pick = control;
                    break;
                }
            }
        }
        (pick as IPrefetch3D)?.Prefetch(gr);
    }

    /// <summary>
    /// Where a control not drawn yet will be on the canvas, in pixels: the slot its nearest drawn ancestor's stack gave
    /// to the branch holding it (stack slots are laid out ahead and kept in the stack's own space), else that
    /// ancestor's own rect. Empty when no ancestor was drawn in the last frame.
    /// </summary>
    internal static SKRect Estimate(SkiaControl control)
    {
        var node = control;
        while (node.Parent is SkiaControl parent)
        {
            if (parent.WasInLastFrame())
            {
                if (parent is SkiaLayout { IsTemplated: false } layout && layout.Type is LayoutType.Column or LayoutType.Row or LayoutType.Wrap)
                {
                    foreach (var cell in layout.GetStackStructure().GetChildren())
                    {
                        if (!ReferenceEquals(cell.View, node))
                            continue;
                        var slot = cell.Destination;
                        var scale = layout.RenderingScale;
                        slot.Offset(layout.DrawingRect.Left + (float)(layout.Padding.Left * scale), layout.DrawingRect.Top + (float)(layout.Padding.Top * scale));
                        return slot;
                    }
                }
                return parent.DrawingRect;
            }
            node = parent;
        }
        return SKRect.Empty;
    }
}
