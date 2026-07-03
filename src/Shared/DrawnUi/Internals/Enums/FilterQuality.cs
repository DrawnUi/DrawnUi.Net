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
    /// Cubic filtering (Mitchell). Best quality, slowest.
    /// </summary>
    High = 3
}
