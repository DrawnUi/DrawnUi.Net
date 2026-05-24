using DrawnUi.Draw;
using SkiaSharp;
using Color = DrawnUi.Color;

/// <summary>
/// Liquid glass shader effect. Uses ShaderCode (inline SKSL) so it works on the Net/OpenTK target
/// where ShaderSource file-loading is not supported.
/// </summary>
internal sealed class GlassBackdropEffect : SkiaShaderEffect
{
    public GlassBackdropEffect()
    {
        ShaderCode = GlassSksl;
    }

    private readonly float[] _uniformTint = new float[4];

    // ── Properties ────────────────────────────────────────────────────────────

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(float), typeof(GlassBackdropEffect), 0f,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty EmbossProperty = BindableProperty.Create(
        nameof(Emboss), typeof(float), typeof(GlassBackdropEffect), 10.0f,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty DepthProperty = BindableProperty.Create(
        nameof(Depth), typeof(float), typeof(GlassBackdropEffect), 1.0f,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty BlurStrengthProperty = BindableProperty.Create(
        nameof(BlurStrength), typeof(float), typeof(GlassBackdropEffect), 1.0f,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty OpacityProperty = BindableProperty.Create(
        nameof(Opacity), typeof(float), typeof(GlassBackdropEffect), 0.75f,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty TintProperty = BindableProperty.Create(
        nameof(Tint), typeof(Color), typeof(GlassBackdropEffect), Colors.Transparent,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty EdgeOpacityProperty = BindableProperty.Create(
        nameof(EdgeOpacity), typeof(float), typeof(GlassBackdropEffect), 0.5f,
        propertyChanged: OnPropertyChanged);

    public static readonly BindableProperty EdgeGlowProperty = BindableProperty.Create(
        nameof(EdgeGlow), typeof(float), typeof(GlassBackdropEffect), 0.95f,
        propertyChanged: OnPropertyChanged);

    public float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Refraction/emboss displacement as % of image width. Range 0–~30, default 10.</summary>
    public float Emboss
    {
        get => (float)GetValue(EmbossProperty);
        set => SetValue(EmbossProperty, value);
    }

    /// <summary>3D depth intensity. Range 0–2+, default 1.</summary>
    public float Depth
    {
        get => (float)GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }

    /// <summary>Blur intensity multiplier. Default 1.</summary>
    public float BlurStrength
    {
        get => (float)GetValue(BlurStrengthProperty);
        set => SetValue(BlurStrengthProperty, value);
    }

    /// <summary>Glass panel opacity. Range 0–1, default 0.75.</summary>
    public float Opacity
    {
        get => (float)GetValue(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    public float EdgeOpacity
    {
        get => (float)GetValue(EdgeOpacityProperty);
        set => SetValue(EdgeOpacityProperty, value);
    }

    public float EdgeGlow
    {
        get => (float)GetValue(EdgeGlowProperty);
        set => SetValue(EdgeGlowProperty, value);
    }

    /// <summary>Tint color; alpha controls tint strength.</summary>
    public Color Tint
    {
        get => (Color)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    private static void OnPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GlassBackdropEffect effect) effect.Update();
    }

    protected override SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var uniforms = base.CreateUniforms(destination);

        var scale = Parent?.RenderingScale ?? 1f;
        uniforms["iCornerRadius"] = CornerRadius * scale;
        uniforms["iEmboss"]       = Emboss;
        uniforms["iDepth"]        = Depth;
        uniforms["iBlurStrength"] = BlurStrength;
        uniforms["iOpacity"]      = Opacity;
        uniforms["iEdgeOpacity"]  = EdgeOpacity;
        uniforms["iEdgeGlow"]     = EdgeGlow;

        var c = Tint;
        _uniformTint[0] = (float)c.Red;   _uniformTint[1] = (float)c.Green;
        _uniformTint[2] = (float)c.Blue;  _uniformTint[3] = (float)c.Alpha;
        uniforms["iTint"] = _uniformTint;

        return uniforms;
    }

    // ── Inline SKSL ───────────────────────────────────────────────────────────
    // Originated from MIT-licensed https://github.com/bergice/liquidglass
    // Heavily modified by taublast, still MIT.

    private const string GlassSksl = """
        uniform float4 iMouse;
        uniform float  iTime;
        uniform float2 iResolution;
        uniform float2 iImageResolution;
        uniform shader iImage1;
        uniform float2 iOffset;
        uniform float2 iOrigin;

        uniform float  iCornerRadius;
        uniform float  iEmboss;
        uniform float  iDepth;
        uniform float  iBlurStrength;
        uniform float  iOpacity;
        uniform float  iEdgeOpacity;
        uniform float  iEdgeGlow;
        uniform float4 iTint;

        const float EDGE_DISTANCE = 1.0;

        float roundedBox(float2 uv, float2 center, float2 size, float radius) {
            float2 q = abs(uv - center) - size + radius;
            return length(max(q, 0.0)) - radius;
        }

        half3 blurBackground(float2 uv) {
            float2 step = 2.8 * iBlurStrength / iImageResolution;
            half3 center = iImage1.eval(uv).rgb;
            const half w_center = 0.32;
            const half w_axis   = 0.16;
            const half w_diag   = 0.09;
            half3 sum = center * w_center;
            sum += iImage1.eval(uv + float2( step.x, 0.0)).rgb * w_axis;
            sum += iImage1.eval(uv - float2( step.x, 0.0)).rgb * w_axis;
            sum += iImage1.eval(uv + float2(0.0,  step.y)).rgb * w_axis;
            sum += iImage1.eval(uv - float2(0.0,  step.y)).rgb * w_axis;
            sum += iImage1.eval(uv + step).rgb * w_diag;
            sum += iImage1.eval(uv - step).rgb * w_diag;
            sum += iImage1.eval(uv + float2( step.x, -step.y)).rgb * w_diag;
            sum += iImage1.eval(uv + float2(-step.x,  step.y)).rgb * w_diag;
            return sum;
        }

        float2 getNormal(float2 uv, float2 center, float2 size, float radius) {
            float2 eps = float2(1.0) / iImageResolution * 2.0;
            float2 p = uv - center;
            float dx = (roundedBox(p + float2(eps.x, 0.0), float2(0.0), size, radius) -
                        roundedBox(p - float2(eps.x, 0.0), float2(0.0), size, radius)) * 0.5;
            float dy = (roundedBox(p + float2(0.0, eps.y), float2(0.0), size, radius) -
                        roundedBox(p - float2(0.0, eps.y), float2(0.0), size, radius)) * 0.5;
            float2 gradient = float2(dx, dy);
            if (length(gradient) < 0.001) return float2(0.0);
            return normalize(gradient);
        }

        float2 scaledDisplaceUV(float2 displaced, float2 original) {
            return clamp(displaced, float2(0.0), iImageResolution - 1.0);
        }

        half4 main(float2 fragCoord) {
            float2 inputCoord = fragCoord - iOffset;
            float distort = 1.66;
            float radiusInPixels = iCornerRadius;
            float2 center = iImageResolution * 0.5;
            float2 size   = (iImageResolution - EDGE_DISTANCE * 2.0) * 0.5;
            float dist = roundedBox(inputCoord, center, size, radiusInPixels);
            float aaWidth = 2.0;
            float alpha   = 1.0 - smoothstep(-aaWidth, aaWidth, dist);
            if (alpha <= 0.001) return iImage1.eval(inputCoord);

            float2 uv = inputCoord / iImageResolution;
            float localYNorm = (inputCoord.y - center.y) / size.y;
            float2 local = (inputCoord - center) / size;
            float zoomDisplace = min(iImageResolution.x, iImageResolution.y) * (iEmboss / 100.0);

            float r = clamp(length(local), 0.0, 1.0);
            float2 domeNormal = normalize(local);
            float eta = 1.0 / 1.5;
            float2 incident = -domeNormal;
            float2 refractVec = refract(incident, domeNormal, eta);
            float2 curvedRefractUV = inputCoord + refractVec * zoomDisplace * distort;

            float contourFalloff = exp(-abs(dist) * 0.4);
            float2 normal = getNormal(inputCoord, center, size, radiusInPixels);
            float2 domeNormalContour = normal * pow(contourFalloff, 1.5);
            float2 refractVecContour = refract(float2(-iDepth/50), domeNormalContour, eta);
            float2 uvContour = inputCoord + refractVecContour * (zoomDisplace * 1.167) * iDepth * contourFalloff;

            float edgeWeight    = smoothstep(0.1, 1.0, abs(dist));
            float radialWeight  = smoothstep(0.5, 1.0, r);
            float combinedWeight = clamp(edgeWeight - radialWeight * 0.5, 0.0, 1.0);
            float2 refractUV = scaledDisplaceUV(mix(curvedRefractUV, uvContour, combinedWeight), inputCoord);

            half3 refracted = iImage1.eval(refractUV).rgb;
            half3 blurred   = blurBackground(refractUV);
            half3 base      = mix(refracted, blurred, 0.5);

            float edgeFalloff  = smoothstep(0.01, 0.0, dist);
            float verticalBand = 1.0 - smoothstep(-1.5, -0.2, localYNorm);
            float topShadow    = edgeFalloff * verticalBand;
            half3 shadowColor  = half3(0.0);
            base = mix(base, shadowColor, topShadow * 0.25 * iDepth);

            float glowWidthPixels = zoomDisplace * (11.0 / 30.0) * iDepth;
            float edge = 1.0 - smoothstep(0.0, glowWidthPixels, dist * -2.0);
            half3 edgeSample = iImage1.eval(scaledDisplaceUV(uvContour, inputCoord)).rgb;
            half3 glow = mix(edgeSample * 2.5 * iEdgeGlow, half3(0.5), 0.1);
            half3 color = mix(base * 1.1, glow * iEdgeGlow, edge * iEdgeOpacity * iDepth);

            color = mix(color, iTint.rgb, iTint.a);

            half4 originalColor = iImage1.eval(inputCoord);
            half3 finalColor = mix(originalColor.rgb, color, alpha);
            float finalAlpha = originalColor.a * (iOpacity * alpha + (1.0 - alpha));
            return half4(finalColor, finalAlpha);
        }
        """;
}
