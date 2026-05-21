using AppoMobi.Gestures;
using System.Collections.Concurrent;
using DrawnUi.Draw;
using SkiaSharp;

namespace BlazorSandbox.Pages;

public class MultiRippleWithTouchEffect : ShaderDoubleTexturesEffect,
    IStateEffect, ISkiaGestureProcessor
{
    private const string RippleShaderCode = """
uniform float4 iMouse;
uniform float iTime;
uniform float2 iResolution;
uniform float2 iImageResolution;
uniform shader iImage1;
uniform shader iImage2;
uniform float2 iOffset;
uniform float2 iOrigin;

uniform float2 origins[10];
uniform float progresses[10];

const float duration = 5.0;
const float amplitude = 0.015;
const float frequency = 15.0;
const float decay = 2.0;
const float speed = 0.8;
const float rippleIntensity = 0.05;

const float reflectionIntensity = 0.15;
const float minReflectionIntensity = 1.5;

const vec3 waterTint = vec3(0.1, 0.2, 0.5);

const float reflectionAngleX = 5.0;
const float reflectionAngleY = 5.0;
const float reflectionAngleZ = 1.0;

half4 main(float2 fragCoord)
{
    float2 renderingScale = iImageResolution.xy / iResolution.xy;
    float2 inputCoord = (fragCoord - iOffset) * renderingScale;
    vec2 uv = (fragCoord - iOffset) / iResolution.xy;

    half4 baseColor = iImage1.eval(inputCoord);
    vec2 combinedDisplacement = vec2(0.0, 0.0);

    for (int i = 0; i < 10; i++)
    {
        float progress = progresses[i];
        vec2 mouse = origins[i];

        if (progress >= 0.0 && progress <= 1.0)
        {
            vec2 origin = mouse / iResolution.xy;
            vec2 direction = uv - origin;
            float distance = max(length(direction), 0.0001);
            float delay = distance / speed;
            float time = max(0.0, progress * duration - delay);
            float rippleAmount = amplitude * sin(frequency * time) * exp(-decay * time);
            vec2 n = direction / distance;
            combinedDisplacement += rippleAmount * n;
        }
    }

    vec2 finalPosition = uv + combinedDisplacement;
    vec3 finalColor = iImage1.eval(finalPosition * iResolution.xy * renderingScale).rgb;

    vec2 viewingDirection = normalize(vec2(reflectionAngleX, reflectionAngleY));
    float fresnelEffect = pow(1.0 - dot(normalize(combinedDisplacement), viewingDirection), 3.0);
    float reflectionFactor = fresnelEffect * clamp(length(combinedDisplacement) / amplitude, 0.0, 1.0);
    reflectionFactor = max(reflectionFactor, minReflectionIntensity);

    float dynamicPerturbationFactor = length(combinedDisplacement) * 10000.0;
    vec2 perturbation = vec2(sin(finalPosition.x * dynamicPerturbationFactor), cos(finalPosition.y * dynamicPerturbationFactor)) * 0.05;

    vec2 angleOffsetX = vec2(reflectionAngleX * combinedDisplacement.y, reflectionAngleX * combinedDisplacement.x);
    vec2 angleOffsetY = vec2(reflectionAngleY * combinedDisplacement.y, reflectionAngleY * combinedDisplacement.x);
    vec2 angleOffsetZ = vec2(reflectionAngleZ * combinedDisplacement.y, reflectionAngleZ * combinedDisplacement.x);
    vec2 angleOffset = angleOffsetX + angleOffsetY + angleOffsetZ;

    vec2 distortedCoord = finalPosition + angleOffset + perturbation;
    vec3 reflectionColor = iImage2.eval(distortedCoord * iResolution.xy * renderingScale).rgb;
    vec3 tintedReflectionColor = mix(reflectionColor, reflectionColor * waterTint, 0.5);

    finalColor += rippleIntensity * (length(combinedDisplacement) / amplitude);
    finalColor = mix(finalColor, tintedReflectionColor, reflectionFactor * reflectionIntensity);

    return vec4(finalColor, baseColor.a);
}
""";

    public MultiRippleWithTouchEffect()
    {
        ShaderCode = RippleShaderCode;
    }

    protected bool Initialized { get; set; }

    private System.Drawing.PointF _mouse;

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

        var activeRipples = GetActiveRipples();

        var mouseArray = new float[10 * 2];
        var progressArray = new float[10];

        for (int index = 0; index < 10; index++)
        {
            if (index < activeRipples.Count)
            {
                var ripple = activeRipples[index];
                mouseArray[index * 2] = ripple.Origin.X;
                mouseArray[index * 2 + 1] = ripple.Origin.Y;
                progressArray[index] = (float)ripple.Progress;
            }
            else
            {
                mouseArray[index * 2] = 0;
                mouseArray[index * 2 + 1] = 0;
                progressArray[index] = -1f;
            }
        }

        uniforms["origins"] = mouseArray;
        uniforms["progresses"] = progressArray;

        return uniforms;
    }

    public class Ripple
    {
        public Guid Uid { get; set; }
        public System.Drawing.PointF Origin { get; set; }
        public long Time { get; set; }
        public double Progress { get; set; }
    }

    private ConcurrentDictionary<Guid, Ripple> Ripples { get; } = new();

    public Ripple CreateRipple(System.Drawing.PointF origin)
    {
        var ripple = new Ripple
        {
            Uid = Guid.NewGuid(),
            Origin = origin,
            Time = Super.GetCurrentTimeNanos()
        };

        Ripples[ripple.Uid] = ripple;
        return ripple;
    }

    public void RemoveRipple(Guid key)
    {
        Ripples.TryRemove(key, out _);
    }

    public List<Ripple> GetActiveRipples()
    {
        return Ripples.Values.OrderByDescending(x => x.Time).Take(10).ToList();
    }

    public virtual ISkiaGestureListener ProcessGestures(
        SkiaGesturesParameters args,
        GestureEventProcessingInfo apply)
    {
        _mouse = args.Event.Location;

        if (args.Type == TouchActionResult.Down && Initialized)
        {
            var ripple = CreateRipple(_mouse);

            Task.Run(async () =>
            {
                await Parent.AnimateRangeAsync(value =>
                {
                    ripple.Progress = value;
                    Update();
                }, 0, 1, 4500);

                RemoveRipple(ripple.Uid);
            }).ConfigureAwait(false);
        }

        return null;
    }
}