using System.ComponentModel;
using System.Globalization;

namespace DrawnUi.Wpf;

/// <summary>
/// Parses the MAUI grid definition shorthand for XAML — <c>ColumnDefinitions="*,2*,Auto,40"</c>,
/// <c>RowDefinitions="Auto,*"</c>. Comma or whitespace separated; <c>*</c> and <c>N*</c> are star
/// lengths, <c>Auto</c> is auto, a plain number is absolute points.
/// </summary>
public static class DrawnGridLengthParser
{
    /// <summary>Parses one token into a <see cref="DrawnUi.Draw.GridLength"/>.</summary>
    public static DrawnUi.Draw.GridLength ParseLength(string token)
    {
        token = token.Trim();

        if (token.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return DrawnUi.Draw.GridLength.Auto;

        if (token == "*")
            return DrawnUi.Draw.GridLength.Star;

        if (token.EndsWith('*'))
        {
            var factor = token[..^1];
            if (!double.TryParse(factor, NumberStyles.Float, CultureInfo.InvariantCulture, out var stars))
                throw new FormatException($"'{token}' is not a valid star length.");

            return new DrawnUi.Draw.GridLength(stars, DrawnUi.Draw.GridUnitType.Star);
        }

        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var absolute))
            throw new FormatException($"'{token}' is not a valid grid length.");

        return new DrawnUi.Draw.GridLength(absolute, DrawnUi.Draw.GridUnitType.Absolute);
    }

    /// <summary>Splits a definitions string into its lengths.</summary>
    public static IEnumerable<DrawnUi.Draw.GridLength> ParseAll(string text)
        => text.Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(ParseLength);
}

/// <summary>Converts XAML text to a <see cref="DrawnUi.Draw.ColumnDefinitionCollection"/>.</summary>
public class DrawnColumnDefinitionsConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        => sourceType == typeof(string);

    /// <inheritdoc/>
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is not string text)
            return new DrawnUi.Draw.ColumnDefinitionCollection();

        return new DrawnUi.Draw.ColumnDefinitionCollection(
            DrawnGridLengthParser.ParseAll(text).Select(l => new DrawnUi.Draw.ColumnDefinition(l)).ToArray());
    }
}

/// <summary>Converts XAML text to a <see cref="DrawnUi.Draw.RowDefinitionCollection"/>.</summary>
public class DrawnRowDefinitionsConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        => sourceType == typeof(string);

    /// <inheritdoc/>
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is not string text)
            return new DrawnUi.Draw.RowDefinitionCollection();

        return new DrawnUi.Draw.RowDefinitionCollection(
            DrawnGridLengthParser.ParseAll(text).Select(l => new DrawnUi.Draw.RowDefinition(l)).ToArray());
    }
}
