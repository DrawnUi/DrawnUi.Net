using DrawnUi.Draw;
using SkiaSharp;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// A gradient's Light above 1 raises each color's HSL lightness by that share of itself, hue and saturation kept: the
/// .NET MAUI rule (Color.AddLuminosity), the same on every head and engine.
/// </summary>
public class GradientLightTests
{
    [Theory]
    [InlineData("#20C997", 40)]
    [InlineData("#0F3460", 40)]
    [InlineData("#E94560", 25)]
    public void MakeLighter_raises_the_lightness_and_keeps_the_hue_and_saturation(string hex, double percent)
    {
        var color = Color.Parse(hex);
        new SKColor((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255)).ToHsl(out var h, out var s, out var l);

        var lighter = color.MakeLighter(percent);
        new SKColor((byte)Math.Round(lighter.Red * 255), (byte)Math.Round(lighter.Green * 255), (byte)Math.Round(lighter.Blue * 255))
            .ToHsl(out var h2, out var s2, out var l2);

        Assert.InRange(l2, l * (1 + percent / 100) - 1.5, l * (1 + percent / 100) + 1.5);
        Assert.InRange(s2, s - 3, s + 3);
        Assert.InRange(h2, h - 3, h + 3);
    }
}
