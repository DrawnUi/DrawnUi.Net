using System.Collections.Concurrent;
using AppoMobi.Gestures;
using SkiaSharp;

namespace DrawnUi.Draw;

/// <summary>
/// A touch-driven SkSL post effect: every press fires an expanding shockwave that pushes
/// pixels radially outward at the wavefront, overshoots and springs back, with a heat flash
/// at the ring. Attach to a cached (GPU) control's <c>VisualEffects</c>; it captures its own
/// gestures (<see cref="ISkiaGestureProcessor"/>) and animates each wave to completion.
/// Single-texture (no reflection source), so it works on any content.
/// </summary>
public class SkiaTouchShockwaveEffect : SkiaShaderEffect, IStateEffect, ISkiaGestureProcessor
{
    private const int MaxWaves = 10;

    private const string ShockwaveShaderCode = """
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform shader iImage1;
uniform float2 iOffset;
uniform float iTime;   // fed by the engine every frame — MUST be declared or SkiaSharp throws
uniform float4 iMouse; // fed by the engine every frame — MUST be declared or SkiaSharp throws

uniform float2 origins[10];
uniform float progresses[10];
uniform float uScale;   // device pixels per point (control RenderingScale) — keeps the wave a stable
                        // physical size on any control, like the ripple's fixed 300pt diameter

// All sizes in POINTS (device-independent), turned into pixels via uScale. Absolute, so the wave
// looks identical on a small button and a full-screen image — it does NOT scale with the control.
const float reachPt = 150.0;    // ring travels this far from the tap
const float bandPt = 38.0;      // thickness of the shock band
const float amplitudePt = 16.0; // radial push strength
const float ringCycles = 5.0;   // overshoot oscillations across the band
const float brightness = 0.5;   // heat flash at the ring
const half3 heatTint = half3(1.0, 0.75, 0.45); // warm color of the flash

half4 main(float2 fragCoord)
{
    float2 renderingScale = iImageResolution.xy / iResolution.xy;
    float2 px = fragCoord - iOffset;              // local pixel coords inside the control

    float reach = reachPt * uScale;               // points -> pixels; independent of control size
    float band  = bandPt  * uScale;
    float amp   = amplitudePt * uScale;

    float2 disp = float2(0.0, 0.0);               // displacement in local pixels
    float glow = 0.0;

    for (int i = 0; i < 10; i++)
    {
        float p = progresses[i];
        if (p < 0.0 || p > 1.0) continue;

        float2 org = origins[i] - iOffset;        // origin in local pixels (canvas space - control origin)
        float2 dir = px - org;
        float d = max(length(dir), 0.0001);       // TRUE pixel distance -> circular on any aspect

        float delta = d - p * reach;              // signed pixel distance to the wavefront
        float g = exp(-(delta * delta) / (band * band));
        float spring = sin((delta / band) * ringCycles);  // overshoot / snap-back
        float fade = (1.0 - p);                   // whole wave decays as it expands
        float infl = g * fade;

        float2 n = dir / d;                        // unit pixel direction
        disp += n * amp * infl * (0.7 + 0.5 * spring);
        glow += infl;
    }

    float2 sample = (px - disp) * renderingScale;
    half4 col = iImage1.eval(sample);
    col.rgb += heatTint * (brightness * glow);
    return col;
}
""";

    public SkiaTouchShockwaveEffect()
    {
        ShaderCode = ShockwaveShaderCode;
    }

    /// <summary>Duration of a single shockwave in milliseconds.</summary>
    public double WaveDurationMs { get; set; } = 900;

    protected bool Initialized { get; set; }

    public virtual void UpdateState()
    {
        if (Parent != null && !Initialized && Parent.IsLayoutReady)
        {
            Initialized = true;
        }
    }

    public override void Attach(SkiaControl parent)
    {
        base.Attach(parent);
        UpdateState();
    }

    protected override SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var uniforms = base.CreateUniforms(destination);

        var active = GetActiveWaves();
        var origins = new float[MaxWaves * 2];
        var progresses = new float[MaxWaves];

        for (int i = 0; i < MaxWaves; i++)
        {
            if (i < active.Count)
            {
                origins[i * 2] = active[i].Origin.X;
                origins[i * 2 + 1] = active[i].Origin.Y;
                progresses[i] = (float)active[i].Progress;
            }
            else
            {
                progresses[i] = -1f;
            }
        }

        uniforms["origins"] = origins;
        uniforms["progresses"] = progresses;
        uniforms["uScale"] = (float)(Parent?.RenderingScale ?? 1.0); // points -> pixels
        return uniforms;
    }

    public class Wave
    {
        public Guid Uid { get; set; }
        public SKPoint Origin { get; set; }
        public long Time { get; set; }
        public double Progress { get; set; }
    }

    private ConcurrentDictionary<Guid, Wave> Waves { get; } = new();

    private List<Wave> GetActiveWaves()
        => Waves.Values.OrderByDescending(x => x.Time).Take(MaxWaves).ToList();

    public virtual ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (args.Type == TouchActionResult.Down && Initialized)
        {
            var wave = new Wave
            {
                Uid = Guid.NewGuid(),
                Origin = new SKPoint(args.Event.Location.X, args.Event.Location.Y),
                Time = Super.GetCurrentTimeNanos()
            };
            Waves[wave.Uid] = wave;

            Task.Run(async () =>
            {
                await Parent.AnimateRangeAsync(value =>
                {
                    wave.Progress = value;
                    Update();
                }, 0, 1, WaveDurationMs);

                Waves.TryRemove(wave.Uid, out _);
                Update();
            }).ConfigureAwait(false);
        }

        return null; // don't consume — let the control's own gestures still fire
    }
}
