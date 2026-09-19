using System.Collections.Concurrent;
using AppoMobi.Gestures;
using DrawnUi.Draw;
using SkiaSharp;

namespace HelloWpf.Pages;

/// <summary>
/// Port of the Sandbox MultiRippleWithTouchEffect: a WWDC-style ripple starts where the control is
/// touched (up to 10 at once). The shader is <c>shaders/ripples.sksl</c>; iImage1 is the host's own
/// cache, iImage2 (<see cref="ShaderDoubleTexturesEffect.SecondarySource"/>) the reflection texture.
/// </summary>
public class MultiRippleWithTouchEffect : ShaderDoubleTexturesEffect, IStateEffect, ISkiaGestureProcessor
{
    /// <summary>Creates the effect pointing at the ripples shader file.</summary>
    public MultiRippleWithTouchEffect()
    {
        ShaderSource = "shaders/ripples.sksl";
    }

    /// <summary>True once the host has laid out.</summary>
    protected bool Initialized { get; set; }

    /// <inheritdoc/>
    public virtual void UpdateState()
    {
        if (Parent != null && !Initialized && Parent.IsLayoutReady)
            Initialized = true;
    }

    /// <inheritdoc/>
    public override void Attach(SkiaControl parent)
    {
        base.Attach(parent);
        UpdateState();
    }

    /// <inheritdoc/>
    protected override SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var uniforms = base.CreateUniforms(destination);

        var active = Ripples.Values.OrderByDescending(x => x.Time).Take(10).ToList();
        var origins = new float[10 * 2];
        var progresses = new float[10];

        for (var i = 0; i < 10; i++)
        {
            if (i < active.Count)
            {
                origins[i * 2] = active[i].Origin.X;
                origins[i * 2 + 1] = active[i].Origin.Y;
                progresses[i] = (float)active[i].Progress;
            }
            else
            {
                progresses[i] = -1f; // inactive
            }
        }

        uniforms["origins"] = origins;
        uniforms["progresses"] = progresses;
        return uniforms;
    }

    private sealed class Ripple
    {
        public Guid Uid;
        public System.Drawing.PointF Origin;
        public long Time;
        public double Progress;
    }

    private ConcurrentDictionary<Guid, Ripple> Ripples { get; } = new();

    /// <inheritdoc/>
    public ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (args.Type != TouchActionResult.Down || !Initialized)
            return null;

        var ripple = new Ripple { Uid = Guid.NewGuid(), Origin = args.Event.Location, Time = Super.GetCurrentTimeNanos() };
        Ripples[ripple.Uid] = ripple;

        Task.Run(async () =>
        {
            await Parent.AnimateRangeAsync(value =>
            {
                ripple.Progress = value;
                Update();
            }, 0, 1, 4500);

            Ripples.TryRemove(ripple.Uid, out _);
        }).ConfigureAwait(false);

        return null;
    }
}
