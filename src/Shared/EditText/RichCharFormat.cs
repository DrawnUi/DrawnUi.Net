#if DEBUG
namespace DrawnUi.Draw;

/// <summary>
/// Per-character format for SkiaRichEditor. Null fields mean "inherit from editor defaults".
/// </summary>
public struct RichCharFormat : IEquatable<RichCharFormat>
{
    /// <summary>null = inherit. true = bold on. false = explicitly off.</summary>
    public bool? Bold;
    public bool? Italic;
    public bool? Underline;
    public bool? Strikethrough;

    /// <summary>null = use editor TextColor</summary>
    public Color? ForegroundColor;

    /// <summary>null = transparent background</summary>
    public Color? BackgroundColor;

    /// <summary>null = use editor FontSize</summary>
    public double? FontSize;

    /// <summary>null/empty = use editor FontFamily</summary>
    public string? FontFamily;

    /// <summary>null = use editor FontWeight</summary>
    public int? FontWeight;

    /// <summary>null = default red strikeout color</summary>
    public Color? StrikeoutColor;

    public static readonly RichCharFormat Default = new();

    public readonly bool IsEffectivelyEmpty =>
        Bold != true && Italic != true && Underline != true && Strikethrough != true
        && ForegroundColor == null && BackgroundColor == null
        && !FontSize.HasValue && string.IsNullOrEmpty(FontFamily)
        && !FontWeight.HasValue;

    /// <summary>
    /// Returns a new format that merges this with <paramref name="delta"/>:
    /// delta fields take priority, falling back to this for null delta fields.
    /// </summary>
    public readonly RichCharFormat MergeWith(RichCharFormat delta) => new()
    {
        Bold = delta.Bold ?? Bold,
        Italic = delta.Italic ?? Italic,
        Underline = delta.Underline ?? Underline,
        Strikethrough = delta.Strikethrough ?? Strikethrough,
        ForegroundColor = delta.ForegroundColor ?? ForegroundColor,
        BackgroundColor = delta.BackgroundColor ?? BackgroundColor,
        FontSize = delta.FontSize ?? FontSize,
        FontFamily = string.IsNullOrEmpty(delta.FontFamily) ? FontFamily : delta.FontFamily,
        FontWeight = delta.FontWeight ?? FontWeight,
        StrikeoutColor = delta.StrikeoutColor ?? StrikeoutColor,
    };

    public readonly bool Equals(RichCharFormat other) =>
        Bold == other.Bold && Italic == other.Italic
        && Underline == other.Underline && Strikethrough == other.Strikethrough
        && Nullable.Equals(ForegroundColor, other.ForegroundColor)
        && Nullable.Equals(BackgroundColor, other.BackgroundColor)
        && FontSize == other.FontSize
        && FontFamily == other.FontFamily
        && FontWeight == other.FontWeight
        && Nullable.Equals(StrikeoutColor, other.StrikeoutColor);

    public override readonly bool Equals(object? obj) => obj is RichCharFormat f && Equals(f);
    public override readonly int GetHashCode() =>
        HashCode.Combine(Bold, Italic, Underline, Strikethrough, ForegroundColor, FontSize, FontFamily, FontWeight);
    public static bool operator ==(RichCharFormat a, RichCharFormat b) => a.Equals(b);
    public static bool operator !=(RichCharFormat a, RichCharFormat b) => !a.Equals(b);
}
#endif
