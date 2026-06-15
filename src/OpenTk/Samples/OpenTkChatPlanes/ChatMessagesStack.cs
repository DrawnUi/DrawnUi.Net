using DrawnUi.Draw;
using SkiaSharp;
using System.Collections.Specialized;
using System.Diagnostics;

namespace DrawnUiRepro;

/// <summary>
/// MAUI-parity stack: stale gesture tree + DrawCachedPlane + RenderTree.Offset path.
/// Copied from DrawnUiReproIS so the OpenTK harness exercises the exact MAUI gesture code path.
///
/// Smooth-scroll stack (Ops-style, banded). SkiaStack subclass owning ONE reused Operations (SKPicture)
/// cache that covers the viewport PLUS the virtualization inflation band (set via the framework's
/// <see cref="SkiaControl.VirtualisationInflated"/> + <see cref="SkiaControl.VirtualisationInflatedRatio"/>).
/// The cells are already <see cref="SkiaCacheType.Image"/>-cached, so the picture is a handful of cheap blits.
///
/// Three frame paths, cheapest first:
///  - STATIC (nothing moved/changed): DrawCachedPlane blit, layout pass skipped, gesture tree STALE.
///  - REUSE (scrolled, still inside band): DrawCachedPlane blit, gesture tree STALE.
///    RenderTree.Offset compensates so ProcessGestures still routes to the right cells.
///  - RECORD (band exit / dirty / content / fingerprint change): bake the band, then DrawCachedPlane.
/// </summary>
public class ChatMessagesStack : SkiaStack
{
    public ChatMessagesStack()
    {
        UseCache = SkiaCacheType.None; // we own DrawDirectInternal + our own Operations cache
        _paintAction = Paint;          // pre-alloc: no per-frame delegate/closure allocation
    }

    private readonly Action<DrawingContext> _paintAction;

    private SKPictureRecorder _recorder; // reused across record frames
    private bool _cacheValid;
    private long _cacheFingerprint, _passFingerprint; // structural fingerprint of the baked vs current cells
    private float _recordOffsetY;        // context.Destination.Top at the last record (band coverage origin)
    private SKRect _lastDestination, _lastDrawingRect; // for static-frame detection
    private bool _skipRedrawing;         // this frame: gate DrawChild off — tree + ArrangeCache still run
    private int _lastDrawn;              // last base.DrawStackVisibleChildren return value

    public CachedObject CachedPlane;     // reused — only Picture/Bounds/RecordingArea are swapped

    // invalidate the picture whenever the windowed ItemsSource changes (LoadMore/trim shifts content)
    private volatile bool _contentChanged = true;

    private int dirtyAfterCache = 0;

    /// <summary>Mutation hook (LoadMore/trim/add/remove on the same collection) — invalidate the picture.</summary>
    protected override void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
    {
        _contentChanged = true;
        base.OnItemsSourceCollectionChanged(sender, args);
    }

    public override void UpdateByChild(SkiaControl child)
    {
        base.UpdateByChild(child);
        TrackChildAsDirty(child);
    }

    protected override bool DrawChild(DrawingContext ctx, ISkiaControl child)
    {
        if (child == null || child.IsDisposed || child.IsDisposing)
            return false;
        if (_skipRedrawing)
            return true;
        return base.DrawChild(ctx, child);
    }

    /// <summary>Scroll-invariant fingerprint of the (banded) visible cells.</summary>
    private static long Fingerprint(List<ControlInStack> visible)
    {
        unchecked
        {
            long h = 17;
            for (int i = 0; i < visible.Count; i++)
            {
                var c = visible[i];
                if (c == null) continue;
                h = h * 31 + c.ControlIndex;
                h = h * 31 + c.Destination.GetHashCode();
            }
            return h;
        }
    }

    protected override int DrawStackVisibleChildren(DrawingContext ctx, LayoutStructure structure,
        List<ControlInStack> visibleElements, bool usesExpandedViewport, ScaledRect visibilityAreaReal,
        List<SkiaControlWithRect> tree, ref bool updateInternal)
    {
        _passFingerprint = Fingerprint(visibleElements);
        if (_skipRedrawing)
        {
            if (_passFingerprint != _cacheFingerprint)
            {
                _skipRedrawing = false; // band content changed -> draw the cells this frame
            }
            else
            {
                return _lastDrawn;
            }
        }

        _lastDrawn = base.DrawStackVisibleChildren(ctx, structure, visibleElements, usesExpandedViewport,
            visibilityAreaReal, tree, ref updateInternal);

        return _lastDrawn;
    }

    public override void OnChildTapped(SkiaControl child, SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (child.BindingContext is ChatMessage msg)
            Debug.WriteLine($"Tapped child data {msg.Index} index {child.ContextIndex}");
        else
            Debug.WriteLine($"Tapped child index {child.ContextIndex}");

        base.OnChildTapped(child, args, apply);
    }

    void DrawCachedPlane(DrawingContext context, SKRect dest)
    {
        // Draw at the RECORDED position, applying a translation so the picture ends up at `dest`.
        // Set RenderTree.Offset = that same translation so ProcessGestures compensates via AdjustOffset
        // and hit-tests still land on the right cells even though the gesture tree is stale.
        var moveY = CachedPlane.Bounds.Top - CachedPlane.RecordingArea.Top;
        var moveX = CachedPlane.Bounds.Left - CachedPlane.RecordingArea.Left;

        var x = (float)(dest.Left - CachedPlane.Bounds.Left + moveX);
        var y = (float)(dest.Top - CachedPlane.Bounds.Top + moveY);

        this.RenderTree.Offset = new SKPoint(x, y); // fix gestures: AdjustOffset subtracts this in ProcessGestures

        CachedPlane!.Draw(context.Context.Canvas, dest, null);
    }

    public override void DrawDirectInternal(DrawingContext context, SKRect drawingRect)
    {
        if (drawingRect.Height == 0 || drawingRect.Width == 0 || IsDisposed || IsDisposing)
            return;

#if DEBUG
        CheckIntegrity();
#endif

        var dest = context.Destination;

        bool dirty = !DirtyChildrenTracker.IsEmpty;
        if (dirty)
        {
            dirtyAfterCache++;
            if (dirtyAfterCache < 2)
                dirty = false;
        }

        bool useCache = false;

        // STATIC PATH
        if (_cacheValid && !dirty && !_contentChanged && dest == _lastDestination && drawingRect == _lastDrawingRect)
        {
            useCache = true;
        }
        else
        {
            var clip = context.Context.Canvas.LocalClipBounds;
            float viewportH = clip.Height > 0 ? clip.Height : drawingRect.Height;
            float overscanPx = (float)(VirtualisationInflated * context.Scale);
            if (VirtualisationInflatedRatio >= 0)
                overscanPx += (float)(VirtualisationInflatedRatio * viewportH);

            // REUSE PATH
            useCache = _cacheValid && !dirty && !_contentChanged
                       && Math.Abs(dest.Top - _recordOffsetY) <= overscanPx;
        }

        if (useCache)
        {
            // Gesture tree is STALE here — RenderTree.Offset set by DrawCachedPlane compensates
            DrawCachedPlane(context, dest);
            return;
        }

        // RECORD PATH
        _skipRedrawing = false;
        _recorder ??= new SKPictureRecorder();
        var rc = context.CreateForRecordingOperations(_recorder, drawingRect);

        DrawWithClipAndTransforms(rc, drawingRect, true, true, (recordingContext) =>
        {
            Paint(recordingContext);
        });

        var picture = _recorder.EndRecording();

        if (!_skipRedrawing)
        {
            dirtyAfterCache = 0;
            if (CachedPlane == null)
            {
                CachedPlane = new CachedObject(SkiaCacheType.Operations, picture, dest, drawingRect);
            }
            else
            {
                CachedPlane.Picture?.Dispose();
                CachedPlane.Picture = picture;
                CachedPlane.Bounds = dest;
                CachedPlane.RecordingArea = drawingRect;
            }
            Debug.WriteLine($"Created stack cache");
        }

        DrawCachedPlane(context, dest);

        _cacheValid = true;
        _cacheFingerprint = _passFingerprint;
        _recordOffsetY = dest.Top;
        _contentChanged = false;
        _lastDestination = dest;
        _lastDrawingRect = drawingRect;
    }

    public override void OnDisposing()
    {
        DisposeObject(CachedPlane); CachedPlane = null;
        _recorder?.Dispose(); _recorder = null;
        base.OnDisposing();
    }

#if DEBUG
    private int _integFrame;
    private string _lastBad;

    private void CheckIntegrity()
    {
        var tree = RenderTree;
        int n = tree?.Count ?? 0;
        if (n < 3)
            return;

        _integFrame++;

        int mn = int.MaxValue, mx = int.MinValue;
        for (int i = 0; i < n; i++)
        {
            int idx = tree[i].FreezeIndex;
            if (idx < mn) mn = idx;
            if (idx > mx) mx = idx;
        }

        if (mx - mn + 1 == n)
        {
            _lastBad = null;
            return;
        }

        string sig = $"span{mn}..{mx}/{n}";
        if (sig == _lastBad)
            return;
        _lastBad = sig;

        var arr = new List<(int idx, float top, float bot)>(n);
        foreach (var t in tree)
            arr.Add((t.FreezeIndex, t.HitRect.Top, t.HitRect.Bottom));
        arr.Sort((x, y) => x.idx.CompareTo(y.idx));

        var sb = new System.Text.StringBuilder();
        int rs = arr[0].idx, pv = rs, gFrom = 0, gTo = 0;
        float gapY = 0;
        for (int k = 1; k < arr.Count; k++)
        {
            if (arr[k].idx - pv != 1)
            {
                sb.Append($"[{rs}..{pv}]");
                if (gTo == 0) { gFrom = pv; gTo = arr[k].idx; gapY = arr[k].top - arr[k - 1].bot; }
                rs = arr[k].idx;
            }
            pv = arr[k].idx;
        }
        sb.Append($"[{rs}..{pv}]");

        Debug.WriteLine($"[CHAT-INTEGRITY] f{_integFrame} blocks{sb} firstgap{gFrom}->{gTo} gapY{gapY:0}");
    }
#endif
}
