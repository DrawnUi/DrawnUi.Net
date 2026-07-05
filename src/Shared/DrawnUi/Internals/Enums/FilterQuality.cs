namespace DrawnUi.Draw;

/// <summary>
/// Image filtering quality level. Maps to SkiaSharp sampling options.
/// </summary>
public enum FilterQuality
{
    /// <summary>
    /// No filtering, nearest neighbor. Fastest.
    /// </summary>
    None = 0,

    /// <summary>
    /// Linear filtering. Good balance of quality and performance.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Linear with mipmapping for better downscaling.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Linear filtering with mipmaps, matches the old SkiaSharp default high quality. Fast on GPU.
    /// </summary>
    High = 3,

    /// <summary>
    /// Cubic filtering (Mitchell). Best quality, slowest.
    /// </summary>
    Ultra = 4
}

public static class SkiaSamplingOptions
{
    /// <summary>
    /// Nearest neighbor, no mipmaps. Fastest, correct for 1:1 blits.
    /// </summary>
    public static readonly SKSamplingOptions NearestNoMip =
        new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>
    /// Linear filtering with linear mipmaps. Smooth up- and downscaling.
    /// </summary>
    public static readonly SKSamplingOptions LinearLinear =
        new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>
    /// Linear filtering with nearest mipmaps.
    /// </summary>
    public static readonly SKSamplingOptions LinearNearest =
        new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest);

    /// <summary>
    /// Linear filtering, no mipmaps. Cheap smoothing for mildly transformed draws.
    /// </summary>
    public static readonly SKSamplingOptions LinearNoMip =
        new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);

    /// <summary>
    /// Mitchell cubic resampler. Best quality upscaling, slowest.
    /// </summary>
    public static readonly SKSamplingOptions MitchellCubic =
        new SKSamplingOptions(SKCubicResampler.Mitchell);

    /// <summary>
    /// Catmull-Rom. Sharper than Mitchell, can introduce ringing.
    /// </summary>
    public static readonly SKSamplingOptions CatmullRomCubic =
        new SKSamplingOptions(SKCubicResampler.CatmullRom);


    public static SKSamplingOptions QualityNone => GetSamplingOptions(FilterQuality.None);
    public static SKSamplingOptions QualityLow => GetSamplingOptions(FilterQuality.Low);
    public static SKSamplingOptions QualityMedium => GetSamplingOptions(FilterQuality.Medium);
    public static SKSamplingOptions QualityHigh => GetSamplingOptions(FilterQuality.High);
    public static SKSamplingOptions QualityUltra => GetSamplingOptions(FilterQuality.Ultra);

    public static SKSamplingOptions GetSamplingOptions(FilterQuality quality) => GetSamplingOptions(quality, false);

    public static SKSamplingOptions GetSamplingOptions(FilterQuality quality, bool isUpscaling)
    {
        return quality switch
        {
            FilterQuality.None => NearestNoMip,

            FilterQuality.Low => LinearNoMip,

            FilterQuality.Medium => LinearNearest,

            FilterQuality.High => LinearLinear,

            FilterQuality.Ultra => isUpscaling
                ? MitchellCubic
                : LinearLinear,        // cubic shimmer-prone when downscaling, linear+mips better

            _ => LinearNoMip
        };
    }
}
