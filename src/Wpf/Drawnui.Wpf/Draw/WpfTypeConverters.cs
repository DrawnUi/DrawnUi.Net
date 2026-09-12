using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DrawnUi.Wpf;

/// <summary>
/// Lets DrawnUI's value types be written as plain XAML attributes — <c>BackgroundColor="Red"</c>,
/// <c>Padding="10"</c>, <c>CornerRadius="8,8,0,0"</c>.
/// <para>
/// Parsing is delegated to WPF's own converters rather than reinvented, so every syntax WPF accepts
/// works here too: named colours, <c>#RGB</c>, <c>#ARGB</c>, <c>#RRGGBB</c>, <c>#AARRGGBB</c>,
/// <c>sc#</c> floats, and the 1/2/4-value forms of Thickness and CornerRadius. Only the final hop
/// onto DrawnUI's own struct is ours.
/// </para>
/// <para>
/// They are attached in WpfTypeConverterAttributes.cs, via partial declarations of the shared types.
/// </para>
/// </summary>

/// <summary>Converts XAML text to a DrawnUI <see cref="DrawnUi.Color"/> using WPF's colour parsing.</summary>
public class DrawnColorConverter : TypeConverter
{
    private static readonly TypeConverter Wpf = new System.Windows.Media.ColorConverter();

    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        => sourceType == typeof(string);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        => destinationType == typeof(string);

    /// <inheritdoc/>
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();

        try
        {
            if (Wpf.ConvertFrom(context, culture, text) is System.Windows.Media.Color media)
                return DrawnUi.Color.FromRgba(media.R, media.G, media.B, media.A);
        }
        catch
        {
            // Not something WPF recognises — fall through to DrawnUI's own hex parsing, which also
            // covers forms WPF does not, and keeps behaviour aligned with the other heads.
        }

        return text.ToColor();
    }

    /// <inheritdoc/>
    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        => value is DrawnUi.Color color ? color.ToHex() : base.ConvertTo(context, culture, value, destinationType);
}

/// <summary>Converts XAML text to a DrawnUI <see cref="DrawnUi.Views.Thickness"/> using WPF's parsing.</summary>
public class DrawnThicknessConverter : TypeConverter
{
    private static readonly TypeConverter Wpf = new System.Windows.ThicknessConverter();

    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        => sourceType == typeof(string);

    /// <inheritdoc/>
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is not string text)
            return new DrawnUi.Views.Thickness(0);

        var wpf = (System.Windows.Thickness)Wpf.ConvertFrom(context, culture, text.Trim());
        return new DrawnUi.Views.Thickness(wpf.Left, wpf.Top, wpf.Right, wpf.Bottom);
    }
}

/// <summary>
/// Converts XAML text to a DrawnUI <see cref="DrawnUi.Draw.CornerRadius"/>.
/// <para>
/// Deliberately NOT delegated to WPF's CornerRadiusConverter. The four-value forms mean different
/// things: WPF reads "a,b,c,d" as TopLeft, TopRight, BottomRight, BottomLeft, while DrawnUI and MAUI
/// read it as TopLeft, TopRight, BottomLeft, BottomRight. Delegating would silently rotate the bottom
/// corners of any XAML moved between heads, so the DrawnUI order is parsed here instead.
/// </para>
/// </summary>
public class DrawnCornerRadiusConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        => sourceType == typeof(string);

    /// <inheritdoc/>
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
            return new DrawnUi.Draw.CornerRadius(0);

        var parts = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var numbers = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]))
                throw new FormatException($"'{text}' is not a valid CornerRadius.");
        }

        return numbers.Length switch
        {
            1 => new DrawnUi.Draw.CornerRadius(numbers[0]),
            4 => new DrawnUi.Draw.CornerRadius(numbers[0], numbers[1], numbers[2], numbers[3]),
            _ => throw new FormatException($"'{text}' is not a valid CornerRadius: expected 1 or 4 values.")
        };
    }
}
