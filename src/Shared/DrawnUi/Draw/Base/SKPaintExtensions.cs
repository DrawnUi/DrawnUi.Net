using System.Runtime.CompilerServices;
using SkiaSharp;

namespace DrawnUi.Draw;

/// <summary>
/// Guarded setters for SKPaint properties. Each method compares a managed shadow value
/// before calling the native P/Invoke setter, eliminating redundant interop when the
/// value has not changed between frames.
/// Methods are prefixed with "Guard" to avoid conflicts with SkiaSharp's own instance methods.
/// </summary>
public static class SKPaintExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardColor(this SKPaint paint, ref SKColor cache, SKColor value)
    {
        if (cache != value) { cache = value; paint.Color = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardStyle(this SKPaint paint, ref SKPaintStyle cache, SKPaintStyle value)
    {
        if (cache != value) { cache = value; paint.Style = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardIsDither(this SKPaint paint, ref bool cache, bool value)
    {
        if (cache != value) { cache = value; paint.IsDither = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardIsAntialias(this SKPaint paint, ref bool cache, bool value)
    {
        if (cache != value) { cache = value; paint.IsAntialias = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardStrokeWidth(this SKPaint paint, ref float cache, float value)
    {
        if (cache != value) { cache = value; paint.StrokeWidth = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardBlendMode(this SKPaint paint, ref SKBlendMode cache, SKBlendMode value)
    {
        if (cache != value) { cache = value; paint.BlendMode = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardFontSize(this SKFont font, ref float cache, float value)
    {
        if (cache != value) { cache = value; font.Size = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardFontTypeface(this SKFont font, ref SKTypeface cache, SKTypeface value)
    {
        if (!ReferenceEquals(cache, value)) { cache = value; font.Typeface = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardFontSkewX(this SKFont font, ref float cache, float value)
    {
        if (cache != value) { cache = value; font.SkewX = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardFontEmbolden(this SKFont font, ref bool cache, bool value)
    {
        if (cache != value) { cache = value; font.Embolden = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardFontEdging(this SKFont font, ref SKFontEdging cache, SKFontEdging value)
    {
        if (cache != value) { cache = value; font.Edging = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardFontSubpixel(this SKFont font, ref bool? cache, bool value)
    {
        if (cache != value) { cache = value; font.Subpixel = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardStrokeCap(this SKPaint paint, ref SKStrokeCap cache, SKStrokeCap value)
    {
        if (cache != value) { cache = value; paint.StrokeCap = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardImageFilter(this SKPaint paint, ref SKImageFilter cache, SKImageFilter value)
    {
        if (!ReferenceEquals(cache, value)) { cache = value; paint.ImageFilter = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardColorFilter(this SKPaint paint, ref SKColorFilter cache, SKColorFilter value)
    {
        if (!ReferenceEquals(cache, value)) { cache = value; paint.ColorFilter = value; }
    }
}
