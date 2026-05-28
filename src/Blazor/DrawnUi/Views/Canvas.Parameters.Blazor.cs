using System.Globalization;
using DrawnUi;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace DrawnUi.Views;

public partial class Canvas
{
    public ElementReference HostReference => _hostElement;

    [Parameter]
    public new string BackgroundColor
    {
        get
        {
            return _backgroundColor;
        }
        set
        {
            if (_backgroundColor != value)
            {
                _backgroundColor = value;

                base.BackgroundColor =
                    string.IsNullOrWhiteSpace(value) ? default(Color): Color.Parse(value);
            }
        }
    } 

    string _backgroundColor = "#00000000";

    [Parameter]
    public new string Margin { get; set; } = string.Empty;

    private string BackgroundColorCss => string.IsNullOrWhiteSpace(BackgroundColor)
        ? "transparent"
        : Color.Parse(BackgroundColor).ToHexRgba();

    private Thickness ParsedMargin => ParseThickness(Margin);

    private static Thickness ParseThickness(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Thickness.Zero;

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var numbers = parts
            .Select(part => double.Parse(part, CultureInfo.InvariantCulture))
            .ToArray();

        return numbers.Length switch
        {
            1 => new Thickness(numbers[0]),
            2 => new Thickness(numbers[0], numbers[1]),
            4 => new Thickness(numbers[0], numbers[1], numbers[2], numbers[3]),
            _ => throw new FormatException($"Unsupported thickness value '{value}'.")
        };
    }
}
