namespace DrawnUi.Draw;

/// <summary>
/// A measured or available size in units and pixels. Immutable: every value is set when it is made, so it can be shared
/// as it is (<see cref="Default"/> is one instance, <see cref="Clone"/> returns the same instance).
/// </summary>
[DebuggerDisplay("{ToString()}")]
public class ScaledSize : IComparable, IComparable<ScaledSize>, IEquatable<ScaledSize>
{
    public ScaledSize()
    {
        Units = default;
        Pixels = default;
        WidthCut = false;
        HeightCut = false;
        Scale = 1;
    }

    public ScaledSize WithCut(bool widthCut, bool heightCut)
    {
        if (widthCut == WidthCut && heightCut == HeightCut)
            return this;
        return Reuse(Scale, Units, Pixels, widthCut, heightCut);
    }

    /// <summary>The empty default size (scale 1). One shared instance.</summary>
    public static ScaledSize Default { get; } = new();

    private static ScaledSize Reuse(float scale, SKSize units, SKSize pixels, bool widthCut, bool heightCut)
        => new() { Scale = scale, Units = units, Pixels = pixels, WidthCut = widthCut, HeightCut = heightCut };

    public float Scale { get; init; }

    public SKSize Units { get; init; }
    public SKSize Pixels { get; init; }
    public bool WidthCut { get; init; }
    public bool HeightCut { get; init; }

    public int CompareTo(object obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is not ScaledSize other)
        {
            return 0;
        }

        return CompareTo(other);
    }

    public int CompareTo(ScaledSize other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (other is null)
        {
            return 1;
        }

        var result = Pixels.Width.CompareTo(other.Pixels.Width);
        if (result != 0)
        {
            return result;
        }

        return Pixels.Height.CompareTo(other.Pixels.Height);
    }

    public bool Equals(ScaledSize other)
    {
        return CompareTo(other) == 0;
    }

    public override bool Equals(object obj)
    {
        return obj is ScaledSize other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Pixels.Width, Pixels.Height);
    }

    public static bool operator ==(ScaledSize left, ScaledSize right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ScaledSize left, ScaledSize right)
    {
        return !Equals(left, right);
    }

    /// <summary>Returns this instance: a ScaledSize never changes after it is made, so a copy is not needed.</summary>
    public ScaledSize Clone() => this;

    public static ScaledSize CreateEmpty(float scale)
    {
        return Reuse(scale, SKSize.Empty, SKSize.Empty, false, false);
    }

    public bool IsEmpty
    {
        get
        {
            return Pixels.IsEmpty;
        }
    }
    
    public override string ToString()
    {
        return $"Pixels {Pixels},  Units {Units}";
    }

    public static float SnapToPixel(double point, double scale)
    {
        return (float)(Math.Round(point * scale) / scale);
    }


    public static ScaledSize FromUnits(float width, float height, float scale)
    {
        if (double.IsNaN(width))
            width = -1;
        if (double.IsNaN(height))
            height = -1;

        var nWidth = (float)(width * scale);
        if (nWidth < 0)
            nWidth = -1;
        if (float.IsInfinity(width))
        {
            nWidth = float.PositiveInfinity;
        }

        var nHeight = (float)(height * scale);
        if (nHeight < 0)
            nHeight = -1;

        if (float.IsInfinity(height))
        {
            nHeight = float.PositiveInfinity;
        }

        return Reuse(scale, new SKSize(width, height), new SKSize((float)Math.Round(nWidth), (float)Math.Round(nHeight)), false, false);
    }

    public static ScaledSize FromPixels(SKSize size, float scale)
    {
        return FromPixels((float)(size.Width), (float)(size.Height), false, false, scale);
    }

    public static ScaledSize FromPixels(float width, float height, float scale)
    {
        return FromPixels(width, height, false, false, scale);
    }

    public static ScaledSize FromPixels(float width, float height, bool widthCut, bool heighCut, float scale)
    {
        if (double.IsNaN(width))
            width = -1;
        if (double.IsNaN(height))
            height = -1;

        var nWidth = width / scale;
        if (nWidth < 0)
            nWidth = -1;

        var nHeight = height / scale;
        if (nHeight < 0)
            nHeight = -1;

#if disabledDEBUG
        if (Math.Round(width) != width || Math.Round(height) != height)
        {
            Debug.WriteLine($"[Warning] Width or height in PIXELS has values after decimal point.");
            width = (float)Math.Round(width);
            height = (float)Math.Round(height);
        }
#else
        width = (float)Math.Round(width);
        height = (float)Math.Round(height);
#endif

        return Reuse(scale, new SKSize(nWidth, nHeight), new SKSize(width, height), widthCut, heighCut);
    }
}
